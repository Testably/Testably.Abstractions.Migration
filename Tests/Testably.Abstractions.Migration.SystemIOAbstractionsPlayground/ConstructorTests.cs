using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Text;

namespace Testably.Abstractions.Migration.SystemIOAbstractionsPlayground;

/// <summary>MockFileSystem constructor patterns flagged by the migration analyzer.</summary>
public class ConstructorTests
{
	[Fact]
	public async Task FilesConstructor_SeedsDictionaryEntries()
	{
		MockFileSystem fs = new(new Dictionary<string, MockFileData>
		{
			["/etc/hosts"] = new MockFileData("127.0.0.1 localhost"),
		});

		await That(fs.File.ReadAllText("/etc/hosts")).IsEqualTo("127.0.0.1 localhost");
	}

	[Fact]
	public async Task FilesConstructorWithCurrentDirectory_SeedsAndSetsCwd()
	{
		MockFileSystem fs = new(new Dictionary<string, MockFileData>
		{
			["/etc/hosts"] = new MockFileData("127.0.0.1 localhost"),
		}, "/work");

		await That(fs.Directory.GetCurrentDirectory()).IsEqualTo("/work");
		await That(fs.File.ReadAllText("/etc/hosts")).IsEqualTo("127.0.0.1 localhost");
	}

	[Fact]
	public async Task FilesOptionsConstructor_FoldsOptionsAndExpandsEntries()
	{
		MockFileSystem fs = new(new Dictionary<string, MockFileData>
		{
			["/etc/hosts"] = new MockFileData("127.0.0.1 localhost"),
			["/etc/binary"] = new MockFileData(new byte[] { 0x01, 0x02 }),
			["/etc/utf8"] = new MockFileData("encoded", Encoding.UTF8),
		}, new MockFileSystemOptions { CurrentDirectory = "/work" });

		await That(fs.Directory.GetCurrentDirectory()).IsEqualTo("/work");
		await That(fs.File.ReadAllText("/etc/hosts")).IsEqualTo("127.0.0.1 localhost");
		await That(fs.File.ReadAllBytes("/etc/binary")).IsEqualTo(new byte[] { 0x01, 0x02 });
	}

	[Fact]
	public async Task OptionsConstructor_AppliesCurrentDirectoryFromOptions()
	{
		IFileSystem fs = new MockFileSystem(new MockFileSystemOptions { CurrentDirectory = "/work" });

		await That(fs.Directory.GetCurrentDirectory()).IsEqualTo("/work");
	}

	[Fact]
	public async Task Parameterless_CreatesEmptyFileSystem()
	{
		MockFileSystem fs = new();

		await That(fs.File.Exists("/no-such-file")).IsFalse();
	}
}
