---
description: Switch the tests from TestableIO.System.IO.Abstractions.TestingHelpers to Testably.Abstractions.Testing, automatically with the Testably.Abstractions.Migration code fix or by hand.
---

import PackageInstall from '@site/src/components/PackageInstall';

# Migration

`Testably.Abstractions` and [TestableIO.System.IO.Abstractions](https://github.com/TestableIO/System.IO.Abstractions) share the **same** `IFileSystem` interface, so production code does not need to change when you switch testing libraries. The migration only affects your **test** projects, and there are two ways to do it:

- **Automatically**: the `Testably.Abstractions.Migration` package is a Roslyn analyzer with a code fix that rewrites the supported call sites, and reports the rest so you can address them by hand. This page describes how to use it, and [Migrating from TestableIO](./01-from-testableio.md) shows what the code fix rewrites.
- **By hand**: swap the package and rewrite the call sites yourself (see [Migrating by hand](#migrating-by-hand)).

## When to use which

The shared `IFileSystem` interface means production code is identical either way; this choice only affects tests. The core differentiator: `Testably.Abstractions` runs its full test suite against both `MockFileSystem` and `RealFileSystem` on every supported OS and against each `SimulationMode`, so the mock and the real OS stay in lock-step over time.

Pick **Testably.Abstractions** when you need a watcher, `SafeFileHandle`, multiple drives with size quotas, the `ITimeSystem` / `IRandomSystem` companions, or you want to exercise Linux/macOS/Windows behavior from a single CI job. Pick **TestableIO.System.IO.Abstractions** when you're already using its `MockFileData` / `MockDirectoryData` to inspect or mutate the in-memory store directly; that surface has no equivalent here.

## Installation

The migration package is a one-shot development tool: install it, migrate, then uninstall. It ships only the analyzer and the code fix, no runtime code, and is marked as a `DevelopmentDependency` so it never flows transitively to consumers of your test project.

Because the package does not pull `Testably.Abstractions.Testing` transitively, **you must reference it yourself** in the project being migrated. Otherwise the rewritten call sites would compile while the migration package is installed but stop compiling the moment you remove it.

<PackageInstall package="Testably.Abstractions.Testing" />

Then add the migration package:

<PackageInstall package="Testably.Abstractions.Migration" developmentDependency />

## How it works

The analyzer reports every call site with the single diagnostic id `TestablyM001`. Patterns without an automatic rewrite are reported as well, so you can locate them; the code fix just offers no rewrite for them.

| Diagnostic     | Source library                         | Code fix title                                                            |
|----------------|----------------------------------------|---------------------------------------------------------------------------|
| `TestablyM001` | [TestableIO](./01-from-testableio.md)  | *Migrate System.IO.Abstractions MockFileSystem to Testably.Abstractions* |

What the code fix rewrites, with examples and the supported constructs, is described in [Migrating from TestableIO](./01-from-testableio.md).

## Recommended workflow

1. **Reference the target library.** Add `Testably.Abstractions.Testing` to the project you want to migrate (see [Installation](#installation)). Existing `TestableIO.System.IO.Abstractions.TestingHelpers` usage keeps compiling side-by-side.
2. **Install the migration package.** Every construct the code fix supports and every construct that [needs manual review](#manual-review) is reported as warning `TestablyM001`.
3. **Apply the code fix** in your IDE or from the command line (see below).
4. **Address the call sites that need manual review.** The code fix offers no rewrite for them, so they keep their warning; migrate each one by hand.
5. **Remove TestableIO and the migration package** (see [Removing the package](#removing-the-package)).

### In the IDE

Apply the code fix in your IDE (Visual Studio, Rider, VS Code with C# Dev Kit). One fix migrates the whole file; use *Fix all occurrences* in the project or solution to migrate many files at once.

### From the command line

Apply every available fix in bulk:

```shell
dotnet format analyzers --diagnostics TestablyM001
```

## Manual review

These call sites are flagged with `TestablyM001` but have no automatic rewrite, either because `Testably.Abstractions` has no equivalent surface or because a safe rewrite would require flow analysis the analyzer does not perform. Address each one by hand.

A supported construct that does not meet the conditions in the tables of [Migrating from TestableIO](./01-from-testableio.md), for example `MockFileSystemOptions` other than `CurrentDirectory`, keeps its warning as well.

| Pattern                                              | Why manual                                                                                        |
|------------------------------------------------------|---------------------------------------------------------------------------------------------------|
| `MockFileData.AccessControl`                         | Use `SetAccessControl` from the [AccessControl companion package](../06-companion-libraries/02-access-control.mdx); to deny access, register a strategy with [`WithAccessControlStrategy`](../03-file-system/07-access-control.mdx). |
| `MockFileData.AllowedFileShare`                      | Testably.Abstractions [honors the `FileShare`](../03-file-system/09-safe-file-handles.mdx) a file was opened with; keep a stream with the restricted share open instead. |
| `MockFileData.UnixMode`                              | Use `File.SetUnixFileMode`; to enforce the permissions, register a strategy with [`WithUnixFileModeStrategy`](../03-file-system/08-unix-file-mode.mdx). |
| `new MockFileVersionInfo(...)`                       | Register the version information for a path pattern with [`WithFileVersionInfo`](../03-file-system/11-file-version-info.mdx). |
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

## Removing the package

Once the analyzer is quiet, remove `TestableIO.System.IO.Abstractions.TestingHelpers` (or `System.IO.Abstractions.TestingHelpers`, the older package id that forwards to it), and then the migration package, which only adds analyzer overhead from here on:

```shell
dotnet remove package TestableIO.System.IO.Abstractions.TestingHelpers
dotnet remove package Testably.Abstractions.Migration
```

`Testably.Abstractions.Testing` stays in the project; that is the library the tests now depend on.

## Migrating by hand

If you'd rather not introduce a build-time analyzer, swap the package and rewrite the call sites yourself.

1. **Swap the package:**

   ```xml
   <!-- Remove -->
   <PackageReference Include="TestableIO.System.IO.Abstractions.TestingHelpers" />
   <!-- Add -->
   <PackageReference Include="Testably.Abstractions.Testing" />
   ```

2. **Update the test code:**

   ```csharp
   // Before (TestableIO)
   MockFileSystem fileSystem = new();
   fileSystem.AddDirectory("artworks");
   fileSystem.AddFile("artworks/red-square.txt", new MockFileData("Oil on canvas"));

   // After (Testably), option A: imperative
   MockFileSystem fileSystem = new();
   fileSystem.Directory.CreateDirectory("artworks");
   fileSystem.File.WriteAllText("artworks/red-square.txt", "Oil on canvas");

   // After (Testably), option B: fluent initialization
   MockFileSystem fileSystem = new();
   fileSystem.Initialize()
       .WithSubdirectory("artworks").Initialized(d => d
           .WithFile("red-square.txt").Which(f => f
               .HasStringContent("Oil on canvas")));
   ```

Production code keeps using `IFileSystem` exactly as before; no changes there.
