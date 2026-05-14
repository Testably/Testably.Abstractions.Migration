// This file intentionally instantiates both libraries side-by-side for cross-library
// parity checks. The TestableIO usages are permanent — they cannot be migrated away
// without losing the comparison — so the migration analyzer is suppressed for the file.
#pragma warning disable TestablyAbstractionsMigration001

using System.IO;
using TestableIo = System.IO.Abstractions.TestingHelpers;
using TestablyAbstractions = Testably.Abstractions.Testing;

namespace Testably.Abstractions.Migration.Example.Tests;

/// <summary>
///     End-to-end example tests that show realistic before/after pairs for the
///     <c>System.IO.Abstractions.TestingHelpers</c> &rarr; <c>Testably.Abstractions.Testing</c>
///     migration.
/// </summary>
public class SystemIOAbstractionsMigrationExamples
{
	/// <summary>
	///     Phase 1 risk-#1 sentinel. A rooted Unix-style path on Windows must round-trip
	///     identically against both libraries through the parameterless constructor that the
	///     Phase 1 code fix preserves verbatim. Any future divergence (case sensitivity,
	///     drive-letter handling, separator normalization) will surface here.
	/// </summary>
	[Theory]
	[InlineData("/etc/hosts", "/etc")]
	[InlineData("/var/log/syslog", "/var/log")]
	public async Task UnixStylePath_ParameterlessCtor_RoundTripsOnBothLibraries(
		string filePath, string parentDirectory)
	{
		const string contents = "127.0.0.1 localhost";

		TestableIo.MockFileSystem testableIo = new();
		testableIo.Directory.CreateDirectory(parentDirectory);
		testableIo.File.WriteAllText(filePath, contents);
		string testableIoResult = testableIo.File.ReadAllText(filePath);

		TestablyAbstractions.MockFileSystem testably = new();
		testably.Directory.CreateDirectory(parentDirectory);
		testably.File.WriteAllText(filePath, contents);
		string testablyResult = testably.File.ReadAllText(filePath);

		await That(testableIoResult).IsEqualTo(contents);
		await That(testablyResult).IsEqualTo(testableIoResult);
	}

	/// <summary>
	///     Mirrors the playground sample <c>MockFileSystemSamples.Parameterless</c>: after the
	///     code fix runs, the same source line resolves to <see cref="TestablyAbstractions.MockFileSystem" />
	///     and still implements <see cref="IFileSystem" />.
	/// </summary>
	[Fact]
	public async Task ParameterlessConstructor_AfterMigration_IsIFileSystem()
	{
		TestablyAbstractions.MockFileSystem fileSystem = new();
		IFileSystem asInterface = fileSystem;

		await That(asInterface).IsNotNull();
	}
}
