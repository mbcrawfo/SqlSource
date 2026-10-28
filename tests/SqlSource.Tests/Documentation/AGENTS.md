# AGENTS.md - tests/SqlSource.Tests/Documentation

- **`UserDocs.cs` finds the user documents from `RepositoryRoot`** and is the one place that knows what counts as a user document.
- **A fenced block opens with `sql`, and a second word says how it is parsed**: a dialect, `fragment` for part of a file, or `error` for SQL that shows a mistake.  Mark a sample so; do not weaken the test.
- **The two readmes are packed, so their links are absolute**, `https://github.com/mbcrawfo/SqlSource/blob/main/<path>`, and a test follows those too.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
