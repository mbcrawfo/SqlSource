# AGENTS.md - tests/SqlSource.Tests.Postgres.Npgsql8

- **This project holds the write and read of `all_types` on Npgsql 8, through the methods and through Dapper, and nothing else.**
- **It references nothing that brings Npgsql 10**, neither the tool, `tests/SqlSource.Tool.Testing` nor `tests/SqlSource.Tests.Postgres`, since a reference to 8 under 10 is a package downgrade ([why](../README.md#the-npgsql-8-project)).
- **It links `AllTypes.sql` and its sidecar from `EndToEnd/Queries/` of `tests/SqlSource.Tests.Postgres`, and commits none of its own, nor a `sqlsource.json`.**  The linked file takes the settings of its own folder, the end-to-end project's, so a setting changed there changes this project's models and methods.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
