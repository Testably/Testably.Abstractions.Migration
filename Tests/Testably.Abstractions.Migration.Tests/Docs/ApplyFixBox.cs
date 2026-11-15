using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Testably.Abstractions.Migration.Tests.Docs;

/// <summary>
///     An <c>ApplyFix</c> component of a documentation page, whose opening tag is on the one-based
///     <paramref name="Line" />: the code <paramref name="Before" /> and <paramref name="After" /> the code fix for the
///     diagnostic <paramref name="Id" />, without the highlight comment lines.
/// </summary>
internal sealed partial record ApplyFixBox(string Page, int Line, string Id, string Before, string After)
{
	/// <summary>
	///     Extracts the <c>ApplyFix</c> components from the <paramref name="lines" /> of the <paramref name="page" />.
	/// </summary>
	public static List<ApplyFixBox> Extract(string page, string[] lines)
	{
		List<ApplyFixBox> boxes = [];
		for (int i = 0; i < lines.Length; i++)
		{
			Match opening = Opening().Match(lines[i]);
			if (!opening.Success)
			{
				continue;
			}

			int line = i + 1;
			List<string> blocks = [];
			while (++i < lines.Length && !Closing().IsMatch(lines[i]))
			{
				i = ReadCSharpBlock(lines, i, blocks);
			}

			if (blocks.Count != 2)
			{
				throw new InvalidOperationException(
					$"{page}({line}): an ApplyFix needs exactly two csharp blocks, but has {blocks.Count}.");
			}

			boxes.Add(new ApplyFixBox(page, line, opening.Groups["id"].Value, blocks[0], blocks[1]));
		}

		return boxes;
	}

	/// <summary>
	///     Adds the code of the <c>```csharp</c> block that opens at <paramref name="start" /> to the
	///     <paramref name="blocks" /> and returns the index of its closing fence.
	/// </summary>
	private static int ReadCSharpBlock(string[] lines, int start, List<string> blocks)
	{
		Match fence = Fence().Match(lines[start]);
		if (!fence.Success)
		{
			return start;
		}

		int end = start + 1;
		while (end < lines.Length && lines[end].Trim() != fence.Groups["fence"].Value)
		{
			end++;
		}

		if (fence.Groups["info"].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries) is ["csharp", ..,])
		{
			blocks.Add(string.Join("\n", lines.Skip(start + 1).Take(end - start - 1)
				.Where(line => !Highlight().IsMatch(line))));
		}

		return end;
	}

	[GeneratedRegex("""^\s*</ApplyFix>""")]
	private static partial Regex Closing();

	[GeneratedRegex(@"^\s*(?<fence>`{3,})(?<info>.*)$")]
	private static partial Regex Fence();

	[GeneratedRegex(@"^\s*//\s*highlight-(next-line|start|end)\s*$")]
	private static partial Regex Highlight();

	[GeneratedRegex("""^\s*<ApplyFix\b[^>]*\bid="(?<id>[^"]*)"[^>]*>""")]
	private static partial Regex Opening();
}
