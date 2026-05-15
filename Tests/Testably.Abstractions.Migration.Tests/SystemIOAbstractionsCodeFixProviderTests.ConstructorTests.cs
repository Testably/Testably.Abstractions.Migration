using Testably.Abstractions.Migration.Analyzers;
using Verifier =
	Testably.Abstractions.Migration.Tests.Verifiers.CSharpCodeFixVerifier<
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsAnalyzer,
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsCodeFixProvider>;

namespace Testably.Abstractions.Migration.Tests;

public partial class SystemIOAbstractionsCodeFixProviderTests
{
	public sealed class ConstructorTests
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
		public async Task FilesConstructor_DictionaryEntryWithAttributes_ExpandsSetAttributesFollowUp()
		{
			const string source = """
				using System.Collections.Generic;
				using System.IO;
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run()
					{
						var fs = {|#0:new MockFileSystem(new Dictionary<string, MockFileData>
						{
							["/foo"] = new MockFileData("hello") { Attributes = FileAttributes.ReadOnly },
						})|};
					}
				}
				""";

			const string fixedSource = """
				using System.Collections.Generic;
				using System.IO;
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run()
					{
						var fs = new MockFileSystem();
						fs.File.WriteAllText("/foo", "hello");
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
		public async Task FilesConstructor_NonRootParent_EmitsCreateDirectoryFollowUp()
		{
			// The `Dictionary<string, MockFileData>` constructor auto-creates each
			// entry's parent directory. The Testably API's WriteAllText does not, so
			// the fixer emits a CreateDirectory call per unique non-root parent.
			const string source = """
				using System.Collections.Generic;
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run()
					{
						var fs = {|#0:new MockFileSystem(new Dictionary<string, MockFileData>
						{
							["/etc/hosts"] = new MockFileData("127.0.0.1 localhost"),
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
						fs.Directory.CreateDirectory("/etc");
						fs.File.WriteAllText("/etc/hosts", "127.0.0.1 localhost");
					}
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task FilesConstructor_SharedParent_EmitsCreateDirectoryOnce()
		{
			// Two entries share the same parent — only one CreateDirectory is emitted.
			const string source = """
				using System.Collections.Generic;
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run()
					{
						var fs = {|#0:new MockFileSystem(new Dictionary<string, MockFileData>
						{
							["/work/a.txt"] = new MockFileData("a"),
							["/work/b.txt"] = new MockFileData("b"),
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
						fs.Directory.CreateDirectory("/work");
						fs.File.WriteAllText("/work/a.txt", "a");
						fs.File.WriteAllText("/work/b.txt", "b");
					}
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				fixedSource);
		}

		[Fact]
		public async Task FilesConstructor_NonLiteralKey_SkipsCreateDirectory()
		{
			// Non-literal keys can't be resolved at fix time. The fixer emits the
			// write call but leaves parent-directory creation to the user.
			const string source = """
				using System.Collections.Generic;
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(string path)
					{
						var fs = {|#0:new MockFileSystem(new Dictionary<string, MockFileData>
						{
							[path] = new MockFileData("hello"),
						})|};
					}
				}
				""";

			const string fixedSource = """
				using System.Collections.Generic;
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Run(string path)
					{
						var fs = new MockFileSystem();
						fs.File.WriteAllText(path, "hello");
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
		public async Task MockFileSystemSubclass_HasNoFix()
		{
			// User-defined MockFileSystem subclasses don't have a Testably equivalent —
			// inheritance hooks differ across libraries. The analyzer flags the class
			// declaration; the code-fix provider intentionally falls through with no
			// rewrite.
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class {|#0:MyFs|} : MockFileSystem
				{
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}
	}
}
