---
sidebar_label: Migration analyzer
description: A Roslyn analyzer and code fix that rewrites TestableIO MockFileSystem tests to the Testably.Abstractions MockFileSystem.
---

import ApplyFix from '@site/src/components/ApplyFix';
import PackageInstall from '@site/src/components/PackageInstall';

# Testably.Abstractions.Migration

`Testably.Abstractions.Migration` is a Roslyn analyzer with a code fix that automates the call-site rewrites from TestableIO to [Testably.Abstractions](/Abstractions) described in [Migration from TestableIO](../). The code fix rewrites the supported patterns, and the analyzer reports the rest so you can address them by hand.

## Installation

The migration package is a one-shot development tool: install it, migrate, then uninstall. It ships only the analyzer and the code fix, no runtime code, and is marked as a `DevelopmentDependency` so it never flows transitively to consumers of your test project.

Because the package does not pull `Testably.Abstractions.Testing` transitively, **you must reference it yourself** in the project being migrated. Otherwise the rewritten call sites would compile while the migration package is installed but stop compiling the moment you remove it.

<PackageInstall package="Testably.Abstractions.Testing" />

Then add the migration package:

<PackageInstall package="Testably.Abstractions.Migration" developmentDependency />

## Recommended workflow

1. **Reference the target library.** Add `Testably.Abstractions.Testing` to the project you want to migrate (see above). Existing `TestableIO.System.IO.Abstractions.TestingHelpers` usage keeps compiling side-by-side.
2. **Install the migration package.** Adds the analyzer. Every supported construct is reported as warning `TestablyM001`.
3. **Apply the code fix.** Use your IDE (Visual Studio, Rider, VS Code with C# Dev Kit) to apply it file by file, as one fix migrates the whole file, or run `dotnet format analyzers` to apply every available fix in bulk.
4. **Address manual-review diagnostics.** Some patterns have no safe automatic rewrite (see [the patterns that need manual review](#manual-review)). The analyzer reports them so they are discoverable; you migrate each call site by hand.
5. **Remove `TestableIO.System.IO.Abstractions.TestingHelpers`.** Once the analyzer is quiet, drop the dependency (or `System.IO.Abstractions.TestingHelpers`, the older package id that forwards to it).
6. **Uninstall the migration package.** It has served its purpose and only adds analyzer overhead from here on.

```shell
dotnet remove package TestableIO.System.IO.Abstractions.TestingHelpers
dotnet remove package Testably.Abstractions.Migration
```

## How it works

The analyzer reports every call site with the single diagnostic id `TestablyM001`. Patterns without an automatic rewrite are reported as well, so you can locate them; the code fix just offers no rewrite for them.

| Diagnostic     | Source library         | Code fix title                                                            |
|----------------|------------------------|---------------------------------------------------------------------------|
| `TestablyM001` | System.IO.Abstractions | *Migrate System.IO.Abstractions MockFileSystem to Testably.Abstractions* |

## Supported migrations

The code fix swaps the `using` directive and rewrites each call on the `MockFileSystem`, so one fix migrates the whole file:

<ApplyFix id="TestablyM001" migrateTo="Testably.Abstractions">

```csharp title="Before (TestableIO)"
using System.Collections.Generic;
// highlight-next-line
using System.IO.Abstractions.TestingHelpers;

public class TempFileCleanerTests
{
    [Test]
    public async Task Clean_ShouldOnlyDeleteTemporaryFiles()
    {
        // highlight-start
        MockFileSystem fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
        {
            ["/work/report.txt"] = new MockFileData("Quarterly report"),
            ["/work/cache.tmp"] = new MockFileData(new byte[] { 1, 2, 3 }),
        });
        fileSystem.AddEmptyFile("/work/empty.tmp");
        // highlight-end

        new TempFileCleaner(fileSystem).Clean("/work");

        // highlight-start
        await That(fileSystem.FileExists("/work/cache.tmp")).IsFalse();
        await That(fileSystem.FileExists("/work/empty.tmp")).IsFalse();
        await That(fileSystem.GetFile("/work/report.txt").TextContents).IsEqualTo("Quarterly report");
        // highlight-end
    }
}
```

```csharp title="After (Testably.Abstractions)"
using System.Collections.Generic;
// highlight-next-line
using Testably.Abstractions.Testing;

public class TempFileCleanerTests
{
    [Test]
    public async Task Clean_ShouldOnlyDeleteTemporaryFiles()
    {
        // highlight-start
        MockFileSystem fileSystem = new MockFileSystem();
        fileSystem.Directory.CreateDirectory("/work");
        fileSystem.File.WriteAllText("/work/report.txt", "Quarterly report");
        fileSystem.File.WriteAllBytes("/work/cache.tmp", new byte[] { 1, 2, 3 });
        fileSystem.File.Create("/work/empty.tmp").Dispose();
        // highlight-end

        new TempFileCleaner(fileSystem).Clean("/work");

        // highlight-start
        await That(fileSystem.File.Exists("/work/cache.tmp")).IsFalse();
        await That(fileSystem.File.Exists("/work/empty.tmp")).IsFalse();
        await That(fileSystem.File.ReadAllText("/work/report.txt")).IsEqualTo("Quarterly report");
        // highlight-end
    }
}
```

</ApplyFix>

### `MockFileSystem` constructors

| TestableIO                                                | Testably.Abstractions                                             |
|-----------------------------------------------------------|-------------------------------------------------------------------|
| `new MockFileSystem()`                                    | `new MockFileSystem()`                                            |
| `new MockFileSystem(IDictionary<string, MockFileData>)`   | `new MockFileSystem()` followed by `fs.Directory.CreateDirectory(…)` for each parent directory and a write call for each file (only as a local declaration in a block) |
| `new MockFileSystem(MockFileSystemOptions)` with `CurrentDirectory` | `new MockFileSystem(o => o.UseCurrentDirectory(…))` (other options have no fix) |
| `new MockFileSystem(IDictionary<…>, MockFileSystemOptions)` | both of the above                                             |
| `new MockFileSystem(IDictionary<…>, currentDirectory)`    | both of the above, with `o => o.UseCurrentDirectory(currentDirectory)` |

The files of the dictionary become statements after the constructor, and the options become a lambda:

<ApplyFix id="TestablyM001" migrateTo="Testably.Abstractions">

```csharp title="Before (TestableIO)"
using System.Collections.Generic;
using System.IO;
// highlight-next-line
using System.IO.Abstractions.TestingHelpers;

// highlight-start
MockFileSystem fs = new MockFileSystem(new Dictionary<string, MockFileData>
{
    ["/work/config.json"] = new MockFileData("{}") { Attributes = FileAttributes.ReadOnly },
}, new MockFileSystemOptions { CurrentDirectory = "/work" });
// highlight-end
```

```csharp title="After (Testably.Abstractions)"
using System.Collections.Generic;
using System.IO;
// highlight-next-line
using Testably.Abstractions.Testing;

// highlight-start
MockFileSystem fs = new MockFileSystem(o => o.UseCurrentDirectory("/work"));
fs.Directory.CreateDirectory("/work");
fs.File.WriteAllText("/work/config.json", "{}");
fs.File.SetAttributes("/work/config.json", FileAttributes.ReadOnly);
// highlight-end
```

</ApplyFix>

### `IMockFileDataAccessor` methods on `MockFileSystem`

| TestableIO                                  | Testably.Abstractions                                   |
|---------------------------------------------|---------------------------------------------------------|
| `fs.AddFile(path, new MockFileData(text[, encoding]))` | `fs.File.WriteAllText(path, text[, encoding])` |
| `fs.AddFile(path, new MockFileData(bytes))` | `fs.File.WriteAllBytes(path, bytes)`                    |
| `fs.AddFile(path, new MockFileData(…) { Attributes = attributes })` | the write call above, followed by `fs.File.SetAttributes(path, attributes)` (only as a statement in a block) |
| `fs.AddEmptyFile(path)`                     | `fs.File.Create(path).Dispose()`                        |
| `fs.AddDirectory(path)`                     | `fs.Directory.CreateDirectory(path)`                    |
| `fs.RemoveFile(path)`                       | `fs.File.Delete(path)`                                  |
| `fs.MoveDirectory(source, dest)`            | `fs.Directory.Move(source, dest)`                       |
| `fs.FileExists(path)`                       | `fs.File.Exists(path)`                                  |
| `fs.AddDrive(name, mockDriveData)`          | `fs.WithDrive(name, d => d.Set…(…))` (mapped setters)   |
| `fs.AddFilesFromEmbeddedNamespace(path, assembly, prefix)` | `fs.InitializeEmbeddedResourcesFromAssembly(path, assembly, relativePath: …)` (when the assembly arg resolves statically and the prefix starts with the assembly name) |

The contents of the `MockFileData` select the write method, and the drive data becomes a lambda:

<ApplyFix id="TestablyM001" migrateTo="Testably.Abstractions">

```csharp title="Before (TestableIO)"
// highlight-next-line
using System.IO.Abstractions.TestingHelpers;

MockFileSystem fs = new MockFileSystem();
// highlight-start
fs.AddFile("/notes.txt", new MockFileData("Hello"));
fs.AddFile("/image.png", new MockFileData(new byte[] { 0x89, 0x50 }));
fs.AddEmptyFile("/empty.txt");
fs.AddDrive("D:", new MockDriveData { TotalSize = 1024 });
// highlight-end
```

```csharp title="After (Testably.Abstractions)"
// highlight-next-line
using Testably.Abstractions.Testing;

MockFileSystem fs = new MockFileSystem();
// highlight-start
fs.File.WriteAllText("/notes.txt", "Hello");
fs.File.WriteAllBytes("/image.png", new byte[] { 0x89, 0x50 });
fs.File.Create("/empty.txt").Dispose();
fs.WithDrive("D:", d => d.SetTotalSize(1024));
// highlight-end
```

</ApplyFix>

### `MockFileData` property access

Reads of `MockFileData` properties (e.g. `fs.GetFile(path).LastWriteTime`) are routed to the matching Testably.Abstractions file-system call; the time properties are read as UTC (e.g. `fs.File.GetLastWriteTimeUtc(path)`). Writes of `TextContents`, `Contents` and `Attributes` become `fs.File.WriteAllText`, `fs.File.WriteAllBytes` and `fs.File.SetAttributes`; writes of the time properties have no fix. The code fix only handles the one-shot `fs.GetFile(path).Prop` shape; property access through a captured reference is left for [manual review](#manual-review).

A time property is read as UTC, and a written property becomes a write call:

<ApplyFix id="TestablyM001" migrateTo="Testably.Abstractions">

```csharp title="Before (TestableIO)"
// highlight-next-line
using System.IO.Abstractions.TestingHelpers;

MockFileSystem fs = new MockFileSystem();
// highlight-start
DateTimeOffset lastWrite = fs.GetFile("/notes.txt").LastWriteTime;
fs.GetFile("/notes.txt").TextContents = "Updated";
// highlight-end
```

```csharp title="After (Testably.Abstractions)"
// highlight-next-line
using Testably.Abstractions.Testing;

MockFileSystem fs = new MockFileSystem();
// highlight-start
DateTimeOffset lastWrite = fs.File.GetLastWriteTimeUtc("/notes.txt");
fs.File.WriteAllText("/notes.txt", "Updated");
// highlight-end
```

</ApplyFix>

## Manual review

These call sites are flagged with `TestablyM001` but have no automatic rewrite, either because `Testably.Abstractions` has no equivalent surface or because a safe rewrite would require flow analysis the analyzer does not perform. Address each one by hand.

| Pattern                                              | Why manual                                                                                        |
|------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| `MockFileData.AccessControl`                         | Use `SetAccessControl` from the [AccessControl companion package](../../06-companion-libraries/02-access-control.mdx); to deny access, register a strategy with [`WithAccessControlStrategy`](../../03-file-system/07-access-control.mdx). |
| `MockFileData.AllowedFileShare`                      | Testably.Abstractions [honors the `FileShare`](../../03-file-system/09-safe-file-handles.mdx) a file was opened with; keep a stream with the restricted share open instead. |
| `MockFileData.UnixMode`                              | Use `File.SetUnixFileMode`; to enforce the permissions, register a strategy with [`WithUnixFileModeStrategy`](../../03-file-system/08-unix-file-mode.mdx). |
| `new MockFileVersionInfo(...)`                       | Register the version information for a path pattern with [`WithFileVersionInfo`](../../03-file-system/11-file-version-info.mdx). |
| Subclassing `MockFileSystem` / `MockFileData`        | Inheritance contract differs in Testably.Abstractions.                                            |
| `new MockFileData(MockFileData template)`            | Copy-clone semantics differ; no Testably.Abstractions equivalent.                                 |
| Captured-reference `MockFileData` property access    | `var data = fs.GetFile(path); data.Prop = …` cannot be rewritten without local flow analysis.     |
| `fs.AllPaths` / `AllFiles` / `AllDirectories` / `AllDrives` | Testably.Abstractions has no enumeration properties; the natural replacements need a root or drive scope the analyzer cannot infer. |
| `fs.MockTime(Func<DateTime>)`                        | TestableIO calls the delegate per timestamp request; Testably.Abstractions installs a fixed-then-mutable `MockTimeSystem` at construction. No observably-equivalent automatic rewrite for arbitrary delegates. |
| `fs.AddFileFromEmbeddedResource(...)`                | Testably.Abstractions exposes only a bulk `InitializeEmbeddedResourcesFromAssembly` with path-style matching; the single-file mapping is not safe to automate. |

## Suppressing the diagnostic

If you choose not to migrate a particular call site, suppress `TestablyM001` per usage with the standard mechanisms:

```csharp
#pragma warning disable TestablyM001
MockFileSystem fs = new MockFileSystem();
#pragma warning restore TestablyM001
```

or via an `.editorconfig` entry scoped to the file or folder:

```text title=".editorconfig"
[**/Legacy/**.cs]
dotnet_diagnostic.TestablyM001.severity = none
```
