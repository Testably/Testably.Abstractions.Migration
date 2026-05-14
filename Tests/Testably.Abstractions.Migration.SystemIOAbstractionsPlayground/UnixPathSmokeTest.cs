// Playground samples deliberately exercise the un-migrated API surface so the analyzer
// and fixer can be developed against them. The fixer-parity check runs over this file's
// content via the code-fix pipeline, not the normal build, so the in-source diagnostic
// is suppressed here to keep static-analysis dashboards quiet.
#pragma warning disable TestablyAbstractionsMigration001

using System.IO.Abstractions.TestingHelpers;

namespace Testably.Abstractions.Migration.SystemIOAbstractionsPlayground;

/// <summary>
///     Smoke test for the path-semantics divergence risk identified in Phase 1: rooted
///     Unix-style paths (e.g. <c>/etc/hosts</c>) are accepted by
///     <see cref="MockFileSystem" /> on Windows but may behave differently under
///     <see cref="Testably.Abstractions.Testing.MockFileSystem" />.
/// </summary>
/// <remarks>
///     The class is a runnable executable in the playground. The body deliberately performs
///     a read after a write so the smoke test surfaces any divergence as a test failure
///     rather than a silent regression.
/// </remarks>
public static class UnixPathSmokeTest
{
	public const string UnixStylePath = "/etc/hosts";
	public const string Contents = "127.0.0.1 localhost";

	public static string RoundTrip()
	{
		MockFileSystem fileSystem = new();
		fileSystem.File.WriteAllText(UnixStylePath, Contents);
		return fileSystem.File.ReadAllText(UnixStylePath);
	}
}
