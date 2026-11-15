# Testably.Abstractions.Migration

[![Nuget](https://img.shields.io/nuget/v/Testably.Abstractions.Migration)](https://www.nuget.org/packages/Testably.Abstractions.Migration)
[![Build](https://github.com/Testably/Testably.Abstractions.Migration/actions/workflows/build.yml/badge.svg)](https://github.com/Testably/Testably.Abstractions.Migration/actions/workflows/build.yml)
[![Quality Gate Status](https://sonarcloud.io/api/project_badges/measure?project=Testably_Testably.Abstractions.Migration&metric=alert_status)](https://sonarcloud.io/summary/new_code?id=Testably_Testably.Abstractions.Migration)
[![Coverage](https://sonarcloud.io/api/project_badges/measure?project=Testably_Testably.Abstractions.Migration&metric=coverage)](https://sonarcloud.io/summary/overall?id=Testably_Testably.Abstractions.Migration)

A Roslyn analyzer with a code fix that migrates [System.IO.Abstractions](https://github.com/TestableIO/System.IO.Abstractions) (`TestableIO`) usage of `MockFileSystem` and `MockFileData` to the [Testably.Abstractions](https://github.com/Testably/Testably.Abstractions) `MockFileSystem` API. Drop the package into a project that uses `TestableIO.System.IO.Abstractions.TestingHelpers`: the code fix rewrites the supported patterns, and the analyzer reports the rest so you can address them by hand.

The documentation is at **[docs.testably.org/Abstractions/migration-from-testableio/Migration](https://docs.testably.org/Abstractions/migration-from-testableio/Migration/)**.

## Installation

The migration package is a one-shot development tool: install it, migrate, then uninstall. It ships only the analyzer and the code fix, no runtime code, and is marked as a `DevelopmentDependency` so it never flows transitively to consumers of your test project.

Because the package does not pull `Testably.Abstractions.Testing` transitively, **you must reference it yourself** in the project being migrated. Otherwise the rewritten call sites would compile while the migration package is installed but stop compiling the moment you remove it.

```shell
dotnet add package Testably.Abstractions.Testing
dotnet add package Testably.Abstractions.Migration
```

## How it works

The analyzer reports every call site with the single diagnostic id `TestablyM001`. Patterns without an automatic rewrite are reported as well, so you can locate them; the code fix just offers no rewrite for them.

| Diagnostic     | Source library | Code fix title                                                            |
|----------------|----------------|---------------------------------------------------------------------------|
| `TestablyM001` | TestableIO     | *Migrate System.IO.Abstractions MockFileSystem to Testably.Abstractions* |

What the code fix rewrites, with examples and the supported constructs, is described in [Migrating from TestableIO](https://docs.testably.org/Abstractions/migration-from-testableio/Migration/testableio).

## Recommended workflow

1. **Reference the target library.** Add `Testably.Abstractions.Testing` to the project you want to migrate (see [Installation](#installation)). Existing `TestableIO.System.IO.Abstractions.TestingHelpers` usage keeps compiling side-by-side.
2. **Install the migration package.** Every construct the code fix supports and every construct that [needs manual review](#manual-review) is reported as warning `TestablyM001`.
3. **Apply the code fix** in your IDE (Visual Studio, Rider, VS Code with C# Dev Kit), where one fix migrates the whole file and *Fix all occurrences* migrates the project or solution, or from the command line with `dotnet format analyzers --diagnostics TestablyM001`.
4. **Address the call sites that need manual review.** The code fix offers no rewrite for them, so they keep their warning; migrate each one by hand.
5. **Remove TestableIO and the migration package** (see [Removing the package](#removing-the-package)).

## Manual review

These call sites are flagged with `TestablyM001` but have no automatic rewrite, either because `Testably.Abstractions` has no equivalent surface or because a safe rewrite would require flow analysis the analyzer does not perform. Address each one by hand.

| Pattern                                              | Why manual                                                                                        |
|------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| `MockFileData.AccessControl`                         | Use `SetAccessControl` from the [AccessControl companion package](https://docs.testably.org/Abstractions/companion-libraries/access-control); to deny access, register a strategy with [`WithAccessControlStrategy`](https://docs.testably.org/Abstractions/file-system/access-control). |
| `MockFileData.AllowedFileShare`                      | Testably.Abstractions [honors the `FileShare`](https://docs.testably.org/Abstractions/file-system/safe-file-handles) a file was opened with; keep a stream with the restricted share open instead. |
| `MockFileData.UnixMode`                              | Use `File.SetUnixFileMode`; to enforce the permissions, register a strategy with [`WithUnixFileModeStrategy`](https://docs.testably.org/Abstractions/file-system/unix-file-mode). |
| `new MockFileVersionInfo(...)`                       | Register the version information for a path pattern with [`WithFileVersionInfo`](https://docs.testably.org/Abstractions/file-system/file-version-info). |
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

```ini
[**/Legacy/**.cs]
dotnet_diagnostic.TestablyM001.severity = none
```

## Removing the package

Once the analyzer is quiet, remove `TestableIO.System.IO.Abstractions.TestingHelpers` (or `System.IO.Abstractions.TestingHelpers`, the older package id that forwards to it), and then the migration package, which only adds analyzer overhead from here on:

```shell
dotnet remove package TestableIO.System.IO.Abstractions.TestingHelpers
dotnet remove package Testably.Abstractions.Migration
```

`Testably.Abstractions.Testing` stays in the project; that is the library the tests now depend on.
