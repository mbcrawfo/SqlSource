# AGENTS.md - tests/DatabaseTests/Postgres.Npgsql8

- **This project holds the write and read of `all_types` on Npgsql 8, through the methods and through Dapper, of `noda_values` through the methods and Npgsql.NodaTime 8, and a streaming method's read and early `break`, and nothing else.**
- **`NodaTimeQueryTests` holds a copy of the rows of the end-to-end project's `EndToEnd/NodaTime/NodaTimeQueryTests.cs`**, since the harness references no NodaTime: a change to one changes the other.
- **It references nothing that brings Npgsql 10**, neither the tool, `tests/SqlSource.Tool.Testing` nor `tests/DatabaseTests/Postgres`, since a reference to 8 under 10 is a package downgrade ([why](../README.md#the-npgsql-8-project)).
- **It links `AllTypes.sql`, `Streaming.sql` and their sidecars from `EndToEnd/Queries/` of `tests/DatabaseTests/Postgres`, and `NodaTime.sql` and its sidecar from `EndToEnd/NodaTime/Queries/`, and commits none of its own, nor a `sqlsource.json`.**  The linked files take the settings of their own folder, the end-to-end project's, so a setting changed there changes this project's models and methods.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
