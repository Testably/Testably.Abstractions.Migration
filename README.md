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

Install the NuGet package into the project you want to migrate:

```shell
dotnet add package Testably.Abstractions.Migration
```

The package only needs to be referenced while you are migrating — it ships the analyzer and code fixer,
not runtime code. Once `System.IO.Abstractions.TestingHelpers` is gone from a project you can remove the
reference again.

## How it works

After installing the package, every supported construct is reported as a warning. Apply the relevant
code fix from your IDE (Visual Studio, Rider, VS Code with C# Dev Kit) or via
`dotnet format analyzers` to rewrite the call site.

| Diagnostic                       | Source library         | Code fix title                                                  |
|----------------------------------|------------------------|-----------------------------------------------------------------|
| `TestablyAbstractionsMigration001` | System.IO.Abstractions | *Migrate System.IO.Abstractions MockFileSystem to Testably*     |
