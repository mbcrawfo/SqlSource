# AGENTS.md - site/start

Get started: the path of a newcomer, read in order.

- **The line for Get started.**  A topic is here only when an application with one PostgreSQL or SQL Server database and the default `repository` output needs it before its first release.  Transactions are here, and are where the `methods` output is introduced, by the need for it.  Anything else is a guide.
- **The pages are written by the `get-started` phase of `docs/superpowers/specs/2026-11-12-docs-overhaul-epic-design.md`.**  Until then `toc.yml` lists `../concepts.md`, a page here holds only what a move placed on it, and a change to getting started goes in `README.md`.
- **The pages are read in the order of `toc.yml`**, and each ends with "Next:" and a link to the next page.
- **One running example and the `repository` output.**  The samples are the files of `examples/PostgresExamples/Repository` and `examples/SqlServerExamples/Repository`, which compile.
- **Give each step whole.**  Never send the reader to Reference in the middle of a step.  State a rule in its common form, and link its owner under "Rules:" near the end of the page.
- **A page here owns no rule**: no `## Limits`, no `setting-<name>` anchor, no table of values or defaults.
- **Put a trap of the first hour where it bites**, stated in full with its error id and the fix, such as a `.env` file at the repository's root that is not read.
- **A difference between databases is a tab** of a sample or at most three sentences; a longer one is two anchored sections.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
