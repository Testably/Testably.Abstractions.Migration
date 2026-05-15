using Testably.Abstractions.Migration.Analyzers;
using Verifier =
	Testably.Abstractions.Migration.Tests.Verifiers.CSharpCodeFixVerifier<
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsAnalyzer,
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsCodeFixProvider>;

namespace Testably.Abstractions.Migration.Tests;

public partial class SystemIOAbstractionsCodeFixProviderTests
{
	public sealed class AccessorMethodTests
	{
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
			// Use a block body so the test isolates the unsupported-property check from the
			// block-context gate that already suppresses expression-bodied AddFile sites.
			const string source = """
				using System;
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
					{
						{|#0:fs.AddFile("/foo", new MockFileData("x") { LastWriteTime = DateTimeOffset.UtcNow })|};
					}
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
	}
}
