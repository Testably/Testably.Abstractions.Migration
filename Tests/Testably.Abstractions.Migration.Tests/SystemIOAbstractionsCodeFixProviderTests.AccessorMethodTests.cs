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
		public async Task AddDrive_LocalVariableReceiver_ShouldRewriteToWithDrive()
		{
			// Local-variable receiver: declarator syntax is VariableDeclaratorSyntax.
			// The `var` type annotation parses to an unqualified IdentifierName, so the
			// using-swap can safely retarget the inferred MockFileSystem.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem source)
					{
						var fs = source;
						{|#0:fs.AddDrive("D:", new MockDriveData())|};
					}
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem source)
					{
						var fs = source;
						fs.WithDrive("D:");
					}
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddDrive_PropertyReceiver_ShouldRewriteToWithDrive()
		{
			// Property-typed receiver: declarator syntax is PropertyDeclarationSyntax.
			// The declared type is unqualified MockFileSystem, so the using-swap retargets
			// it correctly.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public MockFileSystem Fs { get; set; } = null!;
					public void Run() => {|#0:Fs.AddDrive("D:", new MockDriveData())|};
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public MockFileSystem Fs { get; set; } = null!;
					public void Run() => Fs.WithDrive("D:");
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddDrive_MethodReturnReceiver_ShouldRewriteToWithDrive()
		{
			// Method-return-typed receiver: declarator syntax is MethodDeclarationSyntax.
			// The return type is unqualified MockFileSystem, so the using-swap retargets
			// the call site.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public MockFileSystem GetFs() => null!;
					public void Run() => {|#0:GetFs().AddDrive("D:", new MockDriveData())|};
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public MockFileSystem GetFs() => null!;
					public void Run() => GetFs().WithDrive("D:");
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddDrive_NullablePropertyReceiver_ShouldRewriteToWithDrive()
		{
			// Nullable-annotated property type: PropertyDeclarationSyntax.Type is a
			// NullableTypeSyntax wrapping IdentifierName. The retargetability check
			// recurses through the nullable wrapper.
			const string source = """
				#nullable enable
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public MockFileSystem? Fs { get; set; }
					public void Run() => {|#0:Fs!.AddDrive("D:", new MockDriveData())|};
				}
				""";

			const string fixedSource = """
				#nullable enable
				using Testably.Abstractions.Testing;

				public class C
				{
					public MockFileSystem? Fs { get; set; }
					public void Run() => Fs!.WithDrive("D:");
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
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

		[Fact]
		public async Task AddFileFromEmbeddedResource_HasNoFix()
		{
			// Testably exposes only a bulk InitializeEmbeddedResourcesFromAssembly with no
			// single-file overload, and uses path-style matching against the auto-stripped
			// resource name rather than TestableIO's literal dot-prefix StartsWith. A naive
			// rewrite would compile but materialize a different resource set, so the call
			// site is reported for manual migration.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;
				using System.Reflection;

				public class C
				{
					public void Run(MockFileSystem fs, Assembly asm)
						=> {|#0:fs.AddFileFromEmbeddedResource("/data/foo.json", asm, "MyAssembly.TestData.foo.json")|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Fact]
		public async Task AddFilesFromEmbeddedNamespace_NonResolvableAssembly_HasNoFix()
		{
			// When the assembly arg is an opaque parameter, the analyzer cannot identify
			// which prefix to strip from the literal — fix dispatcher falls through to
			// manual review.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;
				using System.Reflection;

				public class C
				{
					public void Run(MockFileSystem fs, Assembly asm)
						=> {|#0:fs.AddFilesFromEmbeddedNamespace("/data", asm, "MyAssembly.TestData")|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Fact]
		public async Task AddFilesFromEmbeddedNamespace_TypeOfAssembly_RewritesToInitializeEmbeddedResources()
		{
			// When the assembly arg is `typeof(X).Assembly` and the literal starts with
			// the resolved assembly name, strip the prefix and emit `relativePath:`.
			// Defaults aside, the rewrite uses Testably's
			// `InitializeEmbeddedResourcesFromAssembly` extension, so a `using
			// Testably.Abstractions.Testing;` is added without disturbing the existing
			// TestingHelpers using (the receiver type stays bound to TestableIO so
			// other call sites in the file keep compiling).
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> {|#0:fs.AddFilesFromEmbeddedNamespace("/data", typeof(C).Assembly, "TestProject.TestData")|};
				}
				""";

			const string fixedSource = """
				using System.IO.Abstractions.TestingHelpers;
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> fs.InitializeEmbeddedResourcesFromAssembly("/data", typeof(C).Assembly, relativePath: "TestData");
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddFilesFromEmbeddedNamespace_NestedNamespace_PreservesPath()
		{
			// Multi-segment relative path: dots are converted to forward slashes so
			// Testably's path-style `relativePath` matcher consumes them correctly.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> {|#0:fs.AddFilesFromEmbeddedNamespace("/data", typeof(C).Assembly, "TestProject.TestData.Sub.Inner")|};
				}
				""";

			const string fixedSource = """
				using System.IO.Abstractions.TestingHelpers;
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> fs.InitializeEmbeddedResourcesFromAssembly("/data", typeof(C).Assembly, relativePath: "TestData/Sub/Inner");
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddFilesFromEmbeddedNamespace_LiteralEqualsAssemblyName_OmitsRelativePath()
		{
			// Literal == assembly name (or assembly-name + ".") means "all resources" in
			// TestableIO. Equivalent in Testably is calling without `relativePath`, so the
			// rewrite drops that argument entirely.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> {|#0:fs.AddFilesFromEmbeddedNamespace("/data", typeof(C).Assembly, "TestProject")|};
				}
				""";

			const string fixedSource = """
				using System.IO.Abstractions.TestingHelpers;
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> fs.InitializeEmbeddedResourcesFromAssembly("/data", typeof(C).Assembly);
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddFilesFromEmbeddedNamespace_GetExecutingAssembly_RewritesToInitializeEmbeddedResources()
		{
			// `Assembly.GetExecutingAssembly()` resolves to the compilation's own
			// assembly. The analyzer reads `Compilation.AssemblyName` and strips the
			// prefix the same way it does for `typeof(X).Assembly`.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;
				using System.Reflection;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> {|#0:fs.AddFilesFromEmbeddedNamespace("/data", Assembly.GetExecutingAssembly(), "TestProject.TestData")|};
				}
				""";

			const string fixedSource = """
				using System.IO.Abstractions.TestingHelpers;
				using System.Reflection;
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> fs.InitializeEmbeddedResourcesFromAssembly("/data", Assembly.GetExecutingAssembly(), relativePath: "TestData");
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task AddFilesFromEmbeddedNamespace_LiteralPrefixMismatch_HasNoFix()
		{
			// Literal does not start with the resolved assembly name. The user might be
			// reaching into a different assembly's resource graph; without static
			// confirmation that the prefix is correct, the analyzer cannot strip safely.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
						=> {|#0:fs.AddFilesFromEmbeddedNamespace("/data", typeof(C).Assembly, "OtherAssembly.TestData")|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Fact]
		public async Task AddFilesFromEmbeddedNamespace_NonLiteralPath_HasNoFix()
		{
			// The third argument is not a string literal — could be a const reference,
			// concatenation, or any expression. The analyzer can't strip a prefix it
			// can't read, so manual review.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs, string ns)
						=> {|#0:fs.AddFilesFromEmbeddedNamespace("/data", typeof(C).Assembly, ns)|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}

		[Fact]
		public async Task AddFilesFromEmbeddedNamespace_InterfaceTypedReceiver_HasNoFix()
		{
			// The Testably target is an extension method on `IFileSystem`. The TestableIO
			// `IMockFileDataAccessor` interface does NOT implement `IFileSystem`, so the
			// rewritten call would not bind. Fall through to manual review.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(IMockFileDataAccessor accessor)
						=> {|#0:accessor.AddFilesFromEmbeddedNamespace("/data", typeof(C).Assembly, "TestProject.TestData")|};
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}
	}
}
