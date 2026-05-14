using System.Collections.Immutable;
using System.Composition;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Testably.Abstractions.Migration.Analyzers;

/// <summary>
///     Code fix provider that rewrites <c>System.IO.Abstractions.TestingHelpers</c> usages
///     to <c>Testably.Abstractions.Testing</c> equivalents.
/// </summary>
[ExportCodeFixProvider(LanguageNames.CSharp, Name = nameof(SystemIOAbstractionsCodeFixProvider))]
[Shared]
public class SystemIOAbstractionsCodeFixProvider : CodeFixProvider
{
	private const string TestablyTestingNamespace = "Testably.Abstractions.Testing";
	private const string TestingHelpersNamespace = "System.IO.Abstractions.TestingHelpers";

	/// <inheritdoc cref="CodeFixProvider.FixableDiagnosticIds" />
	public override ImmutableArray<string> FixableDiagnosticIds
		=> ImmutableArray.Create(Rules.SystemIOAbstractionsRule.Id);

	/// <inheritdoc cref="CodeFixProvider.GetFixAllProvider" />
	public override FixAllProvider? GetFixAllProvider()
		=> WellKnownFixAllProviders.BatchFixer;

	/// <inheritdoc cref="CodeFixProvider.RegisterCodeFixesAsync(CodeFixContext)" />
	public override Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		foreach (Diagnostic diagnostic in context.Diagnostics)
		{
			if (!diagnostic.Properties.TryGetValue(Patterns.Key, out string? pattern) || pattern is null)
			{
				continue;
			}

			switch (pattern)
			{
				case Patterns.MockFileSystemDefaultConstructor:
					context.RegisterCodeFix(
						CodeAction.Create(
							Resources.TestablyAbstractionsMigration001CodeFixTitle,
							ct => RewriteUsingsAsync(context.Document, ct),
							equivalenceKey: Patterns.MockFileSystemDefaultConstructor),
						diagnostic);
					break;
			}
		}

		return Task.CompletedTask;
	}

	private static async Task<Document> RewriteUsingsAsync(Document document, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
		{
			return document;
		}

		UsingDirectiveSyntax? testingHelpersUsing = compilationUnit.Usings
			.FirstOrDefault(u => u.Name?.ToString() == TestingHelpersNamespace);
		bool hasTestablyUsing = compilationUnit.Usings
			.Any(u => u.Name?.ToString() == TestablyTestingNamespace);

		if (testingHelpersUsing is not null)
		{
			// Replace in place so the rewrite inherits the original trivia (line endings,
			// indentation, leading comments) rather than depending on a fresh syntax token.
			if (hasTestablyUsing)
			{
				compilationUnit =
					compilationUnit.RemoveNode(testingHelpersUsing, SyntaxRemoveOptions.KeepNoTrivia)
					?? compilationUnit;
			}
			else
			{
				NameSyntax newName = SyntaxFactory.ParseName(TestablyTestingNamespace);
				UsingDirectiveSyntax replacement = testingHelpersUsing.WithName(newName);
				compilationUnit = compilationUnit.ReplaceNode(testingHelpersUsing, replacement);
			}
		}
		else if (!hasTestablyUsing)
		{
			compilationUnit = AppendUsing(compilationUnit, TestablyTestingNamespace);
		}

		return document.WithSyntaxRoot(compilationUnit);
	}

	private static CompilationUnitSyntax AppendUsing(CompilationUnitSyntax compilationUnit, string namespaceName)
	{
		UsingDirectiveSyntax usingDirective = BuildUsingDirective(compilationUnit, namespaceName);
		return compilationUnit.AddUsings(usingDirective);
	}

	private static UsingDirectiveSyntax BuildUsingDirective(CompilationUnitSyntax compilationUnit, string namespaceName)
	{
		NameSyntax name = SyntaxFactory.ParseName(namespaceName);

		UsingDirectiveSyntax? template = compilationUnit.Usings.LastOrDefault();
		if (template is not null)
		{
			SyntaxToken semicolon = SyntaxFactory.Token(SyntaxKind.SemicolonToken)
				.WithTriviaFrom(template.SemicolonToken);
			return SyntaxFactory.UsingDirective(name).WithSemicolonToken(semicolon);
		}

		return SyntaxFactory.UsingDirective(name);
	}
}
