using Testably.Abstractions.Migration.Analyzers;
using Verifier =
	Testably.Abstractions.Migration.Tests.Verifiers.CSharpCodeFixVerifier<
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsAnalyzer,
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsCodeFixProvider>;

namespace Testably.Abstractions.Migration.Tests;

public partial class SystemIOAbstractionsCodeFixProviderTests
{
	/// <summary>
	///     End-to-end tests that exercise the custom <c>SystemIOAbstractionsFixAllProvider</c>.
	///     Each test puts multiple diagnostics in one source file so the test framework's
	///     fix-all verification (Document / Project / Solution scope) runs the provider.
	///     The default <c>WellKnownFixAllProviders.BatchFixer</c> drops fixes whose text
	///     changes overlap on the using line, and once the first using swap lands the
	///     analyzer no longer fires on remaining constructors — these cases would fail
	///     to migrate fully under BatchFixer.
	/// </summary>
	public sealed class FixAllTests
	{
		[Fact]
		public async Task MultipleDefaultConstructors_InSameDocument_ShouldAllMigrate()
		{
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public MockFileSystem Build1() => {|#0:new MockFileSystem()|};
					public MockFileSystem Build2() => {|#1:new MockFileSystem()|};
					public MockFileSystem Build3() => {|#2:new MockFileSystem()|};
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public MockFileSystem Build1() => new MockFileSystem();
					public MockFileSystem Build2() => new MockFileSystem();
					public MockFileSystem Build3() => new MockFileSystem();
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				[
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(1),
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(2),
				],
				fixedSource);
		}

		[Fact]
		public async Task ConstructorPlusAccessorCalls_OnSameMockFileSystem_ShouldAllMigrate()
		{
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Seed()
					{
						MockFileSystem fs = {|#0:new MockFileSystem()|};
						{|#1:fs.AddDirectory("/b")|};
						{|#2:fs.AddFile("/a", new MockFileData("x"))|};
					}
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public void Seed()
					{
						MockFileSystem fs = new MockFileSystem();
						fs.Directory.CreateDirectory("/b");
						fs.File.WriteAllText("/a", "x");
					}
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				[
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(1),
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(2),
				],
				fixedSource);
		}

		[Fact]
		public async Task MixedPatterns_AcrossMultipleMethods_ShouldAllMigrateInOnePass()
		{
			const string source = """
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void First()
					{
						MockFileSystem fs = {|#0:new MockFileSystem()|};
						{|#1:fs.AddFile("/a", new MockFileData("hello"))|};
					}

					public void Second()
					{
						MockFileSystem fs = {|#2:new MockFileSystem()|};
						{|#3:fs.AddDirectory("/dir")|};
					}

					public string Read(MockFileSystem fs) => {|#4:fs.GetFile("/a").TextContents|};
				}
				""";

			const string fixedSource = """
				using Testably.Abstractions.Testing;

				public class C
				{
					public void First()
					{
						MockFileSystem fs = new MockFileSystem();
						fs.File.WriteAllText("/a", "hello");
					}

					public void Second()
					{
						MockFileSystem fs = new MockFileSystem();
						fs.Directory.CreateDirectory("/dir");
					}

					public string Read(MockFileSystem fs) => fs.File.ReadAllText("/a");
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				[
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(1),
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(2),
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(3),
					Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(4),
				],
				fixedSource);
		}
	}
}
