using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Text;

namespace Testably.Abstractions.Migration.Example.Tests;

/// <summary>
///     End-to-end example tests that show realistic before/after pairs for the
///     <c>System.IO.Abstractions.TestingHelpers</c> &rarr; <c>Testably.Abstractions.Testing</c>
///     migration. Applying the code fix to <see cref="BeforeMigration" /> produces code
///     equivalent to <see cref="ExpectedMigrationResult" />.
/// </summary>
public class SystemIOAbstractionsMigrationExamples
{
	[Fact]
	public async Task BeforeMigration()
	{
#pragma warning disable TestablyM001
		// Parameterless constructor → using directive swap.
		MockFileSystem fs = new();

		// Options-only constructor with CurrentDirectory → folded into options lambda.
		MockFileSystem withCurrent = new(new MockFileSystemOptions { CurrentDirectory = "/sandbox" });

		// Files + Options constructor with mixed entries (text / bytes / attribute initializer)
		// → options lambda + per-entry WriteAll* / SetAttributes calls.
		MockFileSystem seeded = new(new Dictionary<string, MockFileData>
		{
			["/work/text.txt"] = new MockFileData("hello"),
			["/work/data.bin"] = new MockFileData(new byte[] { 1, 2, 3 }),
			["/work/readonly.txt"] = new MockFileData("readonly") { Attributes = FileAttributes.ReadOnly },
		}, new MockFileSystemOptions { CurrentDirectory = "/work" });

		// Accessor methods on the IMockFileSystem surface → IFileSystem equivalents.
		fs.AddDirectory("/foo");
		fs.AddEmptyFile("/foo/empty.txt");
		fs.AddFile("/foo/text.txt", new MockFileData("content"));
		fs.AddFile("/foo/encoded.txt", new MockFileData("héllo", Encoding.UTF8));
		fs.AddFile("/foo/data.bin", new MockFileData(new byte[] { 4, 5, 6 }));
		fs.AddFile("/foo/locked.txt", new MockFileData("locked") { Attributes = FileAttributes.ReadOnly });
		fs.MoveDirectory(sourcePath: "/foo", destPath: "/bar");
		bool exists = fs.FileExists("/bar/text.txt");
		fs.RemoveFile("/bar/data.bin", false);

		// MockFileData property reads → File.Read* / File.Get* calls.
		string textContents = seeded.GetFile("/work/text.txt").TextContents;
		byte[] byteContents = seeded.GetFile("/work/data.bin").Contents;
		FileAttributes attributes = seeded.GetFile("/work/readonly.txt").Attributes;
		DateTimeOffset creationTime = seeded.GetFile("/work/text.txt").CreationTime;
		DateTimeOffset lastWriteTime = seeded.GetFile("/work/text.txt").LastWriteTime;
		DateTimeOffset lastAccessTime = seeded.GetFile("/work/text.txt").LastAccessTime;

		// MockFileData property writes → File.WriteAllText / File.SetAttributes calls.
		seeded.GetFile("/work/text.txt").TextContents = "updated";
		seeded.GetFile("/work/readonly.txt").Attributes = FileAttributes.Normal;
#pragma warning restore TestablyM001

		_ = withCurrent;
		_ = creationTime;
		_ = lastWriteTime;
		_ = lastAccessTime;
		await That(exists).IsTrue();
		await That(textContents).IsEqualTo("hello");
		await That(byteContents).IsEqualTo(new byte[] { 1, 2, 3 });
		await That(attributes).IsEqualTo(FileAttributes.ReadOnly);
	}

	[Fact]
	public async Task ExpectedMigrationResult()
	{
		// Parameterless constructor.
		Testably.Abstractions.Testing.MockFileSystem fs = new();

		// Options-only constructor folded into the options lambda.
		Testably.Abstractions.Testing.MockFileSystem withCurrent =
			new(o => o.UseCurrentDirectory("/sandbox"));

		// Files + Options expansion: options lambda + per-entry write/attribute follow-ups.
		// Note: the dictionary constructor auto-created parent directories; the code fix
		// emits write calls only, so any parent directory must be created explicitly
		// after migration.
		Testably.Abstractions.Testing.MockFileSystem seeded =
			new(o => o.UseCurrentDirectory("/work"));
		seeded.Directory.CreateDirectory("/work");
		seeded.File.WriteAllText("/work/text.txt", "hello");
		seeded.File.WriteAllBytes("/work/data.bin", new byte[] { 1, 2, 3 });
		seeded.File.WriteAllText("/work/readonly.txt", "readonly");
		seeded.File.SetAttributes("/work/readonly.txt", FileAttributes.ReadOnly);

		// Accessor methods migrated onto the IFileSystem surface.
		fs.Directory.CreateDirectory("/foo");
		fs.File.Create("/foo/empty.txt").Dispose();
		fs.File.WriteAllText("/foo/text.txt", "content");
		fs.File.WriteAllText("/foo/encoded.txt", "héllo", Encoding.UTF8);
		fs.File.WriteAllBytes("/foo/data.bin", new byte[] { 4, 5, 6 });
		fs.File.WriteAllText("/foo/locked.txt", "locked");
		fs.File.SetAttributes("/foo/locked.txt", FileAttributes.ReadOnly);
		fs.Directory.Move("/foo", "/bar");
		bool exists = fs.File.Exists("/bar/text.txt");
		fs.File.Delete("/bar/data.bin");

		// MockFileData property reads migrated to File.Read*/File.Get* calls. The Get*Utc
		// overloads return DateTime; implicit conversion preserves the original
		// DateTimeOffset locals from the pre-migration source.
		string textContents = seeded.File.ReadAllText("/work/text.txt");
		byte[] byteContents = seeded.File.ReadAllBytes("/work/data.bin");
		FileAttributes attributes = seeded.File.GetAttributes("/work/readonly.txt");
		DateTimeOffset creationTime = seeded.File.GetCreationTimeUtc("/work/text.txt");
		DateTimeOffset lastWriteTime = seeded.File.GetLastWriteTimeUtc("/work/text.txt");
		DateTimeOffset lastAccessTime = seeded.File.GetLastAccessTimeUtc("/work/text.txt");

		// MockFileData property writes migrated to File.WriteAllText / File.SetAttributes.
		seeded.File.WriteAllText("/work/text.txt", "updated");
		seeded.File.SetAttributes("/work/readonly.txt", FileAttributes.Normal);

		_ = withCurrent;
		_ = creationTime;
		_ = lastWriteTime;
		_ = lastAccessTime;
		await That(exists).IsTrue();
		await That(textContents).IsEqualTo("hello");
		await That(byteContents).IsEqualTo(new byte[] { 1, 2, 3 });
		await That(attributes).IsEqualTo(FileAttributes.ReadOnly);
	}
}
