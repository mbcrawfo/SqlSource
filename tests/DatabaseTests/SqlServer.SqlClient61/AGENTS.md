# AGENTS.md - tests/DatabaseTests/SqlServer.SqlClient61

- **This project holds the read of `all_types` and `new_types` through Dapper, and their write and read through the methods, and a streaming method's read and early `break`, on Microsoft.Data.SqlClient 6.1, and nothing else.**
- **It references nothing that brings Microsoft.Data.SqlClient 7**, neither the tool, `tests/SqlSource.Tool.Testing` nor `tests/DatabaseTests/SqlServer`, since a reference to 6.1 under 7 is a package downgrade ([why](../README.md#the-sqlclient-61-project)).
- **It links `AllTypes.sql`, `NewTypes.sql`, `Streaming.sql` and their sidecars from `tests/DatabaseTests/SqlServer/EndToEnd/Queries/`, and commits none of its own, nor a `sqlsource.json`.**  The linked files take the settings of their own folder, the end-to-end project's, so a setting changed there changes this project's models and methods.
- **Its two claims at `models` name that output on their attribute**, since the end-to-end project's is `CodeGen`.
- **A type that a query of those files names by a type override is in `tests/DatabaseTests/TestHarness`**, as `Level` is.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
