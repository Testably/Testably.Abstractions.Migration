using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Testably.Abstractions.Migration.Analyzers;

/// <summary>
///     Fix-all provider for <see cref="SystemIOAbstractionsCodeFixProvider" />. Processes
///     every diagnostic in a document in a single pass, so that the using-directive swap
///     happens exactly once at the end instead of once per rewrite. The default
///     <see cref="WellKnownFixAllProviders.BatchFixer" /> drops fixes whose text changes
///     overlap (every per-diagnostic rewrite that touches the using line collides), and
///     once the using is swapped the analyzer no longer fires on the remaining
///     constructors — leaving the file partially migrated and non-compiling.
/// </summary>
internal sealed class SystemIOAbstractionsFixAllProvider : DocumentBasedFixAllProvider
{
	public static readonly SystemIOAbstractionsFixAllProvider Instance = new();

	private SystemIOAbstractionsFixAllProvider()
	{
	}

	protected override async Task<Document?> FixAllAsync(
		FixAllContext fixAllContext,
		Document document,
		ImmutableArray<Diagnostic> diagnostics)
		=> await MigrateDocumentAsync(document, diagnostics, fixAllContext.CancellationToken)
			.ConfigureAwait(false);

	/// <summary>
	///     Migrates every diagnostic in <paramref name="diagnostics" /> within
	///     <paramref name="document" /> in a single pass: annotate target nodes, dispatch
	///     each pattern's pure rewriter sequentially with re-acquired semantic models,
	///     then apply the using-directive change exactly once. Used both by the fix-all
	///     scopes (Document/Project/Solution) and by the single-diagnostic CodeAction —
	///     the per-diagnostic Fix re-discovers all sibling diagnostics in the document
	///     and routes through here so one click migrates the whole file (Mockolate-style).
	/// </summary>
	internal static async Task<Document> MigrateDocumentAsync(
		Document document,
		ImmutableArray<Diagnostic> diagnostics,
		CancellationToken cancellationToken)
	{
		if (diagnostics.IsEmpty)
		{
			return document;
		}

		SyntaxNode? root = await document
			.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax originalCu)
		{
			return document;
		}

		(CompilationUnitSyntax annotatedCu, List<WorkItem> work) = CollectAndAnnotate(originalCu, diagnostics);
		if (work.Count == 0)
		{
			return document;
		}

		// Compute the using-directive change up front from work-item intent rather than
		// from rewrite success. A block-level rewrite (e.g. FilesCtor initializer
		// expansion) can absorb a sibling annotated node so that its later dispatch
		// returns null — but the absorbed item's using-change contribution must still
		// land, otherwise the file would be left half-migrated.
		SystemIOAbstractionsCodeFixProvider.UsingChange maxUsingChange =
			SystemIOAbstractionsCodeFixProvider.UsingChange.None;
		foreach (WorkItem item in work)
		{
			SystemIOAbstractionsCodeFixProvider.UsingChange change =
				SystemIOAbstractionsCodeFixProvider.GetUsingChange(item.Pattern);
			if (change > maxUsingChange)
			{
				maxUsingChange = change;
			}
		}

		(CompilationUnitSyntax finalCu, bool anyRewriteSucceeded) = await ApplyRewritesAsync(
			document, annotatedCu, work, cancellationToken).ConfigureAwait(false);

		// Suppress the using swap when no rewrite landed. Otherwise we'd add the Testably
		// using to a file whose code still references TestingHelpers — non-compiling.
		if (anyRewriteSucceeded
		    && maxUsingChange != SystemIOAbstractionsCodeFixProvider.UsingChange.None)
		{
			finalCu = SystemIOAbstractionsCodeFixProvider.ApplyUsingChange(finalCu, maxUsingChange);
		}

