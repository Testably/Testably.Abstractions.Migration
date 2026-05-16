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

	protected override Task<Document?> FixAllAsync(
		FixAllContext fixAllContext,
		Document document,
		ImmutableArray<Diagnostic> diagnostics)
		=> MigrateDocumentAsync(document, diagnostics, fixAllContext.CancellationToken);

	/// <summary>
	///     Migrates every diagnostic in <paramref name="diagnostics" /> within
	///     <paramref name="document" /> in a single pass: annotate target nodes, dispatch
	///     each pattern's pure rewriter sequentially with re-acquired semantic models,
	///     then apply the using-directive change exactly once. Used both by the fix-all
	///     scopes (Document/Project/Solution) and by the single-diagnostic CodeAction —
	///     the per-diagnostic Fix re-discovers all sibling diagnostics in the document
	///     and routes through here so one click migrates the whole file (Mockolate-style).
	/// </summary>
	internal static async Task<Document?> MigrateDocumentAsync(
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

		// Source-order pass: annotate each diagnostic's target node so we can locate it
		// after prior rewrites have transformed its surroundings. The same diagnostic id
		// may legitimately appear on overlapping spans (e.g. nested initializers); a
		// fresh annotation per work item keeps them addressable independently.
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
			return document;
		}

		CompilationUnitSyntax annotatedCu = originalCu.ReplaceNodes(
			nodeToAnnotations.Keys,
			(original, _) => original.WithAdditionalAnnotations([..nodeToAnnotations[original]]));

		Document currentDoc = document.WithSyntaxRoot(annotatedCu);
		SystemIOAbstractionsCodeFixProvider.UsingChange maxUsingChange =
			SystemIOAbstractionsCodeFixProvider.UsingChange.None;

		foreach (WorkItem item in work)
		{
			SyntaxNode? curRoot = await currentDoc
				.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (curRoot is not CompilationUnitSyntax curCu)
			{
				continue;
			}

			// Annotations propagate through ReplaceNode/ReplaceNodes automatically. A null
			// here means a prior block-level rewrite (AddFile/FilesCtor initializer
			// expansion) replaced an enclosing statement and the annotated node was
			// dropped — skip it; the surrounding rewrite already covered it.
			SyntaxNode? currentTarget = curCu.GetAnnotatedNodes(item.Annotation).FirstOrDefault();
			if (currentTarget is null)
			{
				continue;
			}

			// Re-acquire the semantic model from the live document. Earlier rewrites
			// change the syntax tree; the original-document semantic model would return
			// stale or null symbol info for nodes in the new tree.
			SemanticModel? semanticModel = await currentDoc
				.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);

			CompilationUnitSyntax? rewritten = SystemIOAbstractionsCodeFixProvider.DispatchPure(
				item.Pattern, curCu, currentTarget, semanticModel, cancellationToken);
			if (rewritten is null)
			{
				continue;
			}

			currentDoc = currentDoc.WithSyntaxRoot(rewritten);

			SystemIOAbstractionsCodeFixProvider.UsingChange change =
				SystemIOAbstractionsCodeFixProvider.GetUsingChange(item.Pattern);
			if (change > maxUsingChange)
			{
				maxUsingChange = change;
			}
		}

		// Apply the using-directive change once, after every syntax rewrite. Doing it
		// here (instead of inside each pattern's rewrite) is the entire point of this
		// provider: it keeps the analyzer firing on remaining diagnostics during the
		// pass, and produces a single deterministic text change at the using line.
		if (maxUsingChange != SystemIOAbstractionsCodeFixProvider.UsingChange.None)
		{
			SyntaxNode? finalRoot = await currentDoc
				.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
			if (finalRoot is CompilationUnitSyntax finalCu)
			{
				finalCu = SystemIOAbstractionsCodeFixProvider.ApplyUsingChange(finalCu, maxUsingChange);
				currentDoc = currentDoc.WithSyntaxRoot(finalCu);
			}
		}

		return currentDoc;
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
