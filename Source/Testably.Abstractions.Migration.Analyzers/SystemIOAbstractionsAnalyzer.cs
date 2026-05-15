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

			start.RegisterOperationAction(
				ctx => AnalyzeInvocation(ctx, symbols),
				OperationKind.Invocation);

			start.RegisterOperationAction(
				ctx => AnalyzePropertyReference(ctx, symbols),
				OperationKind.PropertyReference);
		});
	}

	private static void AnalyzeObjectCreation(OperationAnalysisContext context, TestableIoSymbols symbols)
	{
		if (context.Operation is not IObjectCreationOperation creation)
		{
			return;
		}

		IMethodSymbol? constructor = creation.Constructor;
		if (constructor is null
		    || !SymbolEqualityComparer.Default.Equals(constructor.ContainingType, symbols.MockFileSystem))
		{
			return;
		}

		string? pattern = ClassifyMockFileSystemConstructor(constructor);
		if (pattern is not null)
		{
			Report(context, creation.Syntax.GetLocation(), pattern);
		}
	}

	private static void AnalyzeInvocation(OperationAnalysisContext context, TestableIoSymbols symbols)
	{
		if (context.Operation is not IInvocationOperation invocation)
		{
			return;
		}

		IMethodSymbol method = invocation.TargetMethod;
		INamedTypeSymbol? containingType = method.ContainingType;
		if (containingType is null)
		{
			return;
		}

		bool onMockFileSystem =
			SymbolEqualityComparer.Default.Equals(containingType, symbols.MockFileSystem);
		bool onAccessor =
			symbols.MockFileDataAccessor is { } accessor
			&& SymbolEqualityComparer.Default.Equals(containingType, accessor);
		if (!onMockFileSystem && !onAccessor)
		{
			return;
		}

		string? pattern = ClassifyAccessorMethod(method);
		if (pattern is not null)
		{
			Report(context, invocation.Syntax.GetLocation(), pattern);
		}
	}

	private static void AnalyzePropertyReference(OperationAnalysisContext context, TestableIoSymbols symbols)
	{
		if (symbols.MockFileData is null
		    || context.Operation is not IPropertyReferenceOperation propertyRef)
		{
			return;
		}

		if (!SymbolEqualityComparer.Default.Equals(propertyRef.Property.ContainingType, symbols.MockFileData))
		{
			return;
		}

		// Distinguish reads from writes via the parent operation: only the LHS of an
		// assignment counts as a write; compound shapes (return values, args, etc.) are
		// reads from this operation's perspective.
		bool isWrite = propertyRef.Parent is IAssignmentOperation assignment
		               && assignment.Target == propertyRef;

		// Skip property assignments inside an object-initializer expression. They are
		// part of MockFileData construction and belong to the AddFile or initializer
		// expansion rewrite in Phase 3.5. Reporting them here would double-flag a
		// single user-visible call site.
		if (isWrite
		    && propertyRef.Parent?.Parent is IObjectOrCollectionInitializerOperation)
		{
			return;
		}

		string pattern = isWrite
			? Patterns.MockFileDataPropertyWrite
			: Patterns.MockFileDataPropertyRead;

		Report(context, propertyRef.Syntax.GetLocation(), pattern);
	}

	private static string? ClassifyMockFileSystemConstructor(IMethodSymbol constructor)
	{
		ImmutableArray<IParameterSymbol> parameters = constructor.Parameters;
		return parameters.Length switch
		{
			0 => Patterns.MockFileSystemDefaultConstructor,
			1 when IsMockFileSystemOptions(parameters[0]) => Patterns.MockFileSystemOptionsConstructor,
			2 when IsFilesDictionary(parameters[0]) && parameters[1].Type.SpecialType == SpecialType.System_String
				=> Patterns.MockFileSystemFilesConstructor,
			2 when IsFilesDictionary(parameters[0]) && IsMockFileSystemOptions(parameters[1])
				=> Patterns.MockFileSystemFilesOptionsConstructor,
			_ => null,
		};
	}

	private static bool IsFilesDictionary(IParameterSymbol parameter)
		=> parameter.Type is INamedTypeSymbol named
		   && named.Name == "IDictionary"
		   && named.ContainingNamespace?.ToDisplayString() == "System.Collections.Generic";

	private static bool IsMockFileSystemOptions(IParameterSymbol parameter)
		=> parameter.Type is INamedTypeSymbol named
		   && named.Name == "MockFileSystemOptions"
		   && named.ContainingNamespace?.ToDisplayString() == TestableIoSymbols.TestingHelpersNamespace;

	private static string? ClassifyAccessorMethod(IMethodSymbol method) => method.Name switch
	{
		"AddFile" => Patterns.AccessorAddFile,
		"AddEmptyFile" => Patterns.AccessorAddEmptyFile,
		"AddDirectory" => Patterns.AccessorAddDirectory,
		"RemoveFile" => Patterns.AccessorRemoveFile,
		"MoveDirectory" => Patterns.AccessorMoveDirectory,
		"FileExists" => Patterns.AccessorFileExists,
		_ => null,
	};

	private static void Report(OperationAnalysisContext context, Location location, string pattern)
	{
		ImmutableDictionary<string, string?> properties =
			new Dictionary<string, string?> { [Patterns.Key] = pattern, }.ToImmutableDictionary();

		context.ReportDiagnostic(Diagnostic.Create(
			Rules.SystemIOAbstractionsRule,
			location,
			properties,
			messageArgs: null));
	}
}
