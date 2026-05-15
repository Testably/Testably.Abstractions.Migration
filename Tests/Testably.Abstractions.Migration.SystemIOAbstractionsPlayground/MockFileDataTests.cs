using System.IO;
using System.IO.Abstractions.TestingHelpers;

namespace Testably.Abstractions.Migration.SystemIOAbstractionsPlayground;

/// <summary>MockFileData property reads and writes against one-shot GetFile(path).Prop chains.</summary>
public class MockFileDataTests
{
	[Fact]
	public async Task PropertyRead_Attributes_ReturnsStoredAttributes()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello") { Attributes = FileAttributes.ReadOnly });

		FileAttributes attrs = fs.GetFile("/a").Attributes;

		await That(attrs).IsEqualTo(FileAttributes.ReadOnly);
	}

	[Fact]
	public async Task PropertyRead_Contents_ReturnsStoredBytes()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData(new byte[] { 0x01, 0x02 }));

		byte[] bytes = fs.GetFile("/a").Contents;

		await That(bytes).IsEqualTo(new byte[] { 0x01, 0x02 });
	}

	[Fact]
	public async Task PropertyRead_CreationTime_ReturnsAssignableTime()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello"));

		DateTimeOffset creationTime = fs.GetFile("/a").CreationTime;

		await That(creationTime).IsNotEqualTo(default(DateTimeOffset));
	}

	[Fact]
	public async Task PropertyRead_LastAccessTime_ReturnsAssignableTime()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello"));

		DateTimeOffset accessTime = fs.GetFile("/a").LastAccessTime;

		await That(accessTime).IsNotEqualTo(default(DateTimeOffset));
	}

	[Fact]
	public async Task PropertyRead_LastWriteTime_ReturnsAssignableTime()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello"));

		DateTimeOffset writeTime = fs.GetFile("/a").LastWriteTime;

		await That(writeTime).IsNotEqualTo(default(DateTimeOffset));
	}

	[Fact]
	public async Task PropertyRead_TextContents_ReturnsStoredText()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello"));

		string text = fs.GetFile("/a").TextContents;

		await That(text).IsEqualTo("hello");
	}

	[Fact]
	public async Task PropertyWrite_Attributes_SetsAttributes()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello"));

		fs.GetFile("/a").Attributes = FileAttributes.ReadOnly;

		await That(fs.File.GetAttributes("/a")).IsEqualTo(FileAttributes.ReadOnly);
	}

	[Fact]
	public async Task PropertyWrite_TextContents_OverwritesStoredText()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello"));

		fs.GetFile("/a").TextContents = "world";

		await That(fs.File.ReadAllText("/a")).IsEqualTo("world");
	}

	[Fact]
	public async Task CapturedReference_ReadsAndWritesViaLocal()
	{
		// Phase 4c manual-review fixture: GetFile result is captured into a local before
		// any property access, so the analyzer flags each access as a captured reference
		// rather than a migratable one-shot chain.
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello") { Attributes = FileAttributes.Normal });
		MockFileData data = fs.GetFile("/a");

		string before = data.TextContents;
		data.Attributes = FileAttributes.ReadOnly;

		await That(before).IsEqualTo("hello");
		await That(fs.File.GetAttributes("/a")).IsEqualTo(FileAttributes.ReadOnly);
	}
}
