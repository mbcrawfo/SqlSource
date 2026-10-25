# Query generation, phase 8.4: SQL Server binding and end-to-end - design

Date: 2026-10-24

Sub-phase 8.4 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It binds each SQL Server parameter of a generated method by its `SqlDbType` with the size, precision and scale the tool described, and proves the methods of [8.1](2026-10-24-query-generation-phase-8-1-methods-design.md) and the converters of [8.2](2026-10-24-query-generation-phase-8-2-converters-design.md) against SQL Server, as [8.3](2026-10-24-query-generation-phase-8-3-postgresql-binding-design.md) does against PostgreSQL: the end-to-end project runs every described query through its method, and every type of the map is written and read through generated code on SqlClient 7 and on SqlClient 6.1, the floor, on SQL Server 2017 and 2025.

It builds on 8.2.  Sub-phase 8.3 can be built beside it.  Sub-phase 8.5 builds on both.

## Intent

The owner's measure for phase 8, for SQL Server, as 8.3 takes it for PostgreSQL.  The binding matters more here than there: SqlClient infers `nvarchar(4000)` or `nvarchar(max)` for a string, `decimal` with the value's own precision, and `datetime` for a `DateTime`, so a parameter bound by value alone fragments the plan cache, rounds a `decimal`, or loses a `datetime2`'s precision.  The manual's Dapper recipe already works around each; the methods must need no workaround.  It mirrors [sub-phase 6.5](2026-10-20-query-generation-phase-6-5-end-to-end-design.md).

## Goals

1. Each parameter of an `mssql` method bound with the `SqlDbType` of its described type, its `Size` from `maxLength`, and its `Precision` and `Scale` where the type has them; `vector` by its `SqlVector<float>` value, a null by `SqlVector<float>.CreateNull` of its dimension; a converted parameter by its database type's binding.
2. In `tests/SqlSource.Tests.SqlServer`, every query that has an entry claimed by at least one `CodeGen` type and run through its method; `NoteQueries` back to `CodeGen`, its `Email` alias type read and written through converters.
3. The shapes, collection types, locations, suffix, tokens, transaction, timeout, cancellation and connection rule, as 8.3 proves them, against SQL Server.
4. Every type of the SQL Server map written through a method's parameter and read back through a method's column, on SqlClient 7 in the main project and on SqlClient 6.1 in `tests/SqlSource.Tests.SqlServer.SqlClient61`, `json` and `vector` on 2025.
5. The allocation test of 8.3, on SQL Server.
6. The Dapper tests kept over `Models` claims.
7. The manual's SQL Server page documents the methods, and the known issue of binding by value is removed.

## Non-goals

