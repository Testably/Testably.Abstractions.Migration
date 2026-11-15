using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Testably.Abstractions.Migration.Tests.Docs;

public sealed class ApplyFixTests
{
	private static readonly string PagesDirectory = GetPagesDirectory();

	public static IEnumerable<string> Pages()
		=> Directory.EnumerateFiles(PagesDirectory, "*.md*", SearchOption.AllDirectories)
			.Select(path => Path.GetRelativePath(PagesDirectory, path).Replace('\\', '/'))
			.Order(StringComparer.Ordinal);

	[Test]
	[MethodDataSource(nameof(Pages))]
	public async Task ApplyFix_ShouldMatchTheCodeFix(string page)
	{
		List<string> mismatches = [];
		foreach (ApplyFixBox box in ExtractBoxes(page))
		{
			string expected = Normalize(CodeFixRunner.Wrap(box.After));
			string actual = Normalize(await CodeFixRunner.ApplyFixes(CodeFixRunner.Wrap(box.Before), box.Id));
			if (expected != actual)
			{
				mismatches.Add($"""
				                {box.Page}({box.Line}): the ApplyFix for {box.Id} does not match the code fix.
				                Expected (page):
				                {expected}
				                Actual (code fix):
				                {actual}
				                """);
			}
		}

		Fail.Unless(mismatches.Count == 0, string.Join(Environment.NewLine, mismatches));
	}

	[Test]
	public async Task Extract_ShouldFindTheApplyFixBoxesOfAllPages()
	{
		int count = Pages().Sum(page => ExtractBoxes(page).Count);

		await That(count).IsGreaterThanOrEqualTo(4)
			.Because("the check must not pass because it no longer finds the ApplyFix boxes");
	}

	[Test]
	public async Task Extract_ShouldReturnTheBeforeAndAfterBlocks()
	{
		string[] lines =
		[
			"```csharp",
			"int outside = 0;",
			"```",
			"<ApplyFix id=\"X001\" migrateTo=\"Y\">",
			"",
			"```csharp title=\"Before\"",
			"int a = 1;",
			"```",
			"",
			"```text",
			"ignored",
			"```",
			"",
			"```csharp title=\"After\"",
			"int b = 2;",
			"int c = 3;",
			"```",
			"",
			"</ApplyFix>",
		];

		List<ApplyFixBox> boxes = ApplyFixBox.Extract("page.md", lines);

		await That(boxes).IsEqualTo([
			new ApplyFixBox("page.md", 4, "X001", "int a = 1;", "int b = 2;\nint c = 3;"),
		]);
	}

	[Test]
	public async Task Extract_ShouldStripTheHighlightCommentLines()
	{
		string[] lines =
		[
			"<ApplyFix id=\"X001\" migrateTo=\"Y\">",
			"```csharp",
			"// highlight-next-line",
			"int a = 1;",
			"// a comment",
			"```",
			"```csharp",
			"  // highlight-start",
			"int b = 2;",
			"  // highlight-end",
			"```",
			"</ApplyFix>",
		];

		List<ApplyFixBox> boxes = ApplyFixBox.Extract("page.md", lines);

		await That(boxes).IsEqualTo([
			new ApplyFixBox("page.md", 1, "X001", "int a = 1;\n// a comment", "int b = 2;"),
		]).Because("only the highlight markers are removed, other comments are part of the code");
	}

	[Test]
	public async Task Extract_WithOneBlock_ShouldThrowInvalidOperationException()
	{
		string[] lines =
		[
			"# Title",
			"<ApplyFix id=\"X001\" migrateTo=\"Y\">",
			"```csharp",
			"int a = 1;",
			"```",
			"</ApplyFix>",
		];

		void Act() => ApplyFixBox.Extract("page.md", lines);

		await That(Act).Throws<InvalidOperationException>()
			.WithMessage("page.md(2): an ApplyFix needs exactly two csharp blocks, but has 1.");
	}

	private static List<ApplyFixBox> ExtractBoxes(string page)
		=> ApplyFixBox.Extract($"Docs/pages/{page}", File.ReadAllLines(Path.Combine(PagesDirectory, page)));

	private static string GetPagesDirectory()
	{
		DirectoryInfo? directory = new(AppContext.BaseDirectory);
		while (directory is not null && !Directory.Exists(Path.Combine(directory.FullName, "Docs", "pages")))
		{
			directory = directory.Parent;
		}

		return directory is null
			? throw new DirectoryNotFoundException($"No Docs/pages above {AppContext.BaseDirectory}")
			: Path.Combine(directory.FullName, "Docs", "pages");
	}

	private static string Normalize(string code)
		=> string.Join("\n", code.Replace("\r\n", "\n").Split('\n').Select(line => line.TrimEnd())).Trim('\n');
}
