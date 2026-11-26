# PostgreSQL user-defined range and `_` array parameter: implementation plan

> **For agentic workers:** implement task by task; each task ends with its tests passing.

**Goal:** a column or parameter of a user-defined PostgreSQL range or multirange is described instead of ending the run with `SQLSRC200` (TD-0125), and an array parameter of a type named with a leading `_` no longer overflows the stack in Npgsql (TD-0126).

**Architecture:** the tool's data source calls `EnableUnmappedTypes()`, as the manual tells an application to, so `GetColumnSchemaAsync` and `check` read and bind a user range as the application does.  `PostgresBindNames` names an array whose element's name starts with `_` by the array's own qualified name, which Npgsql reads without recursing, so such a parameter gets Npgsql's own refusal, `SQLSRC232`, or binds as a domain array does.

**Measured first (postgres:18.6, Npgsql 10.0.3, scratch program):**

- Without the opt-in, `GetColumnSchemaAsync` throws `NotSupportedException` for a column of a user range, of its multirange and of a domain over it; an array of one is read.  With it, all four are read, and every other column's name, OID, size, precision and scale is unchanged.
- With the opt-in a parameter of a user range, multirange, array of range or domain over range binds; without it Npgsql refuses it.  Enums and composites bind the same either way.
- `DataTypeName.FromDisplayName` recurses forever on a name whose type part starts with `_` and ends with `[]`, in v10.0.3, v11.0.0 and `main`.  The array's own name (`y.__feel`) gives `Writing is not supported` for an enum or composite array, and an array of a domain named `_dom` binds as `integer[]`, as any domain array does.

## Tasks

1. **Bind name.**  `src/SqlSource.Tool/Describing/Postgres/Types/PostgresBindNames.cs`: rule 1 gains the case of an element whose bind name's type part starts with `_`; the array is then its own qualified name.  Rows and cases in `tests/SqlSource.Tool.Tests/Describing/Postgres/Types/PostgresBindNamesTests.cs` for an enum, a composite and a domain named `_x`.
2. **Opt-in.**  `PostgresConnection.BuildSource` calls `EnableUnmappedTypes()`, with the reason; `docs/architecture/postgres-describer.md` says so beside `UseNetTopologySuite()`.
3. **Real server.**  `tests/DatabaseTests/Postgres/Describer/`: columns of a user range, its multirange, an array of it and a domain over it are described; parameters of each bind and are recorded; an array parameter of an enum and of a composite named `_x` is `SQLSRC232` naming the parameter, and of a domain named `_x` binds.  Objects in `ScratchSchema`.  Run on the default and the floor image.
4. **Fixtures.**  Record every PostgreSQL case again with only the opt-in: no diff shows no other type's answer moved.  Then add a range and its multirange to `postgres/schema.sql` and a case that selects both, and record again.
5. **End to end.**  A table of a user range and multirange in `EndToEnd/app`, a query file with the manual's type override to `NpgsqlRange<double>` and `NpgsqlRange<double>[]`, a data source with `EnableUnmappedTypes()`, and a test that reads the values and sends a range parameter.  `tools/describe-end-to-end.sh` writes the sidecars; no other sidecar changes.
6. **Docs.**  Delete TD-0125 and TD-0126, their rows, their limits in `site/reference/databases/postgresql/types.md` and their rows of `site/guides/troubleshooting.md`; `site/diagnostics.md` lists the `_` array under `SQLSRC232`'s refused types.
7. **Validate and review.**  `./format.sh`, `./pre-commit-validation.sh --full`, then `/branch-review:review --base origin/main`.
