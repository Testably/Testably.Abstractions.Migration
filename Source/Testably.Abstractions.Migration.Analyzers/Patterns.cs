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

	/// <summary>
	///     <c>fs.AddDrive(name, mockDriveData)</c>; rewrites to
	///     <c>fs.WithDrive(name, d =&gt; d.SetTotalSize(...).SetIsReady(...))</c>.
	/// </summary>
	public const string MockFileSystemAddDrive = "MockFileSystem.AddDrive";

	/// <summary>
	///     <c>fs.MockTime(Func&lt;DateTime&gt;)</c>. Manual review (Phase 5.3): TestableIO
	///     calls the supplied delegate on every timestamp request, while Testably installs
	///     a fixed-then-mutable <c>MockTimeSystem</c> at construction. The two have no
	///     observably-equivalent automatic rewrite for arbitrary delegates, and the
	///     equivalent surface (<c>o =&gt; o.UseTimeSystem(...)</c>) lives in the
	///     <c>MockFileSystemOptions</c> lambda — a cross-statement fold that conflicts
	///     with the parameterless / options-ctor fixes when both touch the construction.
	///     A future sub-phase may add an opt-in fix for the narrow constant-DateTime
	///     lambda shape with a custom <c>FixAllProvider</c>.
	/// </summary>
	public const string MockFileSystemMockTime = "MockFileSystem.MockTime";

	/// <summary>
	///     <c>fs.AddFileFromEmbeddedResource(path, assembly, embeddedResourcePath)</c>.
	///     Manual review (Phase 5.2): Testably exposes only a bulk
	///     <c>InitializeEmbeddedResourcesFromAssembly(directoryPath, assembly,
	///     relativePath, searchPattern, searchOption)</c> with no single-file overload,
	///     and uses a path-style match (auto-strips the assembly-name prefix and
	///     replaces dots with directory separators) instead of TestableIO's literal
	///     dot-prefix match. A naive textual rewrite would compile but materialize a
	///     different resource set, so the call site is reported for manual migration.
	/// </summary>
	public const string MockFileSystemAddFileFromEmbeddedResource =
		"MockFileSystem.AddFileFromEmbeddedResource";

	/// <summary>
	///     <c>fs.AddFilesFromEmbeddedNamespace(path, assembly, embeddedResourcePath)</c>.
	///     Phase 5.2: when the assembly arg resolves statically (<c>typeof(X).Assembly</c>
	///     or <c>Assembly.GetExecutingAssembly()</c>) and the third arg is a string
	///     literal that starts with the resolved assembly name, the code-fix rewrites to
	///     Testably's <c>InitializeEmbeddedResourcesFromAssembly</c>. Otherwise the call
	///     site is left for manual review.
	/// </summary>
	public const string MockFileSystemAddFilesFromEmbeddedNamespace =
		"MockFileSystem.AddFilesFromEmbeddedNamespace";

	// ── Enumeration properties (Phase 5.1) ────────────────────────────────
	// These IMockFileDataAccessor properties enumerate the whole mocked file
	// system. Testably has no direct equivalent — the natural replacements
	// (Directory.EnumerateFiles/EnumerateDirectories, DriveInfo.GetDrives,
	// etc.) need a root path or drive scope the analyzer cannot infer safely.
	// Each property gets its own pattern id so manual migration is
	// discoverable per call site; the code-fix provider intentionally
	// registers no rewrite.

	/// <summary><c>fs.AllPaths</c> — union of files and directories across the mocked file system.</summary>
	public const string MockFileSystemAllPaths = "MockFileSystem.AllPaths";

	/// <summary><c>fs.AllFiles</c> — every mocked file path.</summary>
	public const string MockFileSystemAllFiles = "MockFileSystem.AllFiles";

	/// <summary><c>fs.AllDirectories</c> — every mocked directory path.</summary>
	public const string MockFileSystemAllDirectories = "MockFileSystem.AllDirectories";

	/// <summary><c>fs.AllDrives</c> — every mocked drive name.</summary>
	public const string MockFileSystemAllDrives = "MockFileSystem.AllDrives";

	// ── MockFileData property access ──────────────────────────────────────

	/// <summary>A read access to a <c>MockFileData</c> property.</summary>
	public const string MockFileDataPropertyRead = "MockFileData.propertyRead";

	/// <summary>A write/assignment to a <c>MockFileData</c> property.</summary>
	public const string MockFileDataPropertyWrite = "MockFileData.propertyWrite";

	// ── Manual-review patterns (Phase 4) ──────────────────────────────────
	// These call sites have no automatic rewrite because Testably.Abstractions
	// has no equivalent surface for the captured concept. The analyzer flags
	// them with a discriminating pattern id so the user can locate and address
	// each manually; the code-fix provider intentionally registers no fix.

	// Phase 4a: lossy MockFileData properties + MockFileVersionInfo.

	/// <summary><c>MockFileData.AccessControl</c> — Windows-only FileSecurity has no Testably equivalent.</summary>
	public const string MockFileDataAccessControl = "MockFileData.AccessControl";

	/// <summary><c>MockFileData.AllowedFileShare</c> — file-share locking has no Testably equivalent.</summary>
	public const string MockFileDataAllowedFileShare = "MockFileData.AllowedFileShare";

	/// <summary><c>MockFileData.UnixMode</c> — Unix file permissions have no Testably equivalent.</summary>
	public const string MockFileDataUnixMode = "MockFileData.UnixMode";

	/// <summary><c>new MockFileVersionInfo(...)</c> — file version metadata has no Testably equivalent.</summary>
	public const string MockFileVersionInfoConstructor = "MockFileVersionInfo.ctor";

	// Phase 4b: subclasses + MockFileData copy constructor.

	/// <summary>A user-defined class derives from <c>MockFileSystem</c>; the inheritance contract differs in Testably.</summary>
	public const string MockFileSystemSubclass = "MockFileSystem.subclass";

	/// <summary>A user-defined class derives from <c>MockFileData</c>; there is no Testably equivalent.</summary>
	public const string MockFileDataSubclass = "MockFileData.subclass";

	/// <summary><c>new MockFileData(MockFileData template)</c> — clone semantics differ; no Testably equivalent.</summary>
	public const string MockFileDataCopyConstructor = "MockFileData.copyCtor";

	/// <summary>
	///     A read access to a <c>MockFileData</c> property whose receiver is a captured
	///     reference (local, parameter, field, etc.) rather than a one-shot
	///     <c>fs.GetFile(path)</c> invocation — no safe textual rewrite without flow
	///     analysis.
	/// </summary>
	public const string MockFileDataCapturedReferenceRead = "MockFileData.capturedReferenceRead";

	/// <summary>
	///     A write/assignment to a <c>MockFileData</c> property whose receiver is a
	///     captured reference rather than a one-shot <c>fs.GetFile(path)</c> invocation
	///     — no safe textual rewrite without flow analysis.
	/// </summary>
	public const string MockFileDataCapturedReferenceWrite = "MockFileData.capturedReferenceWrite";
}
