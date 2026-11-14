# AGENTS.md - site/reference/databases

The databases that SqlSource can describe, one folder for each, and the pages for every other database.

- **Every database folder has the same five file names**: `index.md`, `nullability.md`, `types.md`, `libraries.md` and `query-methods.md`.  The headings and anchors inside may differ, since two databases need not have the same features.  A database with no library support has a short `libraries.md` that says so.
- **A page of a database's folder states only what is true of that database.**  What is true of every database is on a page outside the folders, and the database's page links it.
- **A database that can be described gets a folder, and its row of `index.md#database-support` says Yes.**  A database that cannot is a row of that table, is covered by `other.md`, and gets no folder.
- **Until their phase of the documentation overhaul moves them here, PostgreSQL and SQL Server are `../../postgresql.md` and `../../sqlserver.md`**, and no database folder exists.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
