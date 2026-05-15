using System.IO;
using System.IO.Abstractions.TestingHelpers;

namespace Testably.Abstractions.Migration.SystemIOAbstractionsPlayground;

/// <summary>
///     Manual-review fixtures (Phase 4a): call sites for <see cref="MockFileData" />
///     properties and <see cref="MockFileVersionInfo" /> construction that have no
///     equivalent in <c>Testably.Abstractions.Testing</c>. The analyzer flags each one
///     with a discriminating pattern id; the code-fix provider intentionally registers
///     no rewrite, so these tests stand as the parity baseline a human can use to plan
///     the migration manually.
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

	private sealed class MyMockFs : MockFileSystem
	{
	}

	private sealed class MyMockFileData : MockFileData
	{
		public MyMockFileData() : base("hello") { }
	}
}
