using Testably.Abstractions.Migration.Analyzers;
using Verifier =
	Testably.Abstractions.Migration.Tests.Verifiers.CSharpAnalyzerVerifier<
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsAnalyzer>;

namespace Testably.Abstractions.Migration.Tests;

public sealed class SystemIOAbstractionsAnalyzerTests
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
	public async Task AddDrive_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> {|#0:fs.AddDrive("D:", new MockDriveData())|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockTime_ShouldBeFlagged()
	{
		const string source = """
			using System;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> {|#0:fs.MockTime(() => DateTime.UnixEpoch)|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task AddFileFromEmbeddedResource_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;
			using System.Reflection;

			public class C
			{
				public void Run(MockFileSystem fs, Assembly asm)
					=> {|#0:fs.AddFileFromEmbeddedResource("/data/foo.json", asm, "MyAssembly.TestData.foo.json")|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task AddFilesFromEmbeddedNamespace_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;
			using System.Reflection;

			public class C
			{
				public void Run(MockFileSystem fs, Assembly asm)
					=> {|#0:fs.AddFilesFromEmbeddedNamespace("/data", asm, "MyAssembly.TestData")|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Theory]
	[InlineData("AllPaths")]
	[InlineData("AllFiles")]
	[InlineData("AllDirectories")]
	[InlineData("AllDrives")]
	public async Task EnumerationProperty_OnMockFileSystem_ShouldBeFlagged(string property)
	{
		string source = $$"""
			using System.Collections.Generic;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IEnumerable<string> Read(MockFileSystem fs) => {|#0:fs.{{property}}|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task EnumerationProperty_OnAccessorInterface_ShouldBeFlagged()
	{
		// AllFiles is declared on IMockFileDataAccessor — when the receiver is the
		// interface type itself, the property symbol's containing type points at the
		// interface, which the analyzer must still recognise.
		const string source = """
			using System.Collections.Generic;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IEnumerable<string> Read(IMockFileDataAccessor accessor) => {|#0:accessor.AllFiles|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Theory]
	[InlineData("TextContents")]
	[InlineData("Contents")]
	[InlineData("Attributes")]
	[InlineData("LastWriteTime")]
	public async Task MockFileDataPropertyRead_ShouldBeFlagged(string property)
	{
		string source = $$"""
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public object Read(MockFileSystem fs) => {|#0:fs.GetFile("/a").{{property}}|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Theory]
	[InlineData("AccessControl")]
	[InlineData("AllowedFileShare")]
	[InlineData("UnixMode")]
	public async Task MockFileDataManualReviewProperty_ShouldBeFlagged(string property)
	{
		string source = $$"""
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public object Read(MockFileSystem fs) => {|#0:fs.GetFile("/a").{{property}}|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileDataManualReviewProperty_PlainWrite_ShouldBeFlagged()
	{
		const string source = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Write(MockFileSystem fs)
					=> {|#0:fs.GetFile("/a").AllowedFileShare|} = FileShare.Read;
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileDataManualReviewProperty_ObjectInitializerWrite_ShouldBeFlagged()
	{
		// The migratable property writes inside an object initializer are skipped to
		// avoid double-flagging with the AddFile expansion (Phase 3.5). Manual-review
		// properties have no AddFile expansion, so they must still be flagged here —
		// otherwise the lossy call site would be silently invisible. The AddFile
		// invocation also gets its own diagnostic, so two diagnostics for one user-
		// visible site is expected.
		const string source = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
				{
					{|#0:fs.AddFile("/a", new MockFileData("hello") { {|#1:AllowedFileShare|} = FileShare.Read })|};
				}
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(1));
	}

	[Fact]
	public async Task MockFileVersionInfoConstructor_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public MockFileVersionInfo Build() => {|#0:new MockFileVersionInfo("/a.dll", "1.2.3")|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileSystemSubclass_ShouldBeFlaggedAtClassDeclaration()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class {|#0:MyMockFs|} : MockFileSystem
			{
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileSystemSubclass_Transitive_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class {|#0:Intermediate|} : MockFileSystem
			{
			}

			public class {|#1:Leaf|} : Intermediate
			{
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(1));
	}

	[Fact]
	public async Task MockFileDataSubclass_ShouldBeFlaggedAtClassDeclaration()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class {|#0:MyData|} : MockFileData
			{
				public MyData() : base("hello") { }
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileDataCopyConstructor_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public MockFileData Clone(MockFileData template) => {|#0:new MockFileData(template)|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileDataPropertyWrite_ShouldBeFlagged()
	{
		const string source = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Write(MockFileSystem fs)
					=> {|#0:fs.GetFile("/a").Attributes|} = FileAttributes.ReadOnly;
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileDataCapturedReferenceRead_FromLocal_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public string Read(MockFileSystem fs)
				{
					MockFileData data = fs.GetFile("/a");
					return {|#0:data.TextContents|};
				}
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileDataCapturedReferenceWrite_FromLocal_ShouldBeFlagged()
	{
		const string source = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Write(MockFileSystem fs)
				{
					MockFileData data = fs.GetFile("/a");
					{|#0:data.Attributes|} = FileAttributes.ReadOnly;
				}
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileDataCapturedReferenceRead_FromParameter_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public string Read(MockFileData data) => {|#0:data.TextContents|};
			}
			""";

		await Verifier.VerifyAnalyzerAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0));
	}

	[Fact]
	public async Task MockFileDataCapturedReferenceRead_FromField_ShouldBeFlagged()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				private MockFileData data = new("hello");

				public string Read() => {|#0:data.TextContents|};
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
