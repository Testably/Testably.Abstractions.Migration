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

	[Fact]
	public async Task OptionsConstructor_EmptyInitializer_ShouldRewriteToParameterless()
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

		const string fixedSource = """
			using System.IO.Abstractions;
			using Testably.Abstractions.Testing;

			public class C
			{
				public IFileSystem Build()
					=> new MockFileSystem();
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task OptionsConstructor_CurrentDirectory_ShouldRewriteToUseCurrentDirectoryLambda()
	{
		const string source = """
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build()
					=> {|#0:new MockFileSystem(new MockFileSystemOptions { CurrentDirectory = "/work" })|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions;
			using Testably.Abstractions.Testing;

			public class C
			{
				public IFileSystem Build()
					=> new MockFileSystem(o => o.UseCurrentDirectory("/work"));
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task OptionsConstructor_UnsupportedProperty_HasNoFix()
	{
		// The analyzer still flags the call site, but no code action is registered
		// (CreateDefaultTempDir has no Testably equivalent). The user must address it
		// manually, so the source must be identical before and after.
		const string source = """
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build()
					=> {|#0:new MockFileSystem(new MockFileSystemOptions { CreateDefaultTempDir = false })|};
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task AccessorAddDirectory_ShouldRewriteToDirectoryCreateDirectory()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs) => {|#0:fs.AddDirectory("/foo")|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs) => fs.Directory.CreateDirectory("/foo");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task AccessorRemoveFile_ShouldRewriteToFileDelete_AndDropVerifyAccess()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs) => {|#0:fs.RemoveFile("/foo", false)|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs) => fs.File.Delete("/foo");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task AccessorMoveDirectory_ShouldRewriteToDirectoryMove()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs) => {|#0:fs.MoveDirectory("/a", "/b")|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs) => fs.Directory.Move("/a", "/b");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task AccessorFileExists_ShouldRewriteToFileExists()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public bool Run(MockFileSystem fs) => {|#0:fs.FileExists("/foo")|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public bool Run(MockFileSystem fs) => fs.File.Exists("/foo");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task AccessorAddFile_TextContent_ShouldRewriteToFileWriteAllText()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> {|#0:fs.AddFile("/foo", new MockFileData("hello"))|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> fs.File.WriteAllText("/foo", "hello");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task AccessorAddFile_TextContentWithEncoding_ShouldRewriteToFileWriteAllText()
	{
		const string source = """
			using System.Text;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> {|#0:fs.AddFile("/foo", new MockFileData("hello", Encoding.UTF8))|};
			}
			""";

		const string fixedSource = """
			using System.Text;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> fs.File.WriteAllText("/foo", "hello", Encoding.UTF8);
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task AccessorAddFile_ByteContent_ShouldRewriteToFileWriteAllBytes()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs, byte[] bytes)
					=> {|#0:fs.AddFile("/foo", new MockFileData(bytes))|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs, byte[] bytes)
					=> fs.File.WriteAllBytes("/foo", bytes);
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task AccessorAddFile_WithInitializer_HasNoFix()
	{
		// MockFileData with extra property initializers is a Phase 4 manual-review case.
		const string source = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> {|#0:fs.AddFile("/foo", new MockFileData("x") { Attributes = FileAttributes.ReadOnly })|};
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task AccessorAddFile_NonLiteralMockFileData_HasNoFix()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs, MockFileData data)
					=> {|#0:fs.AddFile("/foo", data)|};
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task FilesConstructor_LocalDecl_ShouldExpandToParameterlessCtorAndWrites()
	{
		const string source = """
			using System.Collections.Generic;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run()
				{
					var fs = {|#0:new MockFileSystem(new Dictionary<string, MockFileData>
					{
						["/foo"] = new MockFileData("hello"),
					})|};
				}
			}
			""";

		const string fixedSource = """
			using System.Collections.Generic;
			using Testably.Abstractions.Testing;

			public class C
			{
				public void Run()
				{
					var fs = new MockFileSystem();
					fs.File.WriteAllText("/foo", "hello");
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task FilesConstructor_WithCurrentDirectoryString_ShouldFoldIntoOptionsLambda()
	{
		const string source = """
			using System.Collections.Generic;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run()
				{
					var fs = {|#0:new MockFileSystem(new Dictionary<string, MockFileData>
					{
						["/foo"] = new MockFileData("hello"),
					}, "/work")|};
				}
			}
			""";

		const string fixedSource = """
			using System.Collections.Generic;
			using Testably.Abstractions.Testing;

			public class C
			{
				public void Run()
				{
					var fs = new MockFileSystem(o => o.UseCurrentDirectory("/work"));
					fs.File.WriteAllText("/foo", "hello");
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task FilesOptionsConstructor_ShouldFoldOptionsAndExpandEntries()
	{
		const string source = """
			using System.Collections.Generic;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(byte[] bytes)
				{
					var fs = {|#0:new MockFileSystem(new Dictionary<string, MockFileData>
					{
						["/a"] = new MockFileData("hello"),
						["/b"] = new MockFileData(bytes),
					}, new MockFileSystemOptions { CurrentDirectory = "/work" })|};
				}
			}
			""";

		const string fixedSource = """
			using System.Collections.Generic;
			using Testably.Abstractions.Testing;

			public class C
			{
				public void Run(byte[] bytes)
				{
					var fs = new MockFileSystem(o => o.UseCurrentDirectory("/work"));
					fs.File.WriteAllText("/a", "hello");
					fs.File.WriteAllBytes("/b", bytes);
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task FilesConstructor_ExpressionBodyContext_HasNoFix()
	{
		// The construction is not in a local-declaration statement, so the expansion
		// would have nowhere to place the follow-up File.Write* statements.
		const string source = """
			using System.Collections.Generic;
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build()
					=> {|#0:new MockFileSystem(new Dictionary<string, MockFileData>
					{
						["/foo"] = new MockFileData("hello"),
					})|};
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task AccessorAddEmptyFile_ShouldRewriteToFileCreateDispose()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs) => {|#0:fs.AddEmptyFile("/foo")|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs) => fs.File.Create("/foo").Dispose();
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}
}
