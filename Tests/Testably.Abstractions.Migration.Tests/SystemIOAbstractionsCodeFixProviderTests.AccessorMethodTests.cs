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

		[Fact]
		public async Task AddDrive_EmptyInitializer_ShouldRewriteToWithDrive()
		{
			// `new MockDriveData()` with no initializer has nothing to chain — the
			// rewrite collapses to the single-argument WithDrive overload. The using
			// must also swap, since WithDrive is Testably-only.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs) => {|#0:fs.AddDrive("D:", new MockDriveData())|};
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem fs) => fs.WithDrive("D:");
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddDrive_TotalSizeOnly_ShouldRewriteToWithDriveLambda()
		{
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> {|#0:fs.AddDrive("D:", new MockDriveData { TotalSize = 100 })|};
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> fs.WithDrive("D:", d => d.SetTotalSize(100));
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddDrive_AllMappableProperties_ShouldChainSetters()
		{
			// Initializer order is preserved in the chained call so the user can verify
			// any sequence-dependent behaviour by reading the rewrite top-down.
			const string source = """
				using System.IO;
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
					{
						{|#0:fs.AddDrive("D:", new MockDriveData
						{
							TotalSize = 100,
							IsReady = false,
							DriveFormat = "NTFS",
							DriveType = DriveType.Fixed,
						})|};
					}
				}
				""";

			const string fixedSource = """
				using System.IO;
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem fs)
					{
						fs.WithDrive("D:", d => d.SetTotalSize(100).SetIsReady(false).SetDriveFormat("NTFS").SetDriveType(DriveType.Fixed));
					}
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddDrive_TargetTypedNew_ShouldRewriteToWithDrive()
		{
			// `new() { ... }` resolves to MockDriveData via the AddDrive parameter type;
			// the rewrite path treats it the same as an explicit `new MockDriveData()`.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> {|#0:fs.AddDrive("D:", new() { TotalSize = 200 })|};
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> fs.WithDrive("D:", d => d.SetTotalSize(200));
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddDrive_ShadowingDriveIdentifier_PicksFreshLambdaParameter()
		{
			// The initializer RHS references an outer `d`; the rewrite must not let the
			// lambda parameter shadow it. PickFreshDriveLambdaParameterName falls through
			// to `drive` (next candidate).
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs, long d)
						=> {|#0:fs.AddDrive("D:", new MockDriveData { TotalSize = d })|};
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem fs, long d)
						=> fs.WithDrive("D:", drive => drive.SetTotalSize(d));
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddDrive_InterfaceTypedReceiver_HasNoFix()
		{
			// The rewrite emits `<receiver>.WithDrive(...)`. IMockFileDataAccessor has no
			// WithDrive, so the fix must not run when the user calls through the
			// interface.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(IMockFileDataAccessor accessor)
						=> {|#0:accessor.AddDrive("D:", new MockDriveData())|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Fact]
		public async Task AddDrive_NonLiteralMockDriveData_HasNoFix()
		{
			// A captured MockDriveData reference (parameter, local, field, etc.) has no
			// safe textual rewrite — the user may pass a subclass or mutate the data.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs, MockDriveData data)
						=> {|#0:fs.AddDrive("D:", data)|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Fact]
		public async Task AddDrive_UnsupportedInitializerProperty_HasNoFix()
		{
			// AvailableFreeSpace has no IStorageDrive setter — manual review required.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> {|#0:fs.AddDrive("D:", new MockDriveData { AvailableFreeSpace = 50 })|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Fact]
		public async Task AddDrive_CopyConstructor_HasNoFix()
		{
			// `new MockDriveData(template)` has no 1:1 mapping to a single WithDrive
			// callback — the user might tweak fields after the copy.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs, MockDriveData template)
						=> {|#0:fs.AddDrive("D:", new MockDriveData(template))|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Fact]
		public async Task AddDrive_FullyQualifiedReceiverDeclaration_HasNoFix()
		{
			// The parameter declaration uses the fully-qualified type name, so the
			// using-swap can't retarget the receiver — after the swap, `fs` still binds
			// to TestableIO MockFileSystem and `fs.WithDrive(...)` would not compile.
			const string source = """
				public class C
				{
					public void Run(System.IO.Abstractions.TestingHelpers.MockFileSystem fs)
						=> {|#0:fs.AddDrive("D:", new System.IO.Abstractions.TestingHelpers.MockDriveData())|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Fact]
		public async Task AddDrive_AliasQualifiedReceiverDeclaration_HasNoFix()
		{
			// The parameter declaration is alias-qualified (`TestableIo.MockFileSystem`).
			// The using-swap touches `using System.IO.Abstractions.TestingHelpers;` but
			// leaves the alias `using TestableIo = ...;` in place, so `fs` stays bound to
			// TestableIO and the rewrite would not compile.
			const string source = """
				using TestableIo = System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(TestableIo.MockFileSystem fs)
						=> {|#0:fs.AddDrive("D:", new TestableIo.MockDriveData())|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Theory]
		[InlineData("AllPaths")]
		[InlineData("AllFiles")]
		[InlineData("AllDirectories")]
		[InlineData("AllDrives")]
		public async Task EnumerationProperty_HasNoFix(string property)
		{
			// Testably has no 1:1 equivalent for the IMockFileDataAccessor enumeration
			// properties. The natural replacements (Directory.EnumerateFiles, etc.)
			// require a root path or drive scope the analyzer cannot infer safely, so
			// the fix dispatcher intentionally falls through with no rewrite.
			string source = $$"""
				using System.Collections.Generic;
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public IEnumerable<string> Read(MockFileSystem fs) => {|#0:fs.{{property}}|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Theory]
		[InlineData("() => System.DateTime.UnixEpoch")]
		[InlineData("() => System.DateTime.Now")]
		[InlineData("() => System.DateTime.UtcNow")]
		[InlineData("dateTimeProvider")]
		public async Task MockTime_HasNoFix(string argument)
		{
			// Phase 5.3 ships MockTime as manual review only. TestableIO calls the
			// supplied delegate every time it needs a timestamp; Testably installs a
			// fixed-then-mutable MockTimeSystem at construction. The two have no
			// observably-equivalent automatic rewrite for arbitrary delegates, and the
			// equivalent surface (`o => o.UseTimeSystem(...)`) lives inside the
			// MockFileSystemOptions lambda — a cross-statement fold that conflicts
			// with the parameterless / options-ctor fixes when both touch the same
			// construction. A future sub-phase may opt-in fix the narrow constant-
			// DateTime lambda shape with a custom FixAllProvider.
			string source = $$"""
				using System;
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs, Func<DateTime> dateTimeProvider)
						=> {|#0:fs.MockTime({{argument}})|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}
	}
}
