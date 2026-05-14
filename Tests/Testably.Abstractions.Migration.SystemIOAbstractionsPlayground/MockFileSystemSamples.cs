using System.IO.Abstractions.TestingHelpers;

namespace Testably.Abstractions.Migration.SystemIOAbstractionsPlayground;

/// <summary>
///     Sample call sites that exercise the <c>System.IO.Abstractions.TestingHelpers</c> API.
///     The analyzer in this solution should flag the supported patterns here so that the
///     accompanying code fix can be used to migrate them to <c>Testably.Abstractions.Testing</c>.
/// </summary>
public class MockFileSystemSamples
{
	// Phase 1: parameterless `new MockFileSystem()`. The analyzer flags this; the code fix
	// rewrites the file's usings to bind `MockFileSystem` to the Testably namespace.
	public static IFileSystem Parameterless()
	{
		MockFileSystem fileSystem = new();
		return fileSystem;
	}
}