- PostgreSQL.  Sub-phase 8.3.
- The realistic corpus through the methods.  Sub-phase 8.5.
- The example project.  Sub-phase 8.6.
- `SqlDbTypeExtensions.Json` and `SqlJson`.  Phase 7; `json` binds as `nvarchar(max)`.  `SqlDbTypeExtensions.Json` exists on SqlClient 6.1 and 7 and declares `json` on 2025; on 2017 it is a protocol error ([the spike](2026-10-24-query-generation-phase-8-spike.md#9-sqlclients-facets)).
- A CLR type, `hierarchyid`, `geography` and `geometry`, which have no C# type (`SQLSRC246`) and so no method; a table-valued parameter.  Out of the epic.
- A method for every result set of a batch.  `D-0006`.

## Anti-goals

As 8.3's, with 2017 and 2025 for 14 and 18; and:

- **A binding that depends on a value that fits**, a `Size` taken from a string's length, which is the plan cache's fragmentation the binding exists to stop.
- **A value cut by the client.**  SqlClient cuts a string or binary value longer than the parameter's `Size`, and a `datetime2`, `time` or `datetimeoffset` value to a `Scale` below 7, with no error ([the spike](2026-10-24-query-generation-phase-8-spike.md#9-sqlclients-facets)), so an insert would store less than the caller gave and a `WHERE` could match a row it should not.  The server sees every value whole, and refuses or rounds it as it would by value.

## What exists today

As of the merge of 8.2, which this spec assumes; 8.0's spike says what SqlClient 6.1 and 7 do with `Size`, `Precision`, `Scale`, `datetime2`, `vector` and a C# enum.

- 8.1's binding step for `mssql` sets the name and the value; 8.2 converts the value first.
- A SQL Server parameter's type in the sidecar holds `name`, the server's `system_type_name` verbatim with its facets, `nvarchar(128)` or `decimal(12,4)`, whose base name `SqlServerTypeMap` already parses out; `maxLength` in bytes or `-1` for `max`, `precision`, `scale` and, for an alias or a CLR type, `userType`.  No `bindName`.  An alias type's `name` is its base type's.
- `SqlServerTypeMap` gives each type's C# type and nothing of its binding.
- `tests/SqlSource.Tests.SqlServer` is `Models`, with the classes of 6.5, `Writes.sql` run in rolled-back transactions given to each call, `app/new-types.sql` and `NewTypes.sql` on 2025 alone, and `WriteSideLeftOut` naming what Dapper cannot send; `NoteQueries` is `Models` since 8.1.
- `tests/SqlSource.Tests.SqlServer.SqlClient61` links `AllTypes.sql` and `NewTypes.sql` with their sidecars and reads them through Dapper.

## Design

### Binding

The type map gains, for each SQL Server base type, the `SqlDbType` that names it, and the binding step writes the parameter the command creates, cast to `SqlParameter`, with:

| The type | Set |
|----|----|
| `char`, `varchar`, `binary`, `varbinary` | `Size` = `maxLength`, `-1` for `max` |
| `nchar`, `nvarchar` | `Size` = `maxLength / 2`, in characters, `-1` for `max` |
| `decimal`, `numeric` | `Precision` and `Scale`, both, since SqlClient takes the one not set from the value |
| `datetime2`, `time`, `datetimeoffset` | `Scale` 7, whatever the described scale |
| `json` | `SqlDbType.NVarChar`, `Size` `-1` |
| `vector(n)` | No `SqlDbType`: the value, a `SqlVector<float>`, gives the type, and its length the dimension; a null is `SqlVector<float>.CreateNull(n)`, `n` from the described `vector(n)` |
| `timestamp` (`rowversion`) | `SqlDbType.Timestamp` |
| Any other | Its `SqlDbType` alone |

An alias type is bound as its base type, whose `SqlDbType` its `name` gives.  The binding is the type's, never the value's, with one exception: a string or binary value longer than the described length is bound at `max` of its variable-length type, `varchar(max)`, `nvarchar(max)` or `varbinary(max)`, so that the server sees it whole and refuses its insert or compares it as it would by value, where SqlClient would cut it.  A value that fits never changes the binding.

What [the spike](2026-10-24-query-generation-phase-8-spike.md#9-sqlclients-facets) found behind the rows, on SqlClient 6.1 and 7 alike:

- `Size` of `NVarChar` and `NChar` is in characters, so passing `maxLength` would double the declared length.
- `Scale` 0 is taken as not set and sent as 7, and a `Scale` between 1 and 6 makes SqlClient truncate the value where the server's own conversion rounds it.  Scale 7 declares every such parameter one way and leaves the rounding to the server, as an assignment in T-SQL does.
- `SqlDbTypeExtensions.Vector` does not hold on a parameter without an `ISqlVector` value: it reads back `NVarChar`, and `DBNull.Value` is sent as `nvarchar(4000)`.  `Size` is ignored for a vector.
- A `decimal` outside the declared precision throws `ArgumentException` before sending, where by value the server reports the overflow; the tests pin it.

### The end-to-end project

As 8.3's, in T-SQL, with what SQL Server adds:

- The project's default output becomes `CodeGen`; the query classes' Dapper tests become method tests, and `AllTypes.sql`, `NewTypes.sql` and the recipe's files are claimed a second time by a `Models` class for the Dapper tests that stay; `Constants.sql` stays `Sql`, the one query with no method.
- New queries as 8.3's, and: `rowcount` of a `SELECT`, which is `-1`, and of a statement under `SET NOCOUNT ON`, and of a method called after it on the same open connection, which is `-1` too, since `NOCOUNT` is the session's until the pool resets it ([the spike](2026-10-24-query-generation-phase-8-spike.md#6-executenonquerys-count)); `rowcount` and `one` of a batch of two statements, which read its first result set; `MERGE ... OUTPUT` through a method; `WAITFOR DELAY` for the timeout.
- A token cancelled while the command runs is SqlClient's `SqlException` (`Operation cancelled by user.`), not an `OperationCanceledException`, and one cancelled during the read or before the call a `TaskCanceledException` ([the spike](2026-10-24-query-generation-phase-8-spike.md#11-the-connection-rule)).  The method passes the driver's exception on unchanged; the tests pin each, and the manual says so.
- The values of the write: a string and a `byte[]` one longer than their column, whose insert the server refuses whole; a `datetime2(3)` value with seven digits, which the server rounds; a `decimal` outside its precision.
- The transaction is given to every method that runs inside one, as SqlClient requires; a method called on a connection with a transaction and not given it fails with SqlClient's own error, which a test pins and the manual states.
- `NoteQueries` is `CodeGen` again, `dbo.Email` through converters; and an id type converted from an alias type over `bigint`, added to `app` with a column and a query of its own, as 8.3's domain.
- A new `CodeGen` query in `AllTypes.sql` inserts a row with every column of the type table from its parameters but `rowversion`, which the server writes itself, and its read through a method checks each value against `SqlServerAllTypesRows`; `rowversion` is bound as a parameter in a query's `WHERE`; `json` and `vector` by a new write query in `NewTypes.sql`, described on 2025 alone and skipped below major 17.  `InsertAllTypes`, its Dapper test and `WriteSideLeftOut` stay as they are.  The `decimal` values include one whose precision a value-inferred binding would round, and the `datetime2` one whose seventh digit `datetime` would lose.

### The floor of SqlClient

`tests/SqlSource.Tests.SqlServer.SqlClient61` claims `AllTypes.sql`, `NewTypes.sql` and the write of every type under `CodeGen`, and reads and writes every type through the methods on SqlClient 6.1, beside its Dapper read.  It runs on the default image, as it does.

### The allocation test

As 8.3's, against SQL Server.

### `--check` and the sidecars

`tools/describe-end-to-end.sh` writes the new queries' sidecars.  A new query describes the same on 2017 and 2025, its parameter's type declared with `-- param:` where the versions differ, as 6.5 settled, or it is 2025-only and left out of 2017's `--check`.

## Testing

The project's and the floor project's tests above.  Generator tests for the binding step of each row of the SQL Server map, each facet rule above, an alias type and a converted parameter, compiled against SqlClient 6.1 and 7.

## Documentation

- `docs/manual/sqlserver.md`: its Query methods section, from what was built: how a parameter is bound, the transaction SqlClient requires, `rowcount`'s `-1` and `SET NOCOUNT ON` lasting for the session, a batch's first result set, the exception of a cancelled method, that a C# enum is read only from a column of its own underlying type (an `int` enum from `int` and not from `tinyint`, [the spike](2026-10-24-query-generation-phase-8-spike.md#9-sqlclients-facets)), and anything the end-to-end project found; the known issue of binding by value removed.
- `docs/manual/generator.md`: the SQL Server known issue of 8.1 removed; and 8.3's known issue of a wrapping connection extended to SqlClient's `SqlParameter`, or written here if 8.4 merges first.
- `docs/manual/README.md`: the row of generated query methods for SQL Server.
- `docs/manual/recipes.md`: the Dapper recipe unchanged unless its test found otherwise.
- `docs/architecture/generator.md`: the SQL Server binding step.
- `tests/SqlSource.Tests.SqlServer/README.md`, `tests/README.md`, `tests/AGENTS.md`: as 8.3's.
- `CONTRIBUTING.md`: what the SqlClient 6.1 project proves.
- `docs/deferred/D-0006`: if the batch's tests change what it says.
- The epic outline: the row of 8.4.

## Order of work

As 8.3's, with SQL Server's binding and tests:

1. The outline's row says In progress.  The `SqlDbType` of each type, and binding with facets, with its generator tests.
2. The project's default `CodeGen`, the second claims, and the existing queries' methods called.
3. The new queries, with their sidecars.
4. The run-time arguments, the transaction and the connection rule.
5. Notes through converters, and the id type.
6. Every type written and read, in the main project and on SqlClient 6.1.
7. The allocation test.
8. The documents, and the row set to Done.

## Left to the builder

As 8.3's.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- A parameter is bound by its described type's `SqlDbType` and facets, an alias type as its base, never by the value.
- By 8.0's spike, amended in 8.0's pull request: a string or binary value longer than its described length is bound at `max` so the server sees it whole, `datetime2`, `time` and `datetimeoffset` are bound at scale 7, and `vector` by its value, its null by `SqlVector<float>.CreateNull(n)`.
- The method passes a cancellation's exception on as the driver raises it, `SqlException` while the command runs.
- `json` binds as `nvarchar(max)` until phase 7.
- The end-to-end project's default output becomes `CodeGen`, as 8.3's.
- The floor project proves the methods on the default image alone.
- `rowversion` is proven as a parameter in a `WHERE`, since no insert can write it.
- The converted value type comes from a new alias type over `bigint` in `app`.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
