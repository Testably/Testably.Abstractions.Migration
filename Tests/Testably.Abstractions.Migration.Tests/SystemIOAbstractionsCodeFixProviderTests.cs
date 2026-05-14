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
	public async Task AccessorAddFile_WithAttributesInitializer_ShouldExpandToWriteAndSetAttributes()
	{
		const string source = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
				{
					{|#0:fs.AddFile("/foo", new MockFileData("x") { Attributes = FileAttributes.ReadOnly })|};
				}
			}
			""";

		const string fixedSource = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
				{
					fs.File.WriteAllText("/foo", "x");
					fs.File.SetAttributes("/foo", FileAttributes.ReadOnly);
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task AccessorAddFile_WithUnsupportedInitializerProperty_HasNoFix()
	{
		// LastWriteTime in object initializers is deferred (DateTime vs DateTimeOffset).
		const string source = """
			using System;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> {|#0:fs.AddFile("/foo", new MockFileData("x") { LastWriteTime = DateTimeOffset.UtcNow })|};
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task AccessorAddFile_WithInitializer_NotInBlock_HasNoFix()
	{
		// Expression-bodied member: there is no statement block to host the follow-up
		// SetAttributes call, so the fix is suppressed.
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
	public async Task MockFileDataRead_TextContents_ShouldRewriteToFileReadAllText()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public string Read(MockFileSystem fs) => {|#0:fs.GetFile("/a").TextContents|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public string Read(MockFileSystem fs) => fs.File.ReadAllText("/a");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task MockFileDataRead_Contents_ShouldRewriteToFileReadAllBytes()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public byte[] Read(MockFileSystem fs) => {|#0:fs.GetFile("/a").Contents|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public byte[] Read(MockFileSystem fs) => fs.File.ReadAllBytes("/a");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task MockFileDataRead_Attributes_ShouldRewriteToFileGetAttributes()
	{
		const string source = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public FileAttributes Read(MockFileSystem fs) => {|#0:fs.GetFile("/a").Attributes|};
			}
			""";

		const string fixedSource = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public FileAttributes Read(MockFileSystem fs) => fs.File.GetAttributes("/a");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Theory]
	[InlineData("CreationTime", "GetCreationTimeUtc")]
	[InlineData("LastAccessTime", "GetLastAccessTimeUtc")]
	[InlineData("LastWriteTime", "GetLastWriteTimeUtc")]
	public async Task MockFileDataRead_TimeProperty_ShouldRewriteToFileGetTimeUtc(
		string property, string newMethod)
	{
		string source = $$"""
			using System;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public DateTimeOffset Read(MockFileSystem fs) => {|#0:fs.GetFile("/a").{{property}}|};
			}
			""";

		string fixedSource = $$"""
			using System;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public DateTimeOffset Read(MockFileSystem fs) => fs.File.{{newMethod}}("/a");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task MockFileDataRead_CapturedReference_HasNoFix()
	{
		// `data` is a captured MockFileData reference, not a one-shot `GetFile(p).Prop`.
		// Without flow analysis we cannot safely rewrite. Phase 4 will surface this case.
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

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task MockFileDataWrite_TextContents_ShouldRewriteToFileWriteAllText()
	{
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Write(MockFileSystem fs)
				{
					{|#0:fs.GetFile("/a").TextContents|} = "hello";
				}
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Write(MockFileSystem fs)
				{
					fs.File.WriteAllText("/a", "hello");
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task MockFileDataWrite_Attributes_ShouldRewriteToFileSetAttributes()
	{
		const string source = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Write(MockFileSystem fs)
				{
					{|#0:fs.GetFile("/a").Attributes|} = FileAttributes.ReadOnly;
				}
			}
			""";

		const string fixedSource = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Write(MockFileSystem fs)
				{
					fs.File.SetAttributes("/a", FileAttributes.ReadOnly);
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task MockFileDataWrite_CapturedReference_HasNoFix()
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

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task MockFileDataWrite_TimeProperty_HasNoFix()
	{
		// Time-property writes need a DateTimeOffset → DateTime conversion the rewrite
		// can't emit safely; Phase 4 manual review will pick this up.
		const string source = """
			using System;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Write(MockFileSystem fs)
				{
					{|#0:fs.GetFile("/a").LastWriteTime|} = DateTimeOffset.UtcNow;
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task MockFileDataRead_UnsupportedProperty_HasNoFix()
	{
		// AllowedFileShare has no direct File.Get* equivalent — Phase 4 manual-review.
		const string source = """
			using System.IO;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public FileShare Read(MockFileSystem fs) => {|#0:fs.GetFile("/a").AllowedFileShare|};
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

	[Fact]
	public async Task OptionsConstructor_CurrentDirectoryReferencesIdentifierNamed_o_ShouldPickFreshLambdaParameter()
	{
		// The default lambda parameter name `o` would shadow the local `o`, rewriting
		// `CurrentDirectory = o` to `o => o.UseCurrentDirectory(o)` — wrong semantics.
		const string source = """
			using System.IO.Abstractions;
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build()
				{
					string o = "/work";
					return {|#0:new MockFileSystem(new MockFileSystemOptions { CurrentDirectory = o })|};
				}
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions;
			using Testably.Abstractions.Testing;

			public class C
			{
				public IFileSystem Build()
				{
					string o = "/work";
					return new MockFileSystem(options => options.UseCurrentDirectory(o));
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task AccessorMoveDirectory_WithNamedArgs_ShouldStripNameColons()
	{
		// TestableIO uses sourcePath/destPath; Directory.Move uses sourceDirName/destDirName.
		// Keeping the labels would produce code that won't compile.
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> {|#0:fs.MoveDirectory(sourcePath: "/a", destPath: "/b")|};
			}
			""";

		const string fixedSource = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(MockFileSystem fs)
					=> fs.Directory.Move("/a", "/b");
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			fixedSource);
	}

	[Fact]
	public async Task ParameterlessConstructor_AliasQualifiedType_HasNoFix()
	{
		// `using TestableIo = …;` keeps the type alias-qualified. The using-only fix
		// would not retarget the binding, so we suppress it.
		const string source = """
			using System.IO.Abstractions;
			using TestableIo = System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public IFileSystem Build() => {|#0:new TestableIo.MockFileSystem()|};
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task AccessorAddDirectory_InterfaceTypedReceiver_HasNoFix()
	{
		// The rewrite would emit `accessor.Directory.CreateDirectory(...)`, but
		// IMockFileDataAccessor doesn't expose a Directory property — non-compiling.
		const string source = """
			using System.IO.Abstractions.TestingHelpers;

			public class C
			{
				public void Run(IMockFileDataAccessor accessor) => {|#0:accessor.AddDirectory("/foo")|};
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}

	[Fact]
	public async Task ParameterlessConstructor_TargetTypedNewWithQualifiedTarget_HasNoFix()
	{
		// `new()` is target-typed; the qualified LHS keeps the construction bound to
		// TestableIO regardless of the using swap. Suppress the fix to avoid leaving
		// the source half-rewritten.
		const string source = """
			public class C
			{
				public void Run()
				{
					System.IO.Abstractions.TestingHelpers.MockFileSystem fs = {|#0:new()|};
				}
			}
			""";

		await Verifier.VerifyCodeFixAsync(
			source,
			Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
			source);
	}
}
