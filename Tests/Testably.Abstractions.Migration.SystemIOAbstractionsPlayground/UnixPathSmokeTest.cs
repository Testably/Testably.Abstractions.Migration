using System.IO.Abstractions.TestingHelpers;

namespace Testably.Abstractions.Migration.SystemIOAbstractionsPlayground;

/// <summary>
///     Smoke test for the path-semantics divergence risk identified in Phase 1: rooted
///     Unix-style paths (e.g. <c>/etc/hosts</c>) are accepted by
///     <see cref="MockFileSystem" /> on Windows but may behave differently under
///     <see cref="Testably.Abstractions.Testing.MockFileSystem" />. The migration code
///     fix does not touch the path strings, so any divergence surfaces here.
/// </summary>
public class UnixPathSmokeTest
{
	[Fact]
	public async Task RoundTrip_OnRootedUnixPath_ReturnsWrittenContents()
	{
		MockFileSystem fs = new();
		fs.Directory.CreateDirectory("/etc");
		fs.File.WriteAllText("/etc/hosts", "127.0.0.1 localhost");

		string contents = fs.File.ReadAllText("/etc/hosts");

		await That(contents).IsEqualTo("127.0.0.1 localhost");
	}
}
