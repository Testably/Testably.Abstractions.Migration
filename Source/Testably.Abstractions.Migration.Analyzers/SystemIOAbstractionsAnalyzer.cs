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

			start.RegisterSymbolAction(
				ctx => AnalyzeNamedTypeDeclaration(ctx, symbols),
				SymbolKind.NamedType);
		});
	}

	private static void AnalyzeNamedTypeDeclaration(SymbolAnalysisContext context, TestableIoSymbols symbols)
	{
		if (context.Symbol is not INamedTypeSymbol named || named.TypeKind != TypeKind.Class)
		{
			return;
		}

		// Skip the framework types themselves — only user-defined subclasses need migration.
		if (SymbolEqualityComparer.Default.Equals(named, symbols.MockFileSystem)
		    || (symbols.MockFileData is { } mockFileData
		        && SymbolEqualityComparer.Default.Equals(named, mockFileData)))
		{
			return;
		}

		string? pattern = ClassifySubclass(named, symbols);
		if (pattern is null)
		{
			return;
		}

		// Locations covers every partial declaration; reporting on each surfaces all of
		// them to the user. The set is usually a single location.
		foreach (Location location in named.Locations)
		{
			Report(context, location, pattern);
		}
	}

	private static string? ClassifySubclass(INamedTypeSymbol named, TestableIoSymbols symbols)
	{
		for (INamedTypeSymbol? baseType = named.BaseType; baseType is not null; baseType = baseType.BaseType)
		{
			if (SymbolEqualityComparer.Default.Equals(baseType, symbols.MockFileSystem))
			{
				return Patterns.MockFileSystemSubclass;
			}

			if (symbols.MockFileData is { } mockFileData
			    && SymbolEqualityComparer.Default.Equals(baseType, mockFileData))
			{
				return Patterns.MockFileDataSubclass;
			}
		}

		return null;
	}

	private static void AnalyzeObjectCreation(OperationAnalysisContext context, TestableIoSymbols symbols)
	{
		if (context.Operation is not IObjectCreationOperation creation)
		{
			return;
		}

		IMethodSymbol? constructor = creation.Constructor;
		if (constructor is null)
		{
			return;
		}

		if (SymbolEqualityComparer.Default.Equals(constructor.ContainingType, symbols.MockFileSystem))
		{
			string? pattern = ClassifyMockFileSystemConstructor(constructor);
			if (pattern is not null)
			{
				Report(context, creation.Syntax.GetLocation(), pattern);
			}

			return;
		}

		// Phase 4a manual-review: MockFileVersionInfo has no Testably equivalent.
		// Flag the construction site so the user can locate every fixture that seeded
		// version metadata; the code-fix provider intentionally registers no rewrite.
		if (symbols.MockFileVersionInfo is { } mockFileVersionInfo
		    && SymbolEqualityComparer.Default.Equals(constructor.ContainingType, mockFileVersionInfo))
		{
			Report(context, creation.Syntax.GetLocation(), Patterns.MockFileVersionInfoConstructor);
			return;
		}

		// Phase 4b manual-review: MockFileData copy constructor. Cloning semantics differ
		// across libraries and Testably has no equivalent. We only fire for the explicit
		// 1-parameter MockFileData overload — the encoding/byte/text ctors are part of
		// existing AddFile expansion (Phases 2/3.5) and must keep their current pattern.
		if (symbols.MockFileData is { } mockFileData
		    && SymbolEqualityComparer.Default.Equals(constructor.ContainingType, mockFileData)
		    && constructor.Parameters.Length == 1
		    && SymbolEqualityComparer.Default.Equals(constructor.Parameters[0].Type, mockFileData))
		{
			Report(context, creation.Syntax.GetLocation(), Patterns.MockFileDataCopyConstructor);
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

		// Phase 4a manual-review properties have no Testably equivalent and no other
		// pass picks them up (there is no AddFile expansion for unsupported initializer
		// properties), so they must be reported here — including inside object
		// initializers — or the lossy call site would be invisible.
		string? manualReviewPattern = ClassifyManualReviewProperty(propertyRef.Property.Name);
		if (manualReviewPattern is not null)
		{
			Report(context, propertyRef.Syntax.GetLocation(), manualReviewPattern);
			return;
		}

		// Skip migratable property assignments inside an object-initializer expression.
		// They are part of MockFileData construction and belong to the AddFile or
		// initializer expansion rewrite in Phase 3.5. Reporting them here would
		// double-flag a single user-visible call site.
		if (isWrite
		    && propertyRef.Parent?.Parent is IObjectOrCollectionInitializerOperation)
		{
			return;
		}

		// Phase 4c: separate captured-reference accesses from one-shot
		// `fs.GetFile(path).Prop` chains. The code-fix's rewrite only applies to the
		// one-shot shape; everything else needs flow analysis to retarget safely, so
		// give it a discriminating pattern id and let the fix dispatch fall through.
		bool isOneShotGetFile = propertyRef.Instance is IInvocationOperation invocation
		                        && invocation.TargetMethod.Name == "GetFile"
		                        && invocation.Arguments.Length == 1;

		string pattern = isOneShotGetFile
			? (isWrite ? Patterns.MockFileDataPropertyWrite : Patterns.MockFileDataPropertyRead)
			: (isWrite ? Patterns.MockFileDataCapturedReferenceWrite : Patterns.MockFileDataCapturedReferenceRead);

		Report(context, propertyRef.Syntax.GetLocation(), pattern);
	}

	private static string? ClassifyManualReviewProperty(string propertyName) => propertyName switch
	{
		"AccessControl" => Patterns.MockFileDataAccessControl,
		"AllowedFileShare" => Patterns.MockFileDataAllowedFileShare,
		"UnixMode" => Patterns.MockFileDataUnixMode,
		_ => null,
	};

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
		=> context.ReportDiagnostic(BuildDiagnostic(location, pattern));

	private static void Report(SymbolAnalysisContext context, Location location, string pattern)
		=> context.ReportDiagnostic(BuildDiagnostic(location, pattern));

	private static Diagnostic BuildDiagnostic(Location location, string pattern)
	{
		ImmutableDictionary<string, string?> properties =
			new Dictionary<string, string?> { [Patterns.Key] = pattern, }.ToImmutableDictionary();

		return Diagnostic.Create(
			Rules.SystemIOAbstractionsRule,
			location,
			properties,
			messageArgs: null);
	}
}
