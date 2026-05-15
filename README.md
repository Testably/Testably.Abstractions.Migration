# Testably.Abstractions.Migration

[![Nuget](https://img.shields.io/nuget/v/Testably.Abstractions.Migration)](https://www.nuget.org/packages/Testably.Abstractions.Migration)
[![Build](https://github.com/Testably/Testably.Abstractions.Migration/actions/workflows/build.yml/badge.svg)](https://github.com/Testably/Testably.Abstractions.Migration/actions/workflows/build.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=Testably_Testably.Abstractions.Migration&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=Testably_Testably.Abstractions.Migration)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Testably_Testably.Abstractions.Migration&metric=coverage)](https://sonarcloud.io/summary/overall?id=Testably_Testably.Abstractions.Migration)

A Roslyn analyzer and code-fix provider that migrates
[System.IO.Abstractions](https://github.com/TestableIO/System.IO.Abstractions) (`TestableIO`) usage of
`MockFileSystem` and `MockFileData` to the
[Testably.Abstractions](https://github.com/Testably/Testably.Abstractions) `MockFileSystem` API. Drop the
package into a project that uses `System.IO.Abstractions.TestingHelpers` and the analyzer flags each
construct it can migrate; the accompanying code fix rewrites the call site.

## Installation

The migration package is a one-shot development tool — install it, migrate, then uninstall. It ships only
the analyzer and code fixer, not runtime code, and is marked as a `DevelopmentDependency` so it never
flows transitively to consumers of your test project.

Because the package does not pull `Testably.Abstractions.Testing` transitively, **you must reference it
yourself** in the project being migrated. Otherwise the rewritten call sites would compile while the
migration package is installed but stop compiling the moment you remove it.

```shell
dotnet add package Testably.Abstractions.Testing
dotnet add package Testably.Abstractions.Migration
```

## Recommended workflow

1. **Reference the target library.** Add `Testably.Abstractions.Testing` to the project you want to
   migrate (see above). Existing `System.IO.Abstractions.TestingHelpers` usage keeps compiling
   side-by-side.
2. **Install the migration package.** Adds the analyzer. Every supported construct is reported as
   warning `TestablyM001`.
3. **Apply the code fix.** Use your IDE (Visual Studio, Rider, VS Code with C# Dev Kit) to fix
   diagnostics one by one, or run `dotnet format analyzers` to apply every available fix in bulk.
4. **Address manual-review diagnostics.** Some patterns have no safe automatic rewrite (see
   [Manual review](#manual-review)). The analyzer reports them so they are discoverable; you migrate
   each call site by hand.
5. **Remove `System.IO.Abstractions.TestingHelpers`.** Once the analyzer is quiet, drop the dependency.
6. **Uninstall the migration package.** It has served its purpose and only adds analyzer overhead
   from here on.

```shell
dotnet remove package System.IO.Abstractions.TestingHelpers
dotnet remove package Testably.Abstractions.Migration
```

## How it works

The analyzer emits a single diagnostic id, `TestablyM001`. Each call site carries a `pattern` property
in `Diagnostic.Properties` that tells the code-fix provider which rewrite to perform. Patterns without
an automatic rewrite still get a `TestablyM001` warning so you can locate them — the code fix just
declines to register an action.

| Diagnostic     | Source library         | Code fix title                                              |
|----------------|------------------------|-------------------------------------------------------------|
| `TestablyM001` | System.IO.Abstractions | *Migrate System.IO.Abstractions MockFileSystem to Testably* |

## Supported migrations

### `MockFileSystem` constructors

| TestableIO                                                | Testably                                                          |
|-----------------------------------------------------------|-------------------------------------------------------------------|
| `new MockFileSystem()`                                    | `new MockFileSystem()`                                            |
| `new MockFileSystem(IDictionary<string, MockFileData>)`   | `new MockFileSystem()` followed by per-entry `Initialize…` calls  |
| `new MockFileSystem(MockFileSystemOptions)`               | `new MockFileSystem(o => o…)` with mapped option setters          |
| `new MockFileSystem(IDictionary<…>, MockFileSystemOptions)` | combined dict expansion + options lambda                        |

### `IMockFileDataAccessor` methods on `MockFileSystem`

| TestableIO                                  | Testably                                                |
|---------------------------------------------|---------------------------------------------------------|
| `fs.AddFile(path, mockFileData)`            | `fs.Initialize().With…(…)` (chain mapped from contents) |
| `fs.AddEmptyFile(path)`                     | `fs.File.Create(path).Dispose()`                        |
| `fs.AddDirectory(path)`                     | `fs.Directory.CreateDirectory(path)`                    |
| `fs.RemoveFile(path)`                       | `fs.File.Delete(path)`                                  |
| `fs.MoveDirectory(source, dest)`            | `fs.Directory.Move(source, dest)`                       |
| `fs.FileExists(path)`                       | `fs.File.Exists(path)`                                  |
| `fs.AddDrive(name, mockDriveData)`          | `fs.WithDrive(name, d => d.Set…(…))` (mapped setters)   |
| `fs.AddFilesFromEmbeddedNamespace(path, assembly, prefix)` | `fs.InitializeEmbeddedResourcesFromAssembly(path, assembly, relativePath: …)` (when the assembly arg resolves statically and the prefix starts with the assembly name) |

### `MockFileData` property access

Reads of `MockFileData` properties (e.g. `fs.GetFile(path).LastWriteTime`) are routed to the
matching Testably file-system call (e.g. `fs.File.GetLastWriteTime(path)`). Writes
(e.g. `fs.GetFile(path).LastWriteTime = value`) become `fs.File.SetLastWriteTime(path, value)`. The
fixer only handles the one-shot `fs.GetFile(path).Prop` shape; property access through a captured
reference is left for manual review (see below).

## Manual review

These call sites are flagged with `TestablyM001` but have no automatic rewrite, either because
`Testably.Abstractions` has no equivalent surface or because a safe rewrite would require flow
analysis the analyzer does not perform. Address each one by hand.

| Pattern                                              | Why manual                                                                                        |
|------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| `MockFileData.AccessControl`                         | Windows `FileSecurity` has no Testably equivalent.                                                |
| `MockFileData.AllowedFileShare`                      | File-share locking has no Testably equivalent.                                                    |
| `MockFileData.UnixMode`                              | Unix file permissions have no Testably equivalent.                                                |
| `new MockFileVersionInfo(...)`                       | File-version metadata has no Testably equivalent.                                                 |
| Subclassing `MockFileSystem` / `MockFileData`        | Inheritance contract differs in Testably.                                                         |
| `new MockFileData(MockFileData template)`            | Copy-clone semantics differ; no Testably equivalent.                                              |
| Captured-reference `MockFileData` property access    | `var data = fs.GetFile(path); data.Prop = …` cannot be rewritten without local flow analysis.     |
| `fs.AllPaths` / `AllFiles` / `AllDirectories` / `AllDrives` | Testably has no enumeration properties; the natural replacements need a root or drive scope the analyzer cannot infer. |
| `fs.MockTime(Func<DateTime>)`                        | TestableIO calls the delegate per timestamp request; Testably installs a fixed-then-mutable `MockTimeSystem` at construction. No observably-equivalent automatic rewrite for arbitrary delegates. |
| `fs.AddFileFromEmbeddedResource(...)`                | Testably exposes only a bulk `InitializeEmbeddedResourcesFromAssembly` with path-style matching; the single-file mapping is not safe to automate. |

## Suppressing the diagnostic

If you choose not to migrate a particular call site, suppress `TestablyM001` per usage with the
standard mechanisms:

```csharp
#pragma warning disable TestablyM001
var fs = new MockFileSystem();
#pragma warning restore TestablyM001
```

or via an `.editorconfig` entry scoped to the file/folder:

```ini
[**/Legacy/**.cs]
dotnet_diagnostic.TestablyM001.severity = none
```
