using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Testably.Abstractions.Migration.Analyzers;

/// <summary>
///     Analyzer that flags <c>System.IO.Abstractions.TestingHelpers</c> usages
///     (<c>MockFileSystem</c>, <c>MockFileData</c>, <c>IMockFileDataAccessor</c>) that should be
///     migrated to <c>Testably.Abstractions.Testing.MockFileSystem</c>.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SystemIOAbstractionsAnalyzer : DiagnosticAnalyzer
{
	/// <inheritdoc cref="DiagnosticAnalyzer.SupportedDiagnostics" />
	public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics
		=> ImmutableArray.Create(Rules.SystemIOAbstractionsRule);

	/// <inheritdoc cref="DiagnosticAnalyzer.Initialize(AnalysisContext)" />
	public override void Initialize(AnalysisContext context)
	{
		context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
		context.EnableConcurrentExecution();

		// TODO: register the syntax/symbol/operation actions that detect
		//       System.IO.Abstractions.TestingHelpers usage and report
		//       Rules.SystemIOAbstractionsRule.
	}
}
