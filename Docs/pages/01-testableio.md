---
description: How the Testably.Abstractions.Migration code fix rewrites TestableIO MockFileSystem tests to the Testably.Abstractions MockFileSystem.
---

import ApplyFix from '@site/src/components/ApplyFix';

# Migrating from TestableIO

The `TestablyM001` code fix swaps the `using` directive from `System.IO.Abstractions.TestingHelpers` to `Testably.Abstractions.Testing` and rewrites each call on the `MockFileSystem`, so one fix migrates the whole file:

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

## Supported TestableIO constructs

The tables list the constructs the code fix rewrites. Every other construct of `System.IO.Abstractions.TestingHelpers` that the analyzer reports [needs manual review](./00-index.md#manual-review).

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

Reads of `MockFileData` properties (e.g. `fs.GetFile(path).LastWriteTime`) are routed to the matching Testably.Abstractions file-system call; the time properties are read as UTC (e.g. `fs.File.GetLastWriteTimeUtc(path)`). Writes of `TextContents`, `Contents` and `Attributes` become `fs.File.WriteAllText`, `fs.File.WriteAllBytes` and `fs.File.SetAttributes`; writes of the time properties have no fix. The code fix only handles the one-shot `fs.GetFile(path).Prop` shape; property access through a captured reference is left for [manual review](./00-index.md#manual-review).

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
