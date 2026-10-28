# AGENTS.md - examples

## What an example is

1. **Small and representative.**  A few queries in each folder show the common path and a few of the settings a user reaches for.  Coverage belongs in the end-to-end projects under `tests/`, never here.
2. **No tests, and nothing runs.**  The project is a library in `SqlSource.slnx` that compiles.  Its classes call the generated code as a user's code would, so a stale sidecar or a model that changes shape breaks the build.
3. **Only what the manual recommends.**  The project file, its `sqlsource.json`, the queries and the code are what a user may copy, so code read through Dapper follows every point of [the recipe](../docs/manual/recipes.md#models-through-dapper).
4. **One folder per output**, `Sql/`, `Models/` and `CodeGen/`, each with one class whose `[SqlSourceGenerate]` attribute sets the folder's output and claims the folder's `.sql` files.  `CodeGen/` uses the default output.
5. **Anything only the repository needs is marked.**  The project file references the generator as the package adds it, with `ReferenceOutputAssembly="false"`, imports the package's props and targets by path, and keeps `SQLSRC240` a warning; its comments and the README say what a user writes instead.

## Keeping them current

2. **The sidecars are written by `tools/describe-end-to-end.sh`, never by hand.**  Run it after a change to a query or to a project's `schema.sql`, and read its diff.
3. **A project sets no `database`.**  Its queries belong to the default logical database of its dialect, `postgres` or `mssql`, so a run has one database: the README's `SQLSOURCE_CONNECTION` and the connection `postgres=` or `mssql=` in `tools/describe-end-to-end.sh` both depend on it.
4. **`schema.sql` names no owner, role or database.**  The Compose file and the script each run it in a database of their own, PostgreSQL's Compose file as its own user and the script as `postgres`, SQL Server's both as `sa` in a database `examples`, and both must give the sidecars the same answer.  SQL Server's Compose file applies it from a second service, with `sqlcmd -I` and `-d examples`, only when it creates the database.
7. **Each README's links to the manual are relative.**  It is not a packed readme.

## Adding a database

A new database gets a project of its own, `examples/<Engine>Examples`, by the pattern of `PostgresExamples` and `SqlServerExamples`: its Compose file and `schema.sql`, the three folders, a README, an entry in `SqlSource.slnx` under `/examples/`, and its sidecars written by the same script.

> Maintenance: this file names specific files and folders.  If you rename, move, or remove them, update this file in the same commit.
