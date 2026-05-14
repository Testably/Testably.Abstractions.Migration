using Testably.Abstractions.Migration.Analyzers;
using Verifier =
	Testably.Abstractions.Migration.Tests.Verifiers.CSharpCodeFixVerifier<
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsAnalyzer,
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsCodeFixProvider>;

namespace Testably.Abstractions.Migration.Tests;

public class SystemIOAbstractionsCodeFixProviderTests
{
	[Fact]
	public async Task ParameterlessConstructor_ShouldSwapUsingDirective()
	{
		const string source = """
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build() => {|#0:new MockFileSystem()|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions;
			using Testably.Abstractions.Testing;

			public class C
			{
				public IFileSystem Build() => new MockFileSystem();
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task ParameterlessConstructor_TargetTypedNew_ShouldSwapUsingDirective()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public MockFileSystem Build()
				{
					MockFileSystem fileSystem = {|#0:new()|};
					return fileSystem;
				}
			}
			""";

		const string fixedSource = """
			using Testably.Abstractions.Testing;

			public class C
			{
				public MockFileSystem Build()
				{
					MockFileSystem fileSystem = new();
					return fileSystem;
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}
}
