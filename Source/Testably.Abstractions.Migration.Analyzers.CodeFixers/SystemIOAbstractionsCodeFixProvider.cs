using System.Collections.Generic;
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
	public override async Task RegisterCodeFixesAsync(CodeFixContext context)
	{
		SyntaxNode? root = await context.Document
			.GetSyntaxRootAsync(context.CancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax)
		{
			return;
		}

		foreach (Diagnostic diagnostic in context.Diagnostics)
		{
			if (!diagnostic.Properties.TryGetValue(Patterns.Key, out string? pattern) || pattern is null)
			{
				continue;
			}

			SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
			if (node is null)
			{
				continue;
			}

			switch (pattern)
			{
				case Patterns.MockFileSystemDefaultConstructor:
					TryRegisterDefaultCtorFix(context, diagnostic, node);
					break;
				case Patterns.MockFileSystemOptionsConstructor:
					TryRegisterOptionsCtorFix(context, diagnostic, node);
					break;
				case Patterns.AccessorAddDirectory:
				case Patterns.AccessorRemoveFile:
				case Patterns.AccessorMoveDirectory:
				case Patterns.AccessorFileExists:
				case Patterns.AccessorAddEmptyFile:
					await TryRegisterAccessorMethodFixAsync(context, diagnostic, node, pattern)
						.ConfigureAwait(false);
					break;
				case Patterns.AccessorAddFile:
					await TryRegisterAddFileFixAsync(context, diagnostic, node).ConfigureAwait(false);
					break;
				case Patterns.MockFileSystemFilesConstructor:
				case Patterns.MockFileSystemFilesOptionsConstructor:
					await TryRegisterFilesCtorFixAsync(context, diagnostic, node, pattern)
						.ConfigureAwait(false);
					break;
				case Patterns.MockFileDataPropertyRead:
					TryRegisterPropertyReadFix(context, diagnostic, node);
					break;
				case Patterns.MockFileDataPropertyWrite:
					TryRegisterPropertyWriteFix(context, diagnostic, node);
					break;
			}
		}
	}

	// ── Pattern: MockFileSystem.ctor() ───────────────────────────────────────

	private static void TryRegisterDefaultCtorFix(CodeFixContext context, Diagnostic diagnostic, SyntaxNode node)
	{
		// The fix only adjusts using directives. If the construction is alias- or
		// fully-qualified (`new TestableIo.MockFileSystem()`), the type identifier
		// stays bound to TestableIO regardless of the using swap, so the rewrite
		// would produce code that still targets the old library.
		BaseObjectCreationExpressionSyntax? creation =
			node.FirstAncestorOrSelf<BaseObjectCreationExpressionSyntax>();
		if (creation is null || !HasUnqualifiedMockFileSystemTypeName(creation))
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				Resources.TestablyM001CodeFixTitle,
				ct => RewriteUsingsAsync(context.Document, ct),
				equivalenceKey: Patterns.MockFileSystemDefaultConstructor),
			diagnostic);
	}

	private static bool HasUnqualifiedMockFileSystemTypeName(BaseObjectCreationExpressionSyntax creation)
		=> creation switch
		{
			ObjectCreationExpressionSyntax { Type: IdentifierNameSyntax, } => true,
			ImplicitObjectCreationExpressionSyntax implicitCreation
				=> HasUnqualifiedImplicitTargetType(implicitCreation),
			_ => false,
		};

	private static bool HasUnqualifiedImplicitTargetType(ImplicitObjectCreationExpressionSyntax implicitCreation)
	{
		// `new()` is target-typed: the contextual type is what the compiler binds to.
		// The using-swap fix only retargets unqualified `MockFileSystem` identifiers,
		// so an enclosing fully-qualified or alias-qualified target type
		// (e.g. `System.IO.Abstractions.TestingHelpers.MockFileSystem fs = new();`)
		// would keep the construction bound to TestableIO regardless of the swap.
		//
		// Only support contexts where the syntactic target type annotation is itself
		// an unqualified IdentifierNameSyntax. Other target-typing contexts (parameters,
		// returns, assignments to non-local LHS, casts) fall through to manual review.
		return implicitCreation.Parent switch
		{
			EqualsValueClauseSyntax
			{
				Parent: VariableDeclaratorSyntax
				{
					Parent: VariableDeclarationSyntax { Type: IdentifierNameSyntax, },
				},
			} => true,
			_ => false,
		};
	}

	private static async Task<Document> RewriteUsingsAsync(Document document, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
		{
			return document;
		}

		compilationUnit = SwapToTestablyUsing(compilationUnit);
		return document.WithSyntaxRoot(compilationUnit);
	}

	// ── Pattern: MockFileSystem.ctor(options) ────────────────────────────────

	private static void TryRegisterOptionsCtorFix(CodeFixContext context, Diagnostic diagnostic, SyntaxNode node)
	{
		ObjectCreationExpressionSyntax? creation = node.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
		if (creation?.ArgumentList is not { Arguments.Count: 1, } argList
		    || !HasUnqualifiedMockFileSystemTypeName(creation))
		{
			return;
		}

		if (TryBuildTestablyOptionsArgList(argList.Arguments[0].Expression) is null)
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				Resources.TestablyM001CodeFixTitle,
				ct => ApplyOptionsCtorRewriteAsync(context.Document, diagnostic, ct),
				equivalenceKey: Patterns.MockFileSystemOptionsConstructor),
			diagnostic);
	}

	private static async Task<Document> ApplyOptionsCtorRewriteAsync(
		Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
		{
			return document;
		}

		SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
		ObjectCreationExpressionSyntax? creation = node?.FirstAncestorOrSelf<ObjectCreationExpressionSyntax>();
		if (creation?.ArgumentList is not { Arguments.Count: 1, } argList)
		{
			return document;
		}

		ArgumentListSyntax? newArgList = TryBuildTestablyOptionsArgList(argList.Arguments[0].Expression);
		if (newArgList is null)
		{
			return document;
		}

		ObjectCreationExpressionSyntax newCreation =
			creation.WithArgumentList(newArgList.WithTriviaFrom(argList));
		compilationUnit = compilationUnit.ReplaceNode(creation, newCreation);
		compilationUnit = SwapToTestablyUsing(compilationUnit);

		return document.WithSyntaxRoot(compilationUnit);
	}

	private static ArgumentListSyntax? TryBuildTestablyOptionsArgList(ExpressionSyntax optionsExpression)
	{
		InitializerExpressionSyntax? initializer = optionsExpression switch
		{
			ObjectCreationExpressionSyntax oc => oc.Initializer,
			ImplicitObjectCreationExpressionSyntax ic => ic.Initializer,
			_ => null,
		};

		bool isCreation =
			optionsExpression is ObjectCreationExpressionSyntax
			or ImplicitObjectCreationExpressionSyntax;
		if (!isCreation)
		{
			return null;
		}

		if (initializer is null || initializer.Expressions.Count == 0)
		{
			return SyntaxFactory.ArgumentList();
		}

		ExpressionSyntax? currentDirectoryRhs = null;
		foreach (ExpressionSyntax expression in initializer.Expressions)
		{
			if (expression is not AssignmentExpressionSyntax assignment
			    || assignment.Left is not IdentifierNameSyntax property)
			{
				return null;
			}

			switch (property.Identifier.Text)
			{
				case "CurrentDirectory":
					currentDirectoryRhs = assignment.Right;
					break;
				default:
					return null;
			}
		}

		if (currentDirectoryRhs is null)
		{
			return SyntaxFactory.ArgumentList();
		}

		return SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
			SyntaxFactory.Argument(BuildUseCurrentDirectoryLambda(currentDirectoryRhs))));
	}

	private static SimpleLambdaExpressionSyntax BuildUseCurrentDirectoryLambda(ExpressionSyntax currentDirectory)
	{
		// Avoid shadowing identifiers used inside the captured `currentDirectory`
		// expression. `new MockFileSystemOptions { CurrentDirectory = o }` must not
		// rewrite to `o => o.UseCurrentDirectory(o)`.
		string parameterName = PickFreshLambdaParameterName(currentDirectory);
		return SyntaxFactory.SimpleLambdaExpression(
			SyntaxFactory.Parameter(SyntaxFactory.Identifier(parameterName)),
			SyntaxFactory.InvocationExpression(
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					SyntaxFactory.IdentifierName(parameterName),
					SyntaxFactory.IdentifierName("UseCurrentDirectory")),
				SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
					SyntaxFactory.Argument(currentDirectory.WithoutTrivia())))));
	}

	private static string PickFreshLambdaParameterName(ExpressionSyntax embedded)
	{
		HashSet<string> used = new(embedded.DescendantNodesAndSelf()
			.OfType<IdentifierNameSyntax>()
			.Select(id => id.Identifier.Text));

		string[] candidates = ["o", "options", "opt", "builder",];
		foreach (string candidate in candidates)
		{
			if (!used.Contains(candidate))
			{
				return candidate;
			}
		}

		for (int i = 1;; i++)
		{
			string n = $"o{i}";
			if (!used.Contains(n))
			{
				return n;
			}
		}
	}

	// ── Pattern: 1:1 IMockFileDataAccessor method rewrites ───────────────────

	private static async Task TryRegisterAccessorMethodFixAsync(
		CodeFixContext context, Diagnostic diagnostic, SyntaxNode node, string pattern)
	{
		InvocationExpressionSyntax? invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
		if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
		{
			return;
		}

		// The rewrite emits `<receiver>.File.X(...)` / `<receiver>.Directory.X(...)`. Those
		// members live on the concrete `MockFileSystem` class, not on `IMockFileDataAccessor` —
		// so the fix must not run when the user calls through the interface.
		SemanticModel? semanticModel = await context.Document
			.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
		if (semanticModel is null || !IsConcreteMockFileSystemReceiver(memberAccess.Expression, semanticModel))
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				Resources.TestablyM001CodeFixTitle,
				ct => ApplyAccessorMethodRewriteAsync(context.Document, diagnostic, pattern, ct),
				equivalenceKey: pattern),
			diagnostic);
	}

	private static bool IsConcreteMockFileSystemReceiver(ExpressionSyntax receiver, SemanticModel semanticModel)
	{
		ITypeSymbol? type = semanticModel.GetTypeInfo(receiver).Type;
		return type is INamedTypeSymbol named
		       && named.Name == "MockFileSystem"
		       && named.ContainingNamespace?.ToDisplayString()
		       == "System.IO.Abstractions.TestingHelpers";
	}

	private static async Task<Document> ApplyAccessorMethodRewriteAsync(
		Document document, Diagnostic diagnostic, string pattern, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
		{
			return document;
		}

		SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
		InvocationExpressionSyntax? invocation = node?.FirstAncestorOrSelf<InvocationExpressionSyntax>();
		if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess)
		{
			return document;
		}

		ExpressionSyntax newExpression = pattern switch
		{
			Patterns.AccessorAddDirectory => BuildSubReceiverInvocation(
				invocation, memberAccess, "Directory", "CreateDirectory", argCountToKeep: int.MaxValue),
			Patterns.AccessorRemoveFile => BuildSubReceiverInvocation(
				invocation, memberAccess, "File", "Delete", argCountToKeep: 1),
			Patterns.AccessorMoveDirectory => BuildSubReceiverInvocation(
				invocation, memberAccess, "Directory", "Move", argCountToKeep: int.MaxValue),
			Patterns.AccessorFileExists => BuildSubReceiverInvocation(
				invocation, memberAccess, "File", "Exists", argCountToKeep: int.MaxValue),
			Patterns.AccessorAddEmptyFile => BuildAddEmptyFileInvocation(invocation, memberAccess),
			_ => invocation,
		};

		if (ReferenceEquals(newExpression, invocation))
		{
			return document;
		}

		compilationUnit = compilationUnit.ReplaceNode(invocation, newExpression.WithTriviaFrom(invocation));
		return document.WithSyntaxRoot(compilationUnit);
	}

	private static InvocationExpressionSyntax BuildSubReceiverInvocation(
		InvocationExpressionSyntax original,
		MemberAccessExpressionSyntax memberAccess,
		string subReceiver,
		string newMethod,
		int argCountToKeep)
	{
		MemberAccessExpressionSyntax newAccess = SyntaxFactory.MemberAccessExpression(
			SyntaxKind.SimpleMemberAccessExpression,
			SyntaxFactory.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				memberAccess.Expression,
				SyntaxFactory.IdentifierName(subReceiver)),
			SyntaxFactory.IdentifierName(newMethod));

		SeparatedSyntaxList<ArgumentSyntax> args = original.ArgumentList.Arguments;
		int keep = argCountToKeep < args.Count ? argCountToKeep : args.Count;
		// Strip the NameColon: TestableIO and Testably use different parameter names
		// (e.g. `MoveDirectory(sourcePath:, destPath:)` vs `Directory.Move(sourceDirName:,
		// destDirName:)`). Positional binding is the only safe form across the swap.
		SeparatedSyntaxList<ArgumentSyntax> normalized = SyntaxFactory.SeparatedList(
			args.Take(keep).Select(arg => arg.WithNameColon(null)));

		return SyntaxFactory.InvocationExpression(newAccess, original.ArgumentList.WithArguments(normalized));
	}

	private static InvocationExpressionSyntax BuildAddEmptyFileInvocation(
		InvocationExpressionSyntax original, MemberAccessExpressionSyntax memberAccess)
	{
		InvocationExpressionSyntax createCall = BuildSubReceiverInvocation(
			original, memberAccess, "File", "Create", argCountToKeep: int.MaxValue);

		return SyntaxFactory.InvocationExpression(
			SyntaxFactory.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				createCall,
				SyntaxFactory.IdentifierName("Dispose")));
	}

	// ── Pattern: accessor.AddFile ────────────────────────────────────────────

	private static async Task TryRegisterAddFileFixAsync(
		CodeFixContext context, Diagnostic diagnostic, SyntaxNode node)
	{
		InvocationExpressionSyntax? invocation = node.FirstAncestorOrSelf<InvocationExpressionSyntax>();
		if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess
		    || invocation.ArgumentList.Arguments.Count < 2)
		{
			return;
		}

		SemanticModel? semanticModel = await context.Document
			.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
		if (semanticModel is null
		    || !IsConcreteMockFileSystemReceiver(memberAccess.Expression, semanticModel))
		{
			return;
		}

		MockFileDataShape? shape = ClassifyMockFileDataExpression(
			invocation.ArgumentList.Arguments[1].Expression,
			semanticModel,
			context.CancellationToken);
		if (shape is null)
		{
			return;
		}

		// When initializer properties are present, the rewrite has to insert follow-up
		// statements alongside the original — only supported when the AddFile call is
		// already at the top of a statement block.
		if (shape.Value.InitializerProperties.Count > 0
		    && (invocation.Parent is not ExpressionStatementSyntax
		        || invocation.Parent.Parent is not BlockSyntax))
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				Resources.TestablyM001CodeFixTitle,
				ct => ApplyAddFileRewriteAsync(context.Document, diagnostic, ct),
				equivalenceKey: Patterns.AccessorAddFile),
			diagnostic);
	}

	private static async Task<Document> ApplyAddFileRewriteAsync(
		Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
		{
			return document;
		}

		SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		if (semanticModel is null)
		{
			return document;
		}

		SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
		InvocationExpressionSyntax? invocation = node?.FirstAncestorOrSelf<InvocationExpressionSyntax>();
		if (invocation?.Expression is not MemberAccessExpressionSyntax memberAccess
		    || invocation.ArgumentList.Arguments.Count < 2)
		{
			return document;
		}

		ArgumentSyntax pathArg = invocation.ArgumentList.Arguments[0];
		MockFileDataShape? shape = ClassifyMockFileDataExpression(
			invocation.ArgumentList.Arguments[1].Expression, semanticModel, cancellationToken);
		if (shape is null)
		{
			return document;
		}

		InvocationExpressionSyntax rewritten = BuildAddFileReplacement(memberAccess, pathArg, shape.Value);

		// Without initializer properties, the rewrite is a 1:1 invocation swap.
		if (shape.Value.InitializerProperties.Count == 0)
		{
			compilationUnit = compilationUnit.ReplaceNode(invocation, rewritten.WithTriviaFrom(invocation));
			return document.WithSyntaxRoot(compilationUnit);
		}

		// With initializer properties, the expansion requires inserting follow-up
		// statements after the original statement. Bail unless the AddFile call is a
		// top-level expression statement inside a block — anything else would have
		// nowhere to place the follow-ups.
		if (invocation.Parent is not ExpressionStatementSyntax originalStatement
		    || originalStatement.Parent is not BlockSyntax block)
		{
			return document;
		}

		(string indentation, string newline) = DetectIndentationAndNewline(originalStatement);
		string receiverText = memberAccess.Expression.ToString().Trim();
		string pathText = pathArg.Expression.ToString().Trim();

		// NormalizeWhitespace resets the synthesized invocation's separators to standard
		// formatting (`, ` between args). Without it the SeparatedList we built emits
		// commas without spaces, which the test framework's formatter would otherwise
		// repair but ParseStatement preserves verbatim.
		StatementSyntax newPrimary = SyntaxFactory.ParseStatement(
				$"{rewritten.NormalizeWhitespace()};")
			.WithLeadingTrivia(originalStatement.GetLeadingTrivia())
			.WithTrailingTrivia(originalStatement.GetTrailingTrivia());

		List<StatementSyntax> followUps = shape.Value.InitializerProperties
			.Select(prop => SyntaxFactory.ParseStatement(
				$"{indentation}{receiverText}.File.{MapMockFileDataInitializerProperty(prop.Key)}({pathText}, {prop.Value.ToString().Trim()});{newline}"))
			.ToList();

		SyntaxList<StatementSyntax> updatedStatements = block.Statements;
		int index = updatedStatements.IndexOf(originalStatement);
		updatedStatements = updatedStatements.Replace(originalStatement, newPrimary);
		updatedStatements = updatedStatements.InsertRange(index + 1, followUps);

		BlockSyntax newBlock = block.WithStatements(updatedStatements);
		compilationUnit = compilationUnit.ReplaceNode(block, newBlock);
		return document.WithSyntaxRoot(compilationUnit);
	}

	private static InvocationExpressionSyntax BuildAddFileReplacement(
		MemberAccessExpressionSyntax memberAccess, ArgumentSyntax pathArg, MockFileDataShape shape)
	{
		string newMethod = shape.Kind switch
		{
			MockFileDataKind.Text => "WriteAllText",
			MockFileDataKind.Bytes => "WriteAllBytes",
			_ => "WriteAllText",
		};

		MemberAccessExpressionSyntax newAccess = SyntaxFactory.MemberAccessExpression(
			SyntaxKind.SimpleMemberAccessExpression,
			SyntaxFactory.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				memberAccess.Expression,
				SyntaxFactory.IdentifierName("File")),
			SyntaxFactory.IdentifierName(newMethod));

		// Strip NameColon on the path arg — AddFile uses `path:` but File.WriteAllText
		// uses the same name today, however the WriteAllBytes overload may differ in
		// future updates. Positional binding is the safe baseline.
		SeparatedSyntaxList<ArgumentSyntax> args = SyntaxFactory.SeparatedList(new[]
		{
			pathArg.WithNameColon(null).WithoutTrivia(),
			SyntaxFactory.Argument(shape.PrimaryContent.WithoutTrivia()),
		});
		if (shape.SecondaryContent is not null)
		{
			args = args.Add(SyntaxFactory.Argument(shape.SecondaryContent.WithoutTrivia()));
		}

		return SyntaxFactory.InvocationExpression(newAccess, SyntaxFactory.ArgumentList(args));
	}

	private static MockFileDataShape? ClassifyMockFileDataExpression(
		ExpressionSyntax expression, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		if (!TryExtractCreationParts(expression, out ArgumentListSyntax? argList,
			    out InitializerExpressionSyntax? initializer))
		{
			return null;
		}

		List<KeyValuePair<string, ExpressionSyntax>>? initializerProps =
			TryParseInitializerProperties(initializer);
		if (initializerProps is null)
		{
			return null;
		}

		if (!TryGetMockFileDataConstructor(expression, semanticModel, cancellationToken,
			    out IMethodSymbol? ctor))
		{
			return null;
		}

		return TryMatchMockFileDataOverload(ctor!.Parameters, argList!.Arguments, initializerProps);
	}

	private static bool TryExtractCreationParts(
		ExpressionSyntax expression,
		out ArgumentListSyntax? argList,
		out InitializerExpressionSyntax? initializer)
	{
		(argList, initializer) = expression switch
		{
			ObjectCreationExpressionSyntax oc => (oc.ArgumentList, oc.Initializer),
			ImplicitObjectCreationExpressionSyntax ic => (ic.ArgumentList, ic.Initializer),
			_ => (null, null),
		};
		return argList is not null;
	}

	private static List<KeyValuePair<string, ExpressionSyntax>>? TryParseInitializerProperties(
		InitializerExpressionSyntax? initializer)
	{
		List<KeyValuePair<string, ExpressionSyntax>> result = [];
		if (initializer is null || initializer.Expressions.Count == 0)
		{
			return result;
		}

		foreach (ExpressionSyntax entry in initializer.Expressions)
		{
			if (entry is not AssignmentExpressionSyntax assignment
			    || assignment.Left is not IdentifierNameSyntax id
			    || MapMockFileDataInitializerProperty(id.Identifier.Text) is null)
			{
				return null;
			}

			result.Add(new KeyValuePair<string, ExpressionSyntax>(id.Identifier.Text, assignment.Right));
		}

		return result;
	}

	private static bool TryGetMockFileDataConstructor(
		ExpressionSyntax expression,
		SemanticModel semanticModel,
		CancellationToken cancellationToken,
		out IMethodSymbol? ctor)
	{
		ctor = null;
		SymbolInfo info = semanticModel.GetSymbolInfo(expression, cancellationToken);
		if (info.Symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor, } found
		    || found.ContainingType is not { Name: "MockFileData", } containing
		    || containing.ContainingNamespace?.ToDisplayString()
		    != "System.IO.Abstractions.TestingHelpers")
		{
			return false;
		}

		ctor = found;
		return true;
	}

	private static MockFileDataShape? TryMatchMockFileDataOverload(
		ImmutableArray<IParameterSymbol> parameters,
		SeparatedSyntaxList<ArgumentSyntax> arguments,
		List<KeyValuePair<string, ExpressionSyntax>> initializerProps)
	{
		if (parameters.Length == 1
		    && parameters[0].Type.SpecialType == SpecialType.System_String
		    && arguments.Count == 1)
		{
			return new MockFileDataShape(
				MockFileDataKind.Text, arguments[0].Expression, null, initializerProps);
		}

		if (parameters.Length == 2
		    && parameters[0].Type.SpecialType == SpecialType.System_String
		    && parameters[1].Type is { Name: "Encoding", ContainingNamespace.Name: "Text", }
		    && arguments.Count == 2)
		{
			return new MockFileDataShape(
				MockFileDataKind.Text,
				arguments[0].Expression,
				arguments[1].Expression,
				initializerProps);
		}

		if (parameters.Length == 1
		    && parameters[0].Type is IArrayTypeSymbol { ElementType.SpecialType: SpecialType.System_Byte, }
		    && arguments.Count == 1)
		{
			return new MockFileDataShape(
				MockFileDataKind.Bytes, arguments[0].Expression, null, initializerProps);
		}

		// MockFileData(MockFileData template) and any other overload are out of scope here.
		return null;
	}

	private static string? MapMockFileDataInitializerProperty(string propertyName) => propertyName switch
	{
		"Attributes" => "SetAttributes",
		_ => null,
	};

	private enum MockFileDataKind
	{
		Text,
		Bytes,
	}

	private readonly struct MockFileDataShape
	{
		public MockFileDataShape(
			MockFileDataKind kind,
			ExpressionSyntax primary,
			ExpressionSyntax? secondary,
			List<KeyValuePair<string, ExpressionSyntax>> initializerProperties)
		{
			Kind = kind;
			PrimaryContent = primary;
			SecondaryContent = secondary;
			InitializerProperties = initializerProperties;
		}

		public MockFileDataKind Kind { get; }
		public ExpressionSyntax PrimaryContent { get; }
		public ExpressionSyntax? SecondaryContent { get; }
		public List<KeyValuePair<string, ExpressionSyntax>> InitializerProperties { get; }
	}

	// ── Pattern: MockFileSystem.ctor(files[, options/currentDir]) ────────────

	private static async Task TryRegisterFilesCtorFixAsync(
		CodeFixContext context, Diagnostic diagnostic, SyntaxNode node, string pattern)
	{
		if (!TryGetCreationInLocalDecl(node, out BaseObjectCreationExpressionSyntax? creation,
			    out ArgumentListSyntax? argList, out _, out BlockSyntax? _)
		    || !HasUnqualifiedMockFileSystemTypeName(creation!))
		{
			return;
		}

		SemanticModel? semanticModel = await context.Document
			.GetSemanticModelAsync(context.CancellationToken).ConfigureAwait(false);
		if (semanticModel is null)
		{
			return;
		}

		if (TryParseDictionaryEntries(argList!.Arguments[0].Expression, semanticModel,
			    context.CancellationToken) is null)
		{
			return;
		}

		if (argList.Arguments.Count == 2
		    && TryBuildSecondCtorArgList(argList.Arguments[1], pattern) is null)
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				Resources.TestablyM001CodeFixTitle,
				ct => ApplyFilesCtorRewriteAsync(context.Document, diagnostic, pattern, ct),
				equivalenceKey: pattern),
			diagnostic);
	}

	private static async Task<Document> ApplyFilesCtorRewriteAsync(
		Document document, Diagnostic diagnostic, string pattern, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
		{
			return document;
		}

		SemanticModel? semanticModel = await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
		if (semanticModel is null)
		{
			return document;
		}

		SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
		if (node is null
		    || !TryGetCreationInLocalDecl(node, out BaseObjectCreationExpressionSyntax? creation,
			    out ArgumentListSyntax? argList,
			    out LocalDeclarationStatementSyntax? localDecl,
			    out BlockSyntax? block))
		{
			return document;
		}

		List<DictionaryEntryShape>? entries = TryParseDictionaryEntries(
			argList!.Arguments[0].Expression, semanticModel, cancellationToken);
		if (entries is null)
		{
			return document;
		}

		ArgumentListSyntax newArgList = SyntaxFactory.ArgumentList();
		if (argList.Arguments.Count == 2)
		{
			ArgumentListSyntax? built = TryBuildSecondCtorArgList(argList.Arguments[1], pattern);
			if (built is null)
			{
				return document;
			}

			newArgList = built;
		}

		BaseObjectCreationExpressionSyntax newCreation = creation! switch
		{
			ObjectCreationExpressionSyntax oc => oc
				.WithArgumentList(newArgList.WithTriviaFrom(argList))
				.WithInitializer(null),
			ImplicitObjectCreationExpressionSyntax ic => ic
				.WithArgumentList(newArgList.WithTriviaFrom(argList))
				.WithInitializer(null),
			_ => creation!,
		};

		LocalDeclarationStatementSyntax newDecl = localDecl!.ReplaceNode(creation!, newCreation);

		string variableName = localDecl.Declaration.Variables[0].Identifier.Text;
		(string indentation, string newline) = DetectIndentationAndNewline(localDecl);
		List<StatementSyntax> followUps = entries
			.SelectMany(entry => BuildFollowUpStatements(variableName, entry, indentation, newline))
			.ToList();

		SyntaxList<StatementSyntax> updatedStatements = block!.Statements;
		int index = updatedStatements.IndexOf(localDecl);
		updatedStatements = updatedStatements.Replace(localDecl, newDecl);
		updatedStatements = updatedStatements.InsertRange(index + 1, followUps);

		BlockSyntax newBlock = block.WithStatements(updatedStatements);
		compilationUnit = compilationUnit.ReplaceNode(block, newBlock);
		compilationUnit = SwapToTestablyUsing(compilationUnit);

		return document.WithSyntaxRoot(compilationUnit);
	}

	private static bool TryGetCreationInLocalDecl(
		SyntaxNode node,
		out BaseObjectCreationExpressionSyntax? creation,
		out ArgumentListSyntax? argList,
		out LocalDeclarationStatementSyntax? localDecl,
		out BlockSyntax? block)
	{
		creation = node.FirstAncestorOrSelf<BaseObjectCreationExpressionSyntax>();
		argList = creation?.ArgumentList;
		localDecl = null;
		block = null;

		if (creation is null || argList is null || argList.Arguments.Count < 1)
		{
			return false;
		}

		localDecl = creation.FirstAncestorOrSelf<LocalDeclarationStatementSyntax>();
		if (localDecl is null
		    || localDecl.Declaration.Variables.Count != 1
		    || localDecl.Declaration.Variables[0].Initializer?.Value != creation
		    || localDecl.Parent is not BlockSyntax foundBlock)
		{
			return false;
		}

		block = foundBlock;
		return true;
	}

	private static ArgumentListSyntax? TryBuildSecondCtorArgList(ArgumentSyntax secondArg, string pattern)
	{
		if (pattern == Patterns.MockFileSystemFilesOptionsConstructor)
		{
			return TryBuildTestablyOptionsArgList(secondArg.Expression);
		}

		// MockFileSystemFilesConstructor: second arg is `string currentDirectory`.
		ExpressionSyntax expression = secondArg.Expression;
		if (expression is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression)
		    && literal.Token.ValueText.Length == 0)
		{
			return SyntaxFactory.ArgumentList();
		}

		return SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(
			SyntaxFactory.Argument(BuildUseCurrentDirectoryLambda(expression))));
	}

	private static List<DictionaryEntryShape>? TryParseDictionaryEntries(
		ExpressionSyntax dictExpression, SemanticModel semanticModel, CancellationToken cancellationToken)
	{
		InitializerExpressionSyntax? initializer = dictExpression switch
		{
			ObjectCreationExpressionSyntax oc => oc.Initializer,
			ImplicitObjectCreationExpressionSyntax ic => ic.Initializer,
			_ => null,
		};
		if (initializer is null)
		{
			return null;
		}

		List<DictionaryEntryShape> result = [];
		foreach (ExpressionSyntax entryExpression in initializer.Expressions)
		{
			if (entryExpression is not AssignmentExpressionSyntax assignment
			    || assignment.Left is not ImplicitElementAccessSyntax elementAccess
			    || elementAccess.ArgumentList.Arguments.Count != 1)
			{
				return null;
			}

			ArgumentSyntax keyArg = elementAccess.ArgumentList.Arguments[0];
			MockFileDataShape? shape = ClassifyMockFileDataExpression(
				assignment.Right, semanticModel, cancellationToken);
			if (shape is null)
			{
				return null;
			}

			result.Add(new DictionaryEntryShape(keyArg.Expression, shape.Value));
		}

		return result;
	}

	private static IEnumerable<StatementSyntax> BuildFollowUpStatements(
		string receiverName, DictionaryEntryShape entry, string indentation, string newline)
	{
		string newMethod = entry.Value.Kind == MockFileDataKind.Bytes ? "WriteAllBytes" : "WriteAllText";
		string args = FormatArgumentList(entry.Key, entry.Value);

		// Parse statements from text so the trivia is non-elastic; otherwise the
		// Formatter normalizes the inserted lines and the enclosing block's closing
		// brace, breaking the surrounding indentation style.
		yield return SyntaxFactory.ParseStatement(
			$"{indentation}{receiverName}.File.{newMethod}({args});{newline}");

		// Emit one SetXxx call per initializer property so attributes / future
		// supported metadata aren't silently dropped from the dictionary entry.
		string keyText = entry.Key.ToString().Trim();
		foreach (KeyValuePair<string, ExpressionSyntax> prop in entry.Value.InitializerProperties)
		{
			string setMethod = MapMockFileDataInitializerProperty(prop.Key)!;
			string valueText = prop.Value.ToString().Trim();
			yield return SyntaxFactory.ParseStatement(
				$"{indentation}{receiverName}.File.{setMethod}({keyText}, {valueText});{newline}");
		}
	}

	private static string FormatArgumentList(ExpressionSyntax key, MockFileDataShape shape)
	{
		string primary = $"{key.ToString().Trim()}, {shape.PrimaryContent.ToString().Trim()}";
		if (shape.SecondaryContent is not null)
		{
			return $"{primary}, {shape.SecondaryContent.ToString().Trim()}";
		}

		return primary;
	}

	private static (string indentation, string newline) DetectIndentationAndNewline(SyntaxNode node)
	{
		SyntaxTriviaList leading = node.GetLeadingTrivia();
		System.Text.StringBuilder indent = new();
		foreach (SyntaxTrivia trivia in leading.Reverse())
		{
			if (trivia.IsKind(SyntaxKind.WhitespaceTrivia))
			{
				indent.Insert(0, trivia.ToString());
			}
			else
			{
				break;
			}
		}

		string sourceText = node.SyntaxTree.GetText().ToString();
		string newline = sourceText.Contains("\r\n") ? "\r\n" : "\n";
		return (indent.ToString(), newline);
	}

	private readonly struct DictionaryEntryShape
	{
		public DictionaryEntryShape(ExpressionSyntax key, MockFileDataShape value)
		{
			Key = key;
			Value = value;
		}

		public ExpressionSyntax Key { get; }
		public MockFileDataShape Value { get; }
	}

	// ── Pattern: MockFileData property read ──────────────────────────────────

	private static void TryRegisterPropertyReadFix(
		CodeFixContext context, Diagnostic diagnostic, SyntaxNode node)
	{
		if (!TryMatchOneShotGetFileRead(node, out _, out _, out _, out _))
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				Resources.TestablyM001CodeFixTitle,
				ct => ApplyPropertyReadRewriteAsync(context.Document, diagnostic, ct),
				equivalenceKey: Patterns.MockFileDataPropertyRead),
			diagnostic);
	}

	private static async Task<Document> ApplyPropertyReadRewriteAsync(
		Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
		{
			return document;
		}

		SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
		if (node is null
		    || !TryMatchOneShotGetFileRead(node,
			    out MemberAccessExpressionSyntax? memberAccess,
			    out ExpressionSyntax? receiver,
			    out ArgumentSyntax? pathArg,
			    out string? newMethod))
		{
			return document;
		}

		InvocationExpressionSyntax replacement = SyntaxFactory.InvocationExpression(
			SyntaxFactory.MemberAccessExpression(
				SyntaxKind.SimpleMemberAccessExpression,
				SyntaxFactory.MemberAccessExpression(
					SyntaxKind.SimpleMemberAccessExpression,
					receiver!,
					SyntaxFactory.IdentifierName("File")),
				SyntaxFactory.IdentifierName(newMethod!)),
			SyntaxFactory.ArgumentList(SyntaxFactory.SingletonSeparatedList(pathArg!.WithoutTrivia())));

		compilationUnit = compilationUnit.ReplaceNode(memberAccess!, replacement.WithTriviaFrom(memberAccess!));
		return document.WithSyntaxRoot(compilationUnit);
	}

	private static bool TryMatchOneShotGetFileRead(
		SyntaxNode node,
		out MemberAccessExpressionSyntax? memberAccess,
		out ExpressionSyntax? receiver,
		out ArgumentSyntax? pathArg,
		out string? newMethod)
	{
		memberAccess = node.FirstAncestorOrSelf<MemberAccessExpressionSyntax>();
		receiver = null;
		pathArg = null;
		newMethod = null;

		if (memberAccess is null)
		{
			return false;
		}

		newMethod = MapMockFileDataReadProperty(memberAccess.Name.Identifier.Text);
		if (newMethod is null)
		{
			return false;
		}

		if (memberAccess.Expression is not InvocationExpressionSyntax invocation
		    || invocation.Expression is not MemberAccessExpressionSyntax getFileAccess
		    || getFileAccess.Name.Identifier.Text != "GetFile"
		    || invocation.ArgumentList.Arguments.Count != 1)
		{
			return false;
		}

		// Defensive: even when the property reference is reachable here as a read, it
		// might also be the LHS of a compound assignment (`fs.GetFile(p).Attributes |=
		// FileAttributes.ReadOnly`). Rewriting to a getter call would put the getter on
		// the LHS of the compound — not assignable. Bail for any assignment-target use.
		if (memberAccess.Parent is AssignmentExpressionSyntax assignment
		    && assignment.Left == memberAccess)
		{
			return false;
		}

		receiver = getFileAccess.Expression;
		pathArg = invocation.ArgumentList.Arguments[0];
		return true;
	}

	private static string? MapMockFileDataReadProperty(string propertyName) => propertyName switch
	{
		"TextContents" => "ReadAllText",
		"Contents" => "ReadAllBytes",
		"Attributes" => "GetAttributes",
		// TestableIO returns DateTimeOffset; Testably returns DateTime. We pick the *Utc
		// variant as the closest semantic match. Downstream code that expects
		// DateTimeOffset will fail to compile and needs manual adjustment.
		"CreationTime" => "GetCreationTimeUtc",
		"LastAccessTime" => "GetLastAccessTimeUtc",
		"LastWriteTime" => "GetLastWriteTimeUtc",
		_ => null,
	};

	// ── Pattern: MockFileData property write (one-shot) ──────────────────────

	private static void TryRegisterPropertyWriteFix(
		CodeFixContext context, Diagnostic diagnostic, SyntaxNode node)
	{
		if (!TryMatchOneShotGetFileWrite(node, out _, out _, out _, out _, out _, out _))
		{
			return;
		}

		context.RegisterCodeFix(
			CodeAction.Create(
				Resources.TestablyM001CodeFixTitle,
				ct => ApplyPropertyWriteRewriteAsync(context.Document, diagnostic, ct),
				equivalenceKey: Patterns.MockFileDataPropertyWrite),
			diagnostic);
	}

	private static async Task<Document> ApplyPropertyWriteRewriteAsync(
		Document document, Diagnostic diagnostic, CancellationToken cancellationToken)
	{
		SyntaxNode? root = await document.GetSyntaxRootAsync(cancellationToken).ConfigureAwait(false);
		if (root is not CompilationUnitSyntax compilationUnit)
		{
			return document;
		}

		SyntaxNode? node = root.FindNode(diagnostic.Location.SourceSpan, getInnermostNodeForTie: true);
		if (node is null
		    || !TryMatchOneShotGetFileWrite(node,
			    out _,
			    out ExpressionStatementSyntax? statement,
			    out ExpressionSyntax? receiver,
			    out ArgumentSyntax? pathArg,
			    out ExpressionSyntax? value,
			    out string? newMethod))
		{
			return document;
		}

		// Build the new statement by parsing it from text. The resulting trivia is
		// non-elastic, so the Formatter leaves the inserted statement and the
		// enclosing block's closing brace alone, preserving the source's indentation.
		string receiverText = receiver!.ToString().Trim();
		string pathText = pathArg!.ToString().Trim();
		string valueText = value!.ToString().Trim();
		string text = $"{receiverText}.File.{newMethod!}({pathText}, {valueText});";
		StatementSyntax parsed = SyntaxFactory.ParseStatement(text);
		StatementSyntax newStatement = parsed.WithTriviaFrom(statement!);

		compilationUnit = compilationUnit.ReplaceNode(statement!, newStatement);
		return document.WithSyntaxRoot(compilationUnit);
	}

	private static bool TryMatchOneShotGetFileWrite(
		SyntaxNode node,
		out AssignmentExpressionSyntax? assignment,
		out ExpressionStatementSyntax? statement,
		out ExpressionSyntax? receiver,
		out ArgumentSyntax? pathArg,
		out ExpressionSyntax? value,
		out string? newMethod)
	{
		assignment = null;
		statement = null;
		receiver = null;
		pathArg = null;
		value = null;
		newMethod = null;

		MemberAccessExpressionSyntax? memberAccess = node.FirstAncestorOrSelf<MemberAccessExpressionSyntax>();
		if (memberAccess is null)
		{
			return false;
		}

		newMethod = MapMockFileDataWriteProperty(memberAccess.Name.Identifier.Text);
		if (newMethod is null)
		{
			return false;
		}

		if (memberAccess.Expression is not InvocationExpressionSyntax invocation
		    || invocation.Expression is not MemberAccessExpressionSyntax getFileAccess
		    || getFileAccess.Name.Identifier.Text != "GetFile"
		    || invocation.ArgumentList.Arguments.Count != 1)
		{
			return false;
		}

		if (memberAccess.Parent is not AssignmentExpressionSyntax found
		    || !found.IsKind(SyntaxKind.SimpleAssignmentExpression)
		    || found.Left != memberAccess
		    || found.Parent is not ExpressionStatementSyntax foundStatement)
		{
			return false;
		}

		receiver = getFileAccess.Expression;
		pathArg = invocation.ArgumentList.Arguments[0];
		assignment = found;
		value = found.Right;
		statement = foundStatement;
		return true;
	}

	private static string? MapMockFileDataWriteProperty(string propertyName) => propertyName switch
	{
		"TextContents" => "WriteAllText",
		"Contents" => "WriteAllBytes",
		"Attributes" => "SetAttributes",
		_ => null,
	};

	// ── Shared: using-directive swap ─────────────────────────────────────────

	private static CompilationUnitSyntax SwapToTestablyUsing(CompilationUnitSyntax compilationUnit)
	{
		UsingDirectiveSyntax? testingHelpersUsing = compilationUnit.Usings
			.FirstOrDefault(u => u.Name?.ToString() == TestingHelpersNamespace);
		bool hasTestablyUsing = compilationUnit.Usings
			.Any(u => u.Name?.ToString() == TestablyTestingNamespace);

		if (testingHelpersUsing is not null)
		{
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
			UsingDirectiveSyntax usingDirective = BuildUsingDirective(compilationUnit, TestablyTestingNamespace);
			compilationUnit = compilationUnit.AddUsings(usingDirective);
		}

		return compilationUnit;
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
