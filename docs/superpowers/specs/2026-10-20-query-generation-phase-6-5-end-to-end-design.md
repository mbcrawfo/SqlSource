# Query generation, phase 6.5: the SQL Server end-to-end project - design

Date: 2026-10-20

Sub-phase 6.5 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It makes `tests/SqlSource.Tests.SqlServer` the end-to-end project of SQL Server, as [sub-phase 5.4](2026-10-18-query-generation-phase-5-4-end-to-end-design.md) made PostgreSQL's: queries in `.sql` files over two logical databases, committed sidecars, the tool's `--check` against the container, every `Models` query read through Dapper into its generated model, the error paths, and a second project that reads every mapped type on SqlClient 6.1, the floor.

It builds on sub-phases 6.3 and 6.4.  Sub-phases 6.6 and 6.7 build on it.

## Intent

Sub-phase 6.4's tests prove what the generator writes for SQL Server; only a real server and a real driver prove that it is right: that the type the map chose is the one SqlClient gives on 6.1 and on 7, that a column the model calls not null never holds `NULL`, and that a model built for Dapper is one Dapper fills.  This is the proof of the owner's bar for the map.

## Goals

1. `tests/SqlSource.Tests.SqlServer` runs the generator at build time over its own `.sql` files and committed sidecars, under `SqlSourceDialect` `mssql`.
2. Two logical databases, `app` and `billing`, on the project's container, each with a schema and a seed, applied by the shared `EndToEndFiles`, which learns SQL Server's `GO`.
3. Queries that cover the combinations of settings PostgreSQL's cover, in T-SQL: each output, each `SqlLocation`, each model type, the suffixes, the namespace, the sorting, table models of `dbo` and of `billing`, shared names, the type override at each level with SQL Server's keys, an alias type, the `!` and `?` suffixes in brackets and in double quotes, the `-- column:` marker, and the phase 8 settings accepted with no effect.
4. A table of every type the base map gives a C# type and SQL Server 2017 has, a row of values and a row of `NULL`s, read and, where Dapper binds it, written; and a 2025-only table of `json` and `vector`, by the map's research.
5. `tools/describe-end-to-end.sh` writes the project's SQL Server sidecars too.
6. The three kinds of test, on 2025 and 2017.
7. `tests/SqlSource.Tests.SqlServer.SqlClient61`, which the owner approved as PostgreSQL's Npgsql 8 project was, reads the type table through Dapper on SqlClient 6.1.

## Non-goals

- The realistic corpus as models.  Sub-phase 6.6.
- `examples/SqlServerExamples`.  Sub-phase 6.7.
- Execution through generated methods.  Phase 8.

## Anti-goals

From 5.4, kept: a test that passes by reading nothing; a committed sidecar written by hand; a database left changed.  And: **a query whose committed entry differs between 2017 and 2025**, which `--check` on the floor would report; a query that the spike's version differences touch declares its parameter's type with `-- param:` instead.

## What exists today

- `tests/SqlSource.Tests.Postgres` and `tests/SqlSource.Tests.Postgres.Npgsql8` are the pattern, with `EndToEnd/` holding `AccountQueries`, `AllTypesQueries`, `ConstantQueries`, `InvoiceQueries`, `NoteQueries`, `ShapeQueries`, `Reporting/AccountReports`, `Queries/` with the `.sql` files and sidecars, `app/` and `billing/` with `schema.sql` and `seed.sql`, `ErrorPaths/`, `CheckTests`, `ErrorPathTests`, `DapperRecipeTests` and `ModelShapeTests`.
- `tests/SqlSource.Testing/EndToEndFiles.cs` runs each file as one command and its `Folder` is PostgreSQL's `EndToEnd`.
- `tools/describe-end-to-end.sh` is PostgreSQL's alone: its container, `psql`, `pg_isready`, its `DATABASES` and its connection strings.
- `tests/SqlSource.Testing/EndToEnd/AllTypesRows.cs` holds PostgreSQL's row expectations.

## Design

### The project

`tests/SqlSource.Tests.SqlServer.csproj` imports the props and targets by path and references the generator as an analyzer, as PostgreSQL's does; sets `SqlSourceDialect` `mssql`, `SqlSourceDatabase` `app`, `SqlSourceOutput` `Models`, and the project-level settings that the queries' combinations need; lists `SQLSRC240` in `WarningsNotAsErrors`; references Dapper.  `ErrorPaths/**` is left out of the build and copied to the output, as PostgreSQL's is.

### The databases

`SqlServerContainer` creates `app` and `billing` and applies `EndToEnd/app/` and `EndToEnd/billing/` `schema.sql` and `seed.sql`.  `billing` has a schema `billing`, so the table model's prefix is proven, and `app` the `dbo` tables.  `EndToEndFiles` takes the engine's folder and splitting rule, and `GO` batches for SQL Server; `describe-end-to-end.sh` applies the same files in the same order.  The `json` and `vector` table is in a file applied only on a server of major 17 or later.

### The queries

The PostgreSQL project's classes, each in T-SQL with its combinations kept, and the class and file names the builder chooses:

