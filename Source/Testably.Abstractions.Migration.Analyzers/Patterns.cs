namespace Testably.Abstractions.Migration.Analyzers;

/// <summary>
///     Discriminator values for the <c>pattern</c> property carried by every
///     <see cref="Rules.SystemIOAbstractionsRule" /> diagnostic. The accompanying code fix
///     dispatches on this value to pick the appropriate rewrite.
/// </summary>
public static class Patterns
{
	/// <summary>
	///     The key under which the pattern discriminator is stored in
	///     <see cref="Microsoft.CodeAnalysis.Diagnostic.Properties" />.
	/// </summary>
	public const string Key = "pattern";

	// ── Constructors ──────────────────────────────────────────────────────

	/// <summary>The parameterless <c>new MockFileSystem()</c> constructor.</summary>
	public const string MockFileSystemDefaultConstructor = "MockFileSystem.ctor()";

	/// <summary>
	///     <c>new MockFileSystem(IDictionary&lt;string, MockFileData&gt; files, string currentDirectory = "")</c>.
	/// </summary>
	public const string MockFileSystemFilesConstructor = "MockFileSystem.ctor(files)";

	/// <summary><c>new MockFileSystem(MockFileSystemOptions options)</c>.</summary>
	public const string MockFileSystemOptionsConstructor = "MockFileSystem.ctor(options)";

	/// <summary>
	///     <c>new MockFileSystem(IDictionary&lt;string, MockFileData&gt; files, MockFileSystemOptions options)</c>.
	/// </summary>
	public const string MockFileSystemFilesOptionsConstructor = "MockFileSystem.ctor(files,options)";

	// ── IMockFileDataAccessor methods on MockFileSystem ───────────────────

	/// <summary><c>accessor.AddFile(path, mockFileData[, verifyAccess])</c>.</summary>
	public const string AccessorAddFile = "accessor.AddFile";

	/// <summary><c>accessor.AddEmptyFile(path)</c>.</summary>
	public const string AccessorAddEmptyFile = "accessor.AddEmptyFile";

	/// <summary><c>accessor.AddDirectory(path)</c>.</summary>
	public const string AccessorAddDirectory = "accessor.AddDirectory";

	/// <summary><c>accessor.RemoveFile(path[, verifyAccess])</c>.</summary>
	public const string AccessorRemoveFile = "accessor.RemoveFile";

	/// <summary><c>accessor.MoveDirectory(sourcePath, destPath)</c>.</summary>
	public const string AccessorMoveDirectory = "accessor.MoveDirectory";

	/// <summary><c>accessor.FileExists(path)</c>.</summary>
	public const string AccessorFileExists = "accessor.FileExists";

	// ── MockFileData property access ──────────────────────────────────────

	/// <summary>A read access to a <c>MockFileData</c> property.</summary>
	public const string MockFileDataPropertyRead = "MockFileData.propertyRead";

	/// <summary>A write/assignment to a <c>MockFileData</c> property.</summary>
	public const string MockFileDataPropertyWrite = "MockFileData.propertyWrite";

	// ── Manual-review patterns (Phase 4a) ─────────────────────────────────
	// These call sites have no automatic rewrite because Testably.Abstractions
	// has no equivalent surface for the captured concept. The analyzer flags
	// them with a discriminating pattern id so the user can locate and address
	// each manually; the code-fix provider intentionally registers no fix.

	/// <summary><c>MockFileData.AccessControl</c> — Windows-only FileSecurity has no Testably equivalent.</summary>
	public const string MockFileDataAccessControl = "MockFileData.AccessControl";

	/// <summary><c>MockFileData.AllowedFileShare</c> — file-share locking has no Testably equivalent.</summary>
	public const string MockFileDataAllowedFileShare = "MockFileData.AllowedFileShare";

	/// <summary><c>MockFileData.UnixMode</c> — Unix file permissions have no Testably equivalent.</summary>
	public const string MockFileDataUnixMode = "MockFileData.UnixMode";

	/// <summary><c>new MockFileVersionInfo(...)</c> — file version metadata has no Testably equivalent.</summary>
	public const string MockFileVersionInfoConstructor = "MockFileVersionInfo.ctor";
}
