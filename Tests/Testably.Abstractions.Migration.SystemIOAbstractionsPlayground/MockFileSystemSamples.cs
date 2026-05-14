// Playground samples deliberately exercise the un-migrated API surface so the analyzer
// and fixer can be developed against them. The fixer-parity check runs over this file's
// content via the code-fix pipeline, not the normal build, so the in-source diagnostic
// is suppressed here to keep static-analysis dashboards quiet.
#pragma warning disable TestablyAbstractionsMigration001

using System.Text;
using System.IO.Abstractions.TestingHelpers;

namespace Testably.Abstractions.Migration.SystemIOAbstractionsPlayground;

/// <summary>
///     Sample call sites that exercise the <c>System.IO.Abstractions.TestingHelpers</c> API.
///     The analyzer in this solution flags every supported pattern here so the accompanying
///     code fix can be exercised against the file via the parity check.
/// </summary>
public class MockFileSystemSamples
{
	// Phase 1: parameterless `new MockFileSystem()`.
	public static IFileSystem Parameterless()
	{
		MockFileSystem fileSystem = new();
		return fileSystem;
	}

	// Phase 2: MockFileSystem(IDictionary<string, MockFileData>) — dictionary-of-files
	// constructor, expanded statement-by-statement at the call site.
	public static void FilesConstructor()
	{
		var fs = new MockFileSystem(new Dictionary<string, MockFileData>
		{
			["/etc/hosts"] = new MockFileData("127.0.0.1 localhost"),
		});
	}

	// Phase 2: MockFileSystem(IDictionary, string currentDirectory) — folded into a
	// UseCurrentDirectory options lambda.
	public static void FilesConstructorWithCurrentDirectory()
	{
		var fs = new MockFileSystem(new Dictionary<string, MockFileData>
		{
			["/etc/hosts"] = new MockFileData("127.0.0.1 localhost"),
		}, "/work");
	}

	// Phase 2: MockFileSystem(MockFileSystemOptions) — literal options with
	// CurrentDirectory translates to an options lambda.
	public static IFileSystem OptionsConstructor()
	{
		return new MockFileSystem(new MockFileSystemOptions { CurrentDirectory = "/work" });
	}

	// Phase 2: MockFileSystem(IDictionary, MockFileSystemOptions) — both folded.
	public static void FilesOptionsConstructor(byte[] bytes)
	{
		var fs = new MockFileSystem(new Dictionary<string, MockFileData>
		{
			["/etc/hosts"] = new MockFileData("127.0.0.1 localhost"),
			["/etc/binary"] = new MockFileData(bytes),
			["/etc/utf8"] = new MockFileData("encoded", Encoding.UTF8),
		}, new MockFileSystemOptions { CurrentDirectory = "/work" });
	}

	// Phase 2: 1:1 accessor methods on MockFileSystem (IMockFileDataAccessor surface).
	public static bool AccessorMethods(MockFileSystem fs)
	{
		fs.AddDirectory("/a");
		fs.AddFile("/a/text.txt", new MockFileData("hello"));
		fs.AddFile("/a/bytes.bin", new MockFileData(new byte[] { 0x01, 0x02, }));
		fs.AddEmptyFile("/a/empty.txt");
		fs.MoveDirectory("/a", "/b");
		fs.RemoveFile("/b/text.txt");
		return fs.FileExists("/b/bytes.bin");
	}
}