| Class | Covers |
|----|----|
| Accounts | Project defaults, `Nested`; table models of `dbo`; a shared `-- output-model:`; a parameter declared `null`; an alias type parameter and column |
| Accounts in a second namespace | `Direct`, and a second namespace claiming one file |
| All types | Every row of the type table, read and written |
| Constants | Output `sql` by the file's marker: no models, no sidecar |
| Invoices | `-- database: billing`; the model namespace by the file's marker; a table model named `Billing...` |
| Notes | `Output = GeneratorOutput.CodeGen`; type overrides at the property, metadata, file and query levels with SQL Server's keys, among them `sys.`, `dbo.Email`, `date=System.DateOnly` and `time=System.TimeOnly`; an outer join; `-- column:`; `[name!]` and `"name?"` |
| Shapes | Record and class shapes, a suffix, `ModelNamespace`, `MethodLocation.Public`, `CollectionType.List`, `sort-output` |
| Writes | `INSERT ... OUTPUT`, `UPDATE ... OUTPUT`, `MERGE ... OUTPUT`, run in a transaction that is rolled back |

The types Dapper cannot send are listed as PostgreSQL's `WriteSideLeftOut` lists them.  The row expectations of the type table live beside PostgreSQL's in `tests/SqlSource.Testing/EndToEnd/`.

### The script

`tools/describe-end-to-end.sh` gains SQL Server's part: it reads `DefaultImages.SqlServer` by its line shape, starts the container with the platform it needs on an arm64 host, waits for a query to answer, creates the databases and applies each file with the image's `sqlcmd` given `-I` and `-b`, deletes the project's SQL Server sidecars, and runs the tool as a project with `--connection app=...` and `--connection billing=...`, `TrustServerCertificate=True` in the value.  Each engine's part is a function, and the script runs both.  `tools/AGENTS.md`'s rule that the script applies the same files in the same order as the container class covers SQL Server's.

### The three kinds of test

1. `CheckTests`: `describe --check` through `CliRun.Real` on the project with a connection for each database, asserting each database's line.  On 2017 the 2025-only queries are left out by the positional filters, as PostgreSQL's 14 run leaves out the cases of a later major.
2. A Dapper test for each query class, asserting the values of at least one row and the `NULL`s of another; the type table's test, read and written, by the row expectations; `DapperRecipeTests` for the manual's SQL Server recipe.
3. `ErrorPathTests`, on a copy of `ErrorPaths`: a query edited after it was described is out of date; a column added to a table after is out of date; a query the server refuses is reported and writes no sidecar.

### The floor of SqlClient

`tests/SqlSource.Tests.SqlServer.SqlClient61` references Microsoft.Data.SqlClient with `VersionOverride` at 6.1's last patch, Dapper, and the generator as an analyzer, and nothing that brings SqlClient 7: not the tool, not `SqlSource.Tool.Testing`.  It links the type table's `.sql` files and sidecars from the main project, the 2025-only `json` and `vector` one included, starts its own container on the default image, `DefaultImages.SqlServer`, read by its line shape, builds only `app` with its 2025-only file, and reads both tables through Dapper into the models the generator gave it under the 6.1 column, so that every cell of 6.1's column is proven against the server.  It runs in the solution, on the default image.  `tests/README.md` documents it beside the Npgsql 8 project.

## Testing

The project's tests above; the floor project's; the shared `EndToEndFiles`' tests for `GO`, a `GO` with white space, `go` in another case, and `GO` inside a string or a comment, which is no separator: the splitter splits lines, and a line that holds only `GO` inside a block comment is a known limit of `sqlcmd` too, which the files of this repository do not use.

## Documentation

- `tests/SqlSource.Tests.SqlServer/README.md`, `tests/README.md`, `tests/AGENTS.md`: the end-to-end part, the floor project, writing the sidecars.
- `tools/README.md`, `tools/AGENTS.md`: the script's SQL Server part.
- `CONTRIBUTING.md`: the SqlClient 6.1 project and what it proves.
- Root `AGENTS.md` rule 5: moving SQL Server's default image runs the script for its sidecars.
- `docs/manual/recipes.md`: the Dapper recipe for SQL Server, and `docs/manual/sqlserver.md` where the end-to-end project found something a user must know.
- The epic outline: the row of 6.5.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  The project runs the generator, with no claimed file yet.
2. `EndToEndFiles` for two engines; the two databases, their schemas and seeds.
3. The queries and the classes, with sidecars written by the script's new part.
4. `--check` and the error paths.
5. The Dapper tests.
6. `tests/SqlSource.Tests.SqlServer.SqlClient61`.
7. The documents, and the row set to Done.

The plan counts the files against the limit of 150; the queries and their sidecars are the bulk.

## Left to the builder

- Which queries and how many, within the table; the names of the classes and files.
- The seeds' values.
- The `sqlcmd` path of the default image and the platform flag.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- The `json` and `vector` table is a 2025-only file and query, left out of the floor's `--check` by the positional filters.
- A version difference in a parameter's type is answered by declaring the type, not by a per-version exception.
- The floor project runs on the default image only, as the Npgsql 8 project does.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
