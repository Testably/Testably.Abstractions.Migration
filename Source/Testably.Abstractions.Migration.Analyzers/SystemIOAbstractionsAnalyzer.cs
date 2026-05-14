using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Testably.Abstractions.Migration.Analyzers.Common;

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

		context.RegisterCompilationStartAction(start =>
		{
			TestableIoSymbols? symbols = TestableIoSymbols.TryGetFrom(start.Compilation);
			if (symbols is null)
			{
				return;
			}

			start.RegisterOperationAction(
				ctx => AnalyzeObjectCreation(ctx, symbols),
				OperationKind.ObjectCreation);
		});
	}

	private static void AnalyzeObjectCreation(OperationAnalysisContext context, TestableIoSymbols symbols)
	{
		if (context.Operation is not IObjectCreationOperation creation)
		{
			return;
		}

		INamedTypeSymbol? type = creation.Constructor?.ContainingType;
		if (type is null)
		{
			return;
		}

		// Phase 1 only handles the parameterless overload. The other three
		// MockFileSystem constructors are addressed in Phase 2.
		if (SymbolEqualityComparer.Default.Equals(type, symbols.MockFileSystem)
		    && creation.Arguments.Length == 0)
		{
			Report(context, creation, Patterns.MockFileSystemDefaultConstructor);
		}
	}

	private static void Report(OperationAnalysisContext context, IObjectCreationOperation creation, string pattern)
	{
		ImmutableDictionary<string, string?> properties =
			new Dictionary<string, string?> { [Patterns.Key] = pattern, }.ToImmutableDictionary();

		context.ReportDiagnostic(Diagnostic.Create(
			Rules.SystemIOAbstractionsRule,
			creation.Syntax.GetLocation(),
			properties,
			messageArgs: null));
	}
}
