using System.Collections.Immutable;
using System.Composition;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CodeFixes;

namespace Testably.Abstractions.Migration.Analyzers;

/// <summary>
///     Code fix provider that rewrites <c>System.IO.Abstractions.TestingHelpers</c> usages
///     to <c>Testably.Abstractions.Testing</c> equivalents.
/// </summary>
[ExportCodeFixProvider(Microsoft.CodeAnalysis.LanguageNames.CSharp,
	Name = nameof(SystemIOAbstractionsCodeFixProvider))]
[Shared]
public class SystemIOAbstractionsCodeFixProvider : CodeFixProvider
{
	/// <inheritdoc cref="CodeFixProvider.FixableDiagnosticIds" />
	public override ImmutableArray<string> FixableDiagnosticIds
		=> ImmutableArray.Create(Rules.SystemIOAbstractionsRule.Id);

	/// <inheritdoc cref="CodeFixProvider.GetFixAllProvider" />
	public override FixAllProvider? GetFixAllProvider()
		=> WellKnownFixAllProviders.BatchFixer;

	/// <inheritdoc cref="CodeFixProvider.RegisterCodeFixesAsync(CodeFixContext)" />
	public override Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		// TODO: register a CodeAction that rewrites the offending node
		//       (typically a `new MockFileSystem(...)` invocation, a `new MockFileData(...)`
		//       initializer, or a member access on `IMockFileDataAccessor`) into the
		//       equivalent Testably.Abstractions.Testing call.
		return Task.CompletedTask;
	}
}
