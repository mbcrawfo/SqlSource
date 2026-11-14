# AGENTS.md - site/start

Get started: the path of a newcomer, read in order.

- **The line.**  A topic is here only when an application with one PostgreSQL or SQL Server database and the default `repository` output needs it before its first release.  Anything else is a guide or a reference section.
- **The order is `toc.yml`'s.**  Each page ends with a line that starts "Next:" and links the next page; `errors.md` links the landing page's table of needs.
- **One running example**: `UserRepository` and its queries, whose text is that of `examples/PostgresExamples/Repository` and `examples/SqlServerExamples/Repository`.  A change to those files changes the pages.
- **The `repository` output only.**  `transactions.md` alone shows `-- output: methods`, by the need for a transaction or a timeout.  No page asks the reader to choose an output or a dialect.
- **Each step is whole.**  No step sends the reader to Reference; a page links its owners in its closing "Rules:" line.
- **A rule in its common form.**  No `## Limits`, no `setting-<name>` anchor, no table of a setting's values or defaults.
- **A first-hour trap stands in the step or the tab where it bites**, in full, with its error id and the fix, and its id also stands on `errors.md`.
- **Both databases on every page.**  A tab holds a sample or at most three sentences, and each page reads right with the first tab showing.
- **`SupportedVersionsTests` holds the two minimum database versions of `install.md`** to `../reference/databases/index.md`.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
