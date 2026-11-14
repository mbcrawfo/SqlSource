# AGENTS.md - tests/SqlSource.Repository.Tests/Documentation

- **`UserDocsLinksTests` checks the links and anchors of `README.md`, `src/SqlSource.Tool/README.md` and every page under `site/` but an `AGENTS.md`, `InboundSiteReferenceTests` every reference to a page of `site/` from the rest of the repository but `docs/superpowers`, `UserDocsSamplesTests` the SQL in their fenced blocks, `StatusLanguageTests` that no user document holds a phrase of status, `SupportedVersionsTests` the table of `site/reference/databases/index.md#supported-versions` against the test images and the code, and `AgentsFileTests` the limits of every `AGENTS.md`.**
- **`SiteTocTests` checks that every page of `site/` but `index.md` is in exactly one `toc.yml`, and `SiteStructureTests` the folders and root pages of `site/`, the five pages of each database folder, the tab groups that name a database, and that each limit has its row in `site/guides/troubleshooting.md`.**
- **`UserDocs.cs` finds the user documents through `RepositoryRoot.Path`** and is the one place that knows what counts as a user document.
- **A fenced block opens with `sql`, and a second word says how it is parsed**: a dialect, `fragment` for part of a file, or `error` for SQL that shows a mistake.  Mark a sample so; do not weaken the test.
- **The two readmes are packed, so their links are absolute**: `https://mbcrawfo.github.io/SqlSource/<folder>/<page>.html` into the manual, `https://github.com/mbcrawfo/SqlSource/blob/main/<path>` elsewhere, and a test follows each to the file of the repository it shows.
- **`SiteStructureTests.PagesNotYetMoved` only shrinks.**  It lists the pages that wait at the root of `site/` for their phase of the documentation overhaul.  The phase that moves one removes its name.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
