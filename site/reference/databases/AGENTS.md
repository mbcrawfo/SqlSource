# AGENTS.md - site/reference/databases

The databases that SqlSource can describe, one folder for each, and the pages for every other database.

- **Every database folder has the same five file names**: `index.md`, `nullability.md`, `types.md`, `libraries.md` and `query-methods.md`.  `SiteStructureTests` fails on a folder that differs.  The headings and anchors inside may differ, since two databases need not have the same features.  A database with no library support has a short `libraries.md` that says so.
- **A page of a database's folder states only what is true of that database.**  What is true of every database is on a page outside the folders, and the database's page links it.
- **A database that can be described gets a folder, and its row of `index.md#database-support` says Yes.**  A database that cannot is a row of that table, is covered by `other.md`, and gets no folder.
- **`index.md#supported-versions` is the one home of the supported versions.**  A new statement of a supported version is a cell of its table, in the form `SupportedVersionsTests` reads, and every other page links the table.  Azure SQL is "accepted, and not tested", never "supported".
- **A tab for one database has the folder's name as its tab id**, `#tab/postgresql` or `#tab/sqlserver`.  `SiteStructureTests` fails on a tab group that has one and not the other.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
