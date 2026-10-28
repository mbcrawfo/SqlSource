# AGENTS.md - tests/SqlSource.Tests/Documentation

- **`UserDocsLinksTests` checks the links and anchors of `README.md`, `src/SqlSource.Tool/README.md` and every page of `docs/manual/` but its `AGENTS.md`, `UserDocsSamplesTests` the SQL in their fenced blocks, and `AgentsFileTests` the limits of every `AGENTS.md`.**
- **`UserDocs.cs` finds the user documents through `RepositoryRoot.Path`** and is the one place that knows what counts as a user document.
- **A fenced block opens with `sql`, and a second word says how it is parsed**: a dialect, `fragment` for part of a file, or `error` for SQL that shows a mistake.  Mark a sample so; do not weaken the test.
- **The two readmes are packed, so their links are absolute**, `https://github.com/mbcrawfo/SqlSource/blob/main/<path>`, and a test follows those too.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
