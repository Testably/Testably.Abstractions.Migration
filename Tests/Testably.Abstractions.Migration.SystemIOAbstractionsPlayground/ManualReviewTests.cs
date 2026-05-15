using System.IO;
using System.IO.Abstractions.TestingHelpers;

namespace Testably.Abstractions.Migration.SystemIOAbstractionsPlayground;

/// <summary>
///     Manual-review fixtures (Phase 4): call sites that have no equivalent in
///     <c>Testably.Abstractions.Testing</c>. Phase 4a covers lossy
///     <see cref="MockFileData" /> properties (AccessControl, AllowedFileShare,
///     UnixMode) and <see cref="MockFileVersionInfo" /> construction. Phase 4b adds
///     user-defined subclasses of <see cref="MockFileSystem" /> and
///     <see cref="MockFileData" /> plus the <see cref="MockFileData" /> copy
///     constructor. The analyzer flags each one with a discriminating pattern id; the
///     code-fix provider intentionally registers no rewrite, so these tests stand as
///     the parity baseline a human can use to plan the migration manually.
/// </summary>
public class ManualReviewTests
{
	[Fact]
	public async Task MockFileData_AllowedFileShare_RoundTripsAssignedValue()
	{
		MockFileData data = new("hello") { AllowedFileShare = FileShare.Read };

		await That(data.AllowedFileShare).IsEqualTo(FileShare.Read);
	}

	[Fact]
	public async Task MockFileData_AccessControl_RoundTripsAssignedValue()
	{
		if (!OperatingSystem.IsWindows())
		{
			Assert.Skip("FileSecurity is Windows-only.");
		}

#pragma warning disable CA1416 // Validate platform compatibility
		System.Security.AccessControl.FileSecurity security = new();
		MockFileData data = new("hello") { AccessControl = security };

		await That(data.AccessControl).IsSameAs(security);
#pragma warning restore CA1416
	}

	[Fact]
	public async Task MockFileData_UnixMode_RoundTripsAssignedValue()
	{
		MockFileData data = new("hello") { UnixMode = UnixFileMode.UserRead | UnixFileMode.UserWrite };

		await That(data.UnixMode).IsEqualTo(UnixFileMode.UserRead | UnixFileMode.UserWrite);
	}

	[Fact]
	public async Task MockFileVersionInfo_Constructor_ExposesMetadata()
	{
		MockFileVersionInfo info = new("/a.dll", fileVersion: "1.2.3", productName: "Sample");

		await That(info.FileName).IsEqualTo("/a.dll");
		await That(info.FileVersion).IsEqualTo("1.2.3");
		await That(info.ProductName).IsEqualTo("Sample");
	}

	[Fact]
	public async Task MockFileSystemSubclass_BehavesAsMockFileSystem()
	{
		MyMockFs fs = new();
		fs.AddFile("/a", new MockFileData("hello"));

		await That(fs.File.ReadAllText("/a")).IsEqualTo("hello");
	}

	[Fact]
	public async Task MockFileDataSubclass_BehavesAsMockFileData()
	{
		MyMockFileData data = new();

		await That(data.TextContents).IsEqualTo("hello");
	}

	[Fact]
	public async Task MockFileData_CopyConstructor_ClonesTextContents()
	{
		MockFileData template = new("hello");
		MockFileData clone = new(template);

		await That(clone.TextContents).IsEqualTo("hello");
	}

	[Fact]
	public async Task MockFileSystem_MockTime_ReturnsSelfForFluentChaining()
	{
		// TestableIO calls the supplied delegate every time it needs a timestamp.
		// Testably installs a fixed-then-mutable MockTimeSystem at construction with
		// no equivalent post-construction fluent API, so this site is reported with
		// pattern id `MockFileSystem.MockTime` and left for manual migration. The
		// playground only needs to keep the call shape compiling; the timestamp
		// semantics of MockTime are out of scope for the parity baseline.
		MockFileSystem fs = new();
		MockFileSystem chained = fs.MockTime(() => DateTime.UnixEpoch);

		await That(chained).IsSameAs(fs);
	}

	[Fact]
	public async Task MockFileSystem_AddFileFromEmbeddedResource_MaterializesEmbeddedFile()
	{
		// TestableIO matches the resource name literally; Testably exposes only a
		// bulk InitializeEmbeddedResourcesFromAssembly with no single-file overload.
		// Manual review: pattern id `MockFileSystem.AddFileFromEmbeddedResource`.
		MockFileSystem fs = new();
		fs.AddFileFromEmbeddedResource(
			"/data/sample.txt",
			typeof(ManualReviewTests).Assembly,
			"Testably.Abstractions.Migration.SystemIOAbstractionsPlayground.TestData.sample.txt");

		await That(fs.File.ReadAllText("/data/sample.txt").Trim())
			.IsEqualTo("embedded-resource-content");
	}

	[Fact]
	public async Task MockFileSystem_AddFilesFromEmbeddedNamespace_MaterializesMatchingFiles()
	{
		// TestableIO uses a literal StartsWith on the assembly-qualified resource name,
		// dropping the matched prefix + one separator dot to derive each filename.
		// The Phase 5.2 code-fix rewrites this to Testably's
		// InitializeEmbeddedResourcesFromAssembly when the assembly resolves statically.
		MockFileSystem fs = new();
		fs.AddFilesFromEmbeddedNamespace(
			"/data",
			typeof(ManualReviewTests).Assembly,
			"Testably.Abstractions.Migration.SystemIOAbstractionsPlayground.TestData");

		await That(fs.File.ReadAllText("/data/sample.txt").Trim())
			.IsEqualTo("embedded-resource-content");
	}

	private sealed class MyMockFs : MockFileSystem
	{
	}

	private sealed class MyMockFileData : MockFileData
	{
		public MyMockFileData() : base("hello") { }
	}
}
