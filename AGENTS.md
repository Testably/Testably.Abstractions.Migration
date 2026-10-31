# Testably.Abstractions.Migration Agent Guide

Testably.Abstractions.Migration is a Roslyn analyzer and code fix that migrates tests from the TestableIO `MockFileSystem` to the Testably.Abstractions `MockFileSystem`. See [CONTRIBUTING.md](CONTRIBUTING.md) for contributor rules.

## Documentation

Pages under `Docs/pages` are published on docs.testably.org and follow the [docs style guide](https://github.com/Testably/Testably.Site/blob/main/Docs/style-guide.md). The key rules:

- US English, sentence case for headings and labels, no em dashes, one line per paragraph.
- A `description` in the front matter of every page; the H1 is the title; order with unique `NN-` prefixes.
- Relative file links within the repository, site-absolute `/Library/...` links across libraries.
- Install instructions only through `<PackageInstall package="..." />`, never hand-written commands or NuGet badges.
- Examples check results with `await That(...)`; complete examples use TUnit `[Test]`.

`Docs/pages/00-index.md` is published inside the Testably.Abstractions section (`/Abstractions/migration-from-testableio/Migration/`), so it links to its neighbors with relative paths as they are on the site, e.g. `../../03-file-system/07-access-control.mdx`.
