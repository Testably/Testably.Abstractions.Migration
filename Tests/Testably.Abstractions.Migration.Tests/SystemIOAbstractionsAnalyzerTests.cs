using Testably.Abstractions.Migration.Analyzers;
using Verifier =
	Testably.Abstractions.Migration.Tests.Verifiers.CSharpAnalyzerVerifier<
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsAnalyzer>;

namespace Testably.Abstractions.Migration.Tests;

public class SystemIOAbstractionsAnalyzerTests
{
	[Fact]
	public async Task ParameterlessConstructor_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build() => {|#0:new MockFileSystem()|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task ConstructorWithArguments_ShouldNotBeFlagged_InPhase1()
	{
		const string source = """
			using System.Collections.Generic;
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build()
					=> new MockFileSystem(new Dictionary<string, MockFileData>());
			}
			""";

		await Verifier.VerifyAnalyzerAsync(source);
	}

	[Fact]
	public async Task WithoutTestingHelpersAssembly_ShouldDoNothing()
	{
		const string source = """
			public class C
			{
				public object Build() => new object();
			}
			""";

		await Verifier.VerifyAnalyzerAsync(source);
	}
}
