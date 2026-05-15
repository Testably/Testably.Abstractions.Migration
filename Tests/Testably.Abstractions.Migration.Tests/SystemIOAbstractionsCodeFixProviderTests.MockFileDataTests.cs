using Testably.Abstractions.Migration.Analyzers;
using Verifier =
	Testably.Abstractions.Migration.Tests.Verifiers.CSharpCodeFixVerifier<
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsAnalyzer,
		Testably.Abstractions.Migration.Analyzers.SystemIOAbstractionsCodeFixProvider>;

namespace Testably.Abstractions.Migration.Tests;

public partial class SystemIOAbstractionsCodeFixProviderTests
{
	public sealed class MockFileDataTests
	{
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
		public async Task MockFileDataAccess_CompoundAssignment_HasNoFix()
		{
			// `|=` is a compound assignment. The read-side rewrite would put a getter call
			// on the LHS (`fs.File.GetAttributes(p) |= ...`), which is not assignable. The
			// write-side fix only handles simple `=`, so neither fix should run.
			const string source = """
				using System.IO;
				using System.IO.Abstractions.TestingHelpers;

				public class C
				{
					public void Run(MockFileSystem fs)
					{
						{|#0:fs.GetFile("/a").Attributes|} |= FileAttributes.ReadOnly;
					}
				}
				""";

			await Verifier.VerifyCodeFixAsync(
				source,
				Verifier.Diagnostic(Rules.SystemIOAbstractionsRule).WithLocation(0),
				source);
		}
	}
}
