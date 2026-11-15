using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Testably.Abstractions.Migration.Analyzers;
using Testably.Abstractions.Migration.Tests.Verifiers;
using Testably.Abstractions.Testing;

namespace Testably.Abstractions.Migration.Tests.Docs;

/// <summary>
///     Applies the code fix to the code of a documentation page, with the references of the code fix tests.
/// </summary>
internal static class CodeFixRunner
{
	private const int MaxIterations = 20;

	private static readonly CSharpParseOptions ParseOptions = new(LanguageVersion.Latest);
	private static readonly Lazy<Task<MetadataReference[]>> References = new(GetReferences);

	/// <summary>
	///     Applies the code fix for the diagnostics with the <paramref name="diagnosticId" /> to the
	///     <paramref name="source" /> until it offers no fix any more, and returns the resulting code.
	/// </summary>
	public static async Task<string> ApplyFixes(string source, string diagnosticId)
	{
		using AdhocWorkspace workspace = new();
		Document document = workspace.AddProject("Docs", LanguageNames.CSharp)
			.WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary))
			.WithParseOptions(ParseOptions)
			.WithMetadataReferences(await References.Value)
			.AddDocument("Docs.cs", source);
		for (int i = 0; i < MaxIterations; i++)
		{
			CodeAction? action = await GetFirstCodeAction(document, diagnosticId);
			if (action is null)
			{
				return (await document.GetTextAsync()).ToString();
			}

			ImmutableArray<CodeActionOperation> operations = await action.GetOperationsAsync(CancellationToken.None);
			document = operations.OfType<ApplyChangesOperation>().Single().ChangedSolution.GetDocument(document.Id)!;
		}

		throw new InvalidOperationException(
			$"The code fix still offered a fix for {diagnosticId} after {MaxIterations} iterations.");
	}

	/// <summary>
	///     Wraps the <paramref name="code" /> of a page into a compilable document: its <c>using</c> directives at the
	///     top, its statements in a method and its types after the class of that method.
	/// </summary>
	public static string Wrap(string code)
	{
		CompilationUnitSyntax root = CSharpSyntaxTree.ParseText(code, ParseOptions).GetCompilationUnitRoot();
		StringBuilder source = new();
		foreach (UsingDirectiveSyntax directive in root.Usings)
		{
			source.Append(directive.ToFullString());
		}

		source.Append("public class DocsExample\n{\npublic async global::System.Threading.Tasks.Task Run()\n{\n");
		foreach (MemberDeclarationSyntax statement in root.Members.Where(member => member is GlobalStatementSyntax))
		{
			source.Append(statement.ToFullString());
		}

		source.Append("\n}\n}\n");
		foreach (MemberDeclarationSyntax type in root.Members.Where(member => member is not GlobalStatementSyntax))
		{
			source.Append(type.ToFullString());
		}

		return source.ToString();
	}

	private static async Task<CodeAction?> GetFirstCodeAction(Document document, string diagnosticId)
	{
		Compilation compilation = (await document.Project.GetCompilationAsync())!;
		ImmutableArray<Diagnostic> diagnostics = await compilation
			.WithAnalyzers([new SystemIOAbstractionsAnalyzer(),])
			.GetAnalyzerDiagnosticsAsync();
		foreach (Diagnostic diagnostic in diagnostics
			         .Where(diagnostic => diagnostic.Id == diagnosticId)
			         .OrderBy(diagnostic => diagnostic.Location.SourceSpan.Start))
		{
			List<CodeAction> actions = [];
			CodeFixContext context = new(document, diagnostic,
				(action, _) => actions.Add(action), CancellationToken.None);
			await new SystemIOAbstractionsCodeFixProvider().RegisterCodeFixesAsync(context);
			if (actions.Count > 0)
			{
				return actions[0];
			}
		}

		return null;
	}

	private static async Task<MetadataReference[]> GetReferences()
	{
		ImmutableArray<MetadataReference> references = await CSharpVerifierHelper.ReferenceAssemblies
			.ResolveAsync(LanguageNames.CSharp, CancellationToken.None);
		return
		[
			..references,
			MetadataReference.CreateFromFile(typeof(MockFileSystem).Assembly.Location),
		];
	}
}