		return document.WithSyntaxRoot(finalCu);
	}

	/// <summary>
	///     Walks the diagnostics in source order, finds each one's target node in the
	///     original compilation unit, and annotates every target so subsequent rewrites
	///     can locate it via <see cref="SyntaxNode.GetAnnotatedNodes(SyntaxAnnotation)" />.
	/// </summary>
	private static (CompilationUnitSyntax AnnotatedCu, List<WorkItem> Work) CollectAndAnnotate(
		CompilationUnitSyntax originalCu, ImmutableArray<Diagnostic> diagnostics)
	{
		List<WorkItem> work = [];
		Dictionary<SyntaxNode, List<SyntaxAnnotation>> nodeToAnnotations = new();
		foreach (Diagnostic diagnostic in diagnostics.OrderBy(d => d.Location.SourceSpan.Start))
		{
			if (!diagnostic.Properties.TryGetValue(Patterns.Key, out string? pattern) || pattern is null)
			{
				continue;
			}

			SyntaxNode? target = originalCu.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
			if (target is null)
			{
				continue;
			}

			SyntaxAnnotation annotation = new();
			work.Add(new WorkItem(pattern, annotation));

			if (!nodeToAnnotations.TryGetValue(target, out List<SyntaxAnnotation>? list))
			{
				list = [];
				nodeToAnnotations[target] = list;
			}

			list.Add(annotation);
		}

		if (work.Count == 0)
		{
			return (originalCu, work);
		}

		CompilationUnitSyntax annotatedCu = originalCu.ReplaceNodes(
			nodeToAnnotations.Keys,
			(original, _) => original.WithAdditionalAnnotations([..nodeToAnnotations[original]]));
		return (annotatedCu, work);
	}

	/// <summary>
	///     Applies each pattern's pure rewriter to <paramref name="annotatedCu" /> in
	///     sequence. Stays in-memory between iterations for purely-syntactic patterns and
	///     only round-trips through a fresh <see cref="Document" /> /
	///     <see cref="SemanticModel" /> when the pattern actually needs semantic info —
	///     that avoids an O(N) re-compile per rewrite for the (common) syntactic patterns.
	///     When a round-trip is necessary, both the rewrite target and the semantic model
	///     are taken from the round-tripped tree, since <see cref="Document.WithSyntaxRoot" />
	///     may produce a tree whose nodes are not reference-equal to the input.
	/// </summary>
	private static async Task<(CompilationUnitSyntax FinalCu, bool AnyRewriteSucceeded)> ApplyRewritesAsync(
		Document originalDocument,
		CompilationUnitSyntax annotatedCu,
		List<WorkItem> work,
		CancellationToken cancellationToken)
	{
		CompilationUnitSyntax currentCu = annotatedCu;
		bool anyRewriteSucceeded = false;

		foreach (WorkItem item in work)
		{
			CompilationUnitSyntax? rewritten;
			if (SystemIOAbstractionsCodeFixProvider.PatternNeedsSemanticModel(item.Pattern))
			{
				rewritten = await DispatchWithSemanticModelAsync(
					originalDocument, currentCu, item, cancellationToken).ConfigureAwait(false);
			}
			else
			{
				SyntaxNode? currentTarget = currentCu.GetAnnotatedNodes(item.Annotation).FirstOrDefault();
				if (currentTarget is null)
				{
					continue;
				}

				rewritten = SystemIOAbstractionsCodeFixProvider.DispatchPure(
					item.Pattern, currentCu, currentTarget, semanticModel: null, cancellationToken);
			}

			if (rewritten is null)
			{
				continue;
			}

			currentCu = rewritten;
			anyRewriteSucceeded = true;
		}

		return (currentCu, anyRewriteSucceeded);
	}

	/// <summary>
	///     Round-trips <paramref name="currentCu" /> through a document so the semantic
	///     model binds to the live tree, then re-locates the work item's annotated target
	///     in that round-tripped tree and dispatches the pattern's pure rewriter against
	///     it. Returns the new compilation unit (still annotation-bearing) or
	///     <see langword="null" /> if the target was absorbed or the rewrite is not
	///     applicable.
	/// </summary>
	private static async Task<CompilationUnitSyntax?> DispatchWithSemanticModelAsync(
		Document originalDocument,
		CompilationUnitSyntax currentCu,
		WorkItem item,
		CancellationToken cancellationToken)
	{
		Document syncedDoc = originalDocument.WithSyntaxRoot(currentCu);
		SyntaxNode? syncedRoot = await syncedDoc
			.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (syncedRoot is not CompilationUnitSyntax syncedCu)
		{
			return null;
		}

		SyntaxNode? syncedTarget = syncedCu.GetAnnotatedNodes(item.Annotation).FirstOrDefault();
		if (syncedTarget is null)
		{
			return null;
		}

		SemanticModel? semanticModel = await syncedDoc
			.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

		return SystemIOAbstractionsCodeFixProvider.DispatchPure(
			item.Pattern, syncedCu, syncedTarget, semanticModel, cancellationToken);
	}

	private readonly struct WorkItem
	{
		public WorkItem(string pattern, SyntaxAnnotation annotation)
		{
			Pattern = pattern;
			Annotation = annotation;
		}

		public string Pattern { get; }
		public SyntaxAnnotation Annotation { get; }
	}
}
