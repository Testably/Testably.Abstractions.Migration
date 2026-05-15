using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Text;

namespace Testably.Abstractions.Migration.SystemIOAbstractionsPlayground;

/// <summary>1:1 accessor methods on MockFileSystem (IMockFileDataAccessor surface).</summary>
public class AccessorMethodTests
{
	[Fact]
	public async Task AddDirectory_CreatesDirectory()
	{
		MockFileSystem fs = new();
		fs.AddDirectory("/a");

		await That(fs.Directory.Exists("/a")).IsTrue();
	}

	[Fact]
	public async Task AddEmptyFile_CreatesZeroByteFile()
	{
		MockFileSystem fs = new();
		fs.AddEmptyFile("/a/empty.txt");

		await That(fs.File.Exists("/a/empty.txt")).IsTrue();
		await That(fs.File.ReadAllBytes("/a/empty.txt")).IsEmpty();
	}

	[Fact]
	public async Task AddFile_TextContent_StoresText()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a/text.txt", new MockFileData("hello"));

		await That(fs.File.ReadAllText("/a/text.txt")).IsEqualTo("hello");
	}

	[Fact]
	public async Task AddFile_TextContentWithEncoding_StoresEncodedText()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a/utf8.txt", new MockFileData("héllo", Encoding.UTF8));

		await That(fs.File.ReadAllText("/a/utf8.txt", Encoding.UTF8)).IsEqualTo("héllo");
	}

	[Fact]
	public async Task AddFile_ByteContent_StoresBytes()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a/bytes.bin", new MockFileData(new byte[] { 0x01, 0x02 }));

		await That(fs.File.ReadAllBytes("/a/bytes.bin")).IsEqualTo(new byte[] { 0x01, 0x02 });
	}

	[Fact]
	public async Task AddFile_WithAttributesInitializer_AppliesAttributes()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello") { Attributes = FileAttributes.ReadOnly });

		await That(fs.File.ReadAllText("/a")).IsEqualTo("hello");
		await That(fs.File.GetAttributes("/a")).IsEqualTo(FileAttributes.ReadOnly);
	}

	[Fact]
	public async Task FileExists_ReturnsTrueWhenFileWasAdded()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello"));

		await That(fs.FileExists("/a")).IsTrue();
	}

	[Fact]
	public async Task MoveDirectory_MovesContents()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a/text.txt", new MockFileData("hello"));
		fs.MoveDirectory("/a", "/b");

		await That(fs.FileExists("/b/text.txt")).IsTrue();
		await That(fs.FileExists("/a/text.txt")).IsFalse();
	}

	[Fact]
	public async Task MoveDirectory_WithNamedArgs_MovesContents()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a/text.txt", new MockFileData("hello"));
		fs.MoveDirectory(sourcePath: "/a", destPath: "/b");

		await That(fs.FileExists("/b/text.txt")).IsTrue();
	}

	[Fact]
	public async Task RemoveFile_DeletesFile()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello"));
		fs.RemoveFile("/a");

		await That(fs.FileExists("/a")).IsFalse();
	}

	[Fact]
	public async Task RemoveFile_WithVerifyAccess_DeletesFile()
	{
		MockFileSystem fs = new();
		fs.AddFile("/a", new MockFileData("hello"));
		fs.RemoveFile("/a", false);

		await That(fs.FileExists("/a")).IsFalse();
	}
}
