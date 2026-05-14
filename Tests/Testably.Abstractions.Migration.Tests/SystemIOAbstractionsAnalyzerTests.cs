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
	public async Task FilesConstructor_ShouldBeFlagged()
	{
		const string source = """
			using System.Collections.Generic;
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build()
					=> {|#0:new MockFileSystem(new Dictionary<string, MockFileData>())|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task OptionsConstructor_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build()
					=> {|#0:new MockFileSystem(new MockFileSystemOptions())|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task FilesOptionsConstructor_ShouldBeFlagged()
	{
		const string source = """
			using System.Collections.Generic;
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build()
					=> {|#0:new MockFileSystem(new Dictionary<string, MockFileData>(), new MockFileSystemOptions())|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Theory]
	[InlineData("AddDirectory(\"/foo\")")]
	[InlineData("AddEmptyFile(\"/foo\")")]
	[InlineData("RemoveFile(\"/foo\")")]
	[InlineData("MoveDirectory(\"/foo\", \"/bar\")")]
	[InlineData("FileExists(\"/foo\")")]
	public async Task AccessorMethods_ShouldBeFlagged(string invocation)
	{
		string source = $$"""
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs) => {|#0:fs.{{invocation}}|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task AddFile_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> {|#0:fs.AddFile("/foo", new MockFileData("x"))|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
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
