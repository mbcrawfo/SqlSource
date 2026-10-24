# Query generation, phase 8.3: PostgreSQL binding and end-to-end - design

Date: 2026-10-24

Sub-phase 8.3 of the [query generation epic](2026-10-07-query-generation-epic-design.md).  It binds each PostgreSQL parameter of a generated method by the type the tool described it as, `NpgsqlParameter.DataTypeName` set to the sidecar's `bindName`, and proves the methods of [8.1](2026-10-24-query-generation-phase-8-1-methods-design.md) and the converters of [8.2](2026-10-24-query-generation-phase-8-2-converters-design.md) against PostgreSQL: the end-to-end project runs every described query through its method, and every type of the map is written and read through generated code on Npgsql 10 and on Npgsql 8, the floor, on PostgreSQL 14 and 18.

It builds on 8.2.  Sub-phase 8.4 can be built beside it.  Sub-phase 8.5 builds on both.

## Intent

The owner's measure for phase 8, for PostgreSQL: every query runs through its method; every row of the map is written as a parameter and read as a column through generated code on the newest driver and the floor's; and the methods cost no more than hand-written ADO.NET.  This sub-phase is where the measure is taken.  It mirrors [sub-phase 5.4](2026-10-18-query-generation-phase-5-4-end-to-end-design.md), which proved the models through Dapper.

## Goals

1. Each parameter of a `postgres` method bound with `DataTypeName` set to its `bindName`, a converted one by its database type's, with no `NpgsqlDbType` named anywhere in generated code.
2. In `tests/SqlSource.Tests.Postgres`, every query that has an entry claimed by at least one `CodeGen` type and run through its method, its rows, counts and `NULL`s asserted; `NoteQueries` back to `CodeGen`, its `Email` read and written through converters.  `Constants.sql`, whose output is `Sql` to prove that the tool leaves such a file out, has no entry and so no method; it is the one query of the project that does not run through one.
3. Every shape's success and failure, every collection type, every `MethodLocation`, `async-method-suffix`, a query with tokens, the transaction, the timeout, cancellation, and the connection rule, run against the server.
4. Every type of the PostgreSQL map written through a method's parameter and read back through a method's column, on Npgsql 10 in the main project and on Npgsql 8 in `tests/SqlSource.Tests.Postgres.Npgsql8`.
5. A test that a generated method allocates no more than a hand-written equivalent.
6. Dapper kept where it proves what a Dapper user relies on: `DapperRecipeTests`, the Dapper read and write of `all_types`, and the realistic corpus's Dapper run.  The other classes' Dapper tests become tests of their methods.
7. The manual's PostgreSQL page documents the methods, and the known issue of binding by value is removed.

## Non-goals

- SQL Server.  Sub-phase 8.4.
- The realistic corpus through the methods.  Sub-phase 8.5.
- The example project.  Sub-phase 8.6.
- A parameter whose type `SQLSRC232` refuses, which has no `bindName`.

## Anti-goals

From 5.4, kept: a test that passes by reading nothing; a committed sidecar written by hand; a database left changed.  And:

- **A query whose committed entry differs between 14 and 18.**  A new query describes the same on both, or is left out of 14's `--check` as `merge-returning` is.
- **A test of a method that a Dapper call does the work of.**  A method's test calls the method.
- **An allocation test that passes by measuring nothing,** or that fails by measuring another test's work.

## What exists today

As of the merge of 8.2, which this spec assumes; 8.0's spike says what Npgsql 8 and 10 do with `DataTypeName`, `NpgsqlParameter<T>` and a `NULL`.

- 8.1's binding step for `postgres` sets the name and the value; 8.2 converts the value first where a converter applies.
- `bindName` is written on every PostgreSQL parameter's top-level type in the sidecar and read by `SidecarTypeReader`.
- `tests/SqlSource.Tests.Postgres` sets `SqlSourceOutput` `Models`; its queries are in `EndToEnd/Queries/`, claimed by the classes of `EndToEnd/`; `NoteQueries` is `Models` since 8.1.  Its Dapper tests read each class's queries, `DapperRecipeTests` mirrors the manual's recipe, and `InsertAllTypes` writes the types Dapper can send, `WriteSideLeftOut` naming the rest.  No query has no rows, `one`, `one-optional`, `none` or `rowcount`, or a token.
- `tests/SqlSource.Tests.Postgres.Npgsql8` links `AllTypes.sql` and its sidecar and reads them through Dapper under `Models`.
- `EndToEndSources` sets Dapper's globals and the `App` data source's `MapEnum<NoteKind>()`.

## Design

### Binding

For each parameter of a `postgres` method, the binding step writes a parameter whose `DataTypeName` is the entry's `bindName`, and whose value is the property's, converted when 8.2 applies, `DBNull.Value` for null.  The parameter is the one the command creates, cast to `NpgsqlParameter`, or an `NpgsqlParameter<T>` with `TypedValue` set where 8.0's spike finds it avoids boxing a value type with `DataTypeName` honoured on Npgsql 8 and 10; the method is the same on every supported Npgsql.  `bindName` holds no modifier, so a `varchar(50)` parameter is bound as `character varying`, as the tool's `check` bound it.

### The end-to-end project

- **Every described query through a method.**  The project's default output becomes `CodeGen`, so that the default is what the end-to-end project proves.  The query classes' Dapper tests become tests of their methods.  `AllTypes.sql` and the files `DapperRecipeTests` reads are claimed a second time by a `Models` class, whose constants the Dapper tests run, as the realistic corpus is claimed twice in 8.5; `RealisticDapperTests` is 8.5's.  `Constants.sql` stays `Sql`.  The builder chooses which classes carry which claims, within: each described query has a `CodeGen` claim and a test that calls its method.
- **The settings of the methods.**  New queries and claims cover what no query covers today: a query with no rows, run as `rowcount` by default and as `none`; `one` with one row, no row and two rows; `one-optional` with one row, none and two; `many` with each of the ten collection types, by the attribute, the property, metadata and the marker; each of the four `MethodLocation`s; `async-method-suffix`; a `CodeGen` query with a token and its default, called with another fragment, and with a declared parameter the fragment does not use; `INSERT ... RETURNING` as `rowcount`.
- **The run-time arguments.**  A method given a transaction, whose write the rollback undoes; `commandTimeout` that expires on `pg_sleep`; a token cancelled before the call and during the read; a closed connection closed again after the method, after its exception, and after its cancellation; an open connection, and one in a transaction, left open.
- **Notes.**  `NoteQueries` is `CodeGen` again.  `Email` gains converters, so its column and its parameter go through them; and `NoteKind`, a C# enum the data source maps, is trusted to the driver as the owner decided.
- **A converted value type.**  An id type converted from a domain over `bigint`, added to `app`'s schema with a column of its own and a query that reads it and filters by it, so that a converter of a value type is proven as a column and a parameter, and no existing query's entry changes.  A type override keys on a database type, so a domain is what lets the override name this column alone.
- **Every type.**  A new `CodeGen` query in `AllTypes.sql` inserts a row of `all_types` with every column from its parameters, and its read through a method checks each value against `AllTypesRows`, for the row of values and the row of `NULL`s, in a transaction rolled back.  `InsertAllTypes`, its Dapper test and `WriteSideLeftOut` stay as they are: they prove what the manual says of Dapper.  A type of the map that `all_types` leaves out by design, `cube`, which Npgsql 8 cannot read, is written and read by a query of its own in the main project alone.  Every row of the map is so proven, or the test names it with why.

### The floor of Npgsql

`tests/SqlSource.Tests.Postgres.Npgsql8` claims `AllTypes.sql`, and the write of every type, under `CodeGen`, and reads and writes every type through the methods on Npgsql 8, beside its Dapper read, so that each cell of the map's Npgsql 8 column is proven through generated code.  It runs on the default image, as it does.

### The allocation test

A query with parameters and a few rows, run through its method and through a hand-written ADO.NET method of the same calls, written in the test the way the manual's own-code recipe writes one; after a warm-up, each run many times, and the generated method's allocated bytes per call no more than the hand-written's and a small margin.  It measures `GC.GetTotalAllocatedBytes(true)`, which counts every thread, since an asynchronous method's continuations run where the driver completes them; so it runs in a test collection of its own with parallelization off, which starts its own container, so that no other test's work is counted.  The margin and the counts are the builder's, written in the test with why.

### `--check` and the sidecars

`tools/describe-end-to-end.sh` writes the new queries' sidecars.  `--check` covers them on 18 and on 14, but for a query 14 describes otherwise, which is left out by the positional filters with the reason written beside it.

## Testing

The project's and the floor project's tests above.  Generator tests for the binding step of each row of the PostgreSQL map: the `DataTypeName` emitted for a base type, an array, an enum, a domain, a range, a multirange and a converted parameter, compiled against Npgsql 8, 9 and 10.

## Documentation

- `docs/manual/postgresql.md`: its Query methods section, from what was built: how a parameter is bound, `MapEnum` and plugins on the data source for a mapped type with no converter, what Npgsql 8 and 10 read, and anything the end-to-end project found a user must know; the known issue of binding by value removed.
- `docs/manual/generator.md`: the PostgreSQL known issue of 8.1 removed.
- `docs/manual/README.md`: the row of generated query methods for PostgreSQL.
- `docs/manual/recipes.md`: the Dapper recipe unchanged unless its test found otherwise.
- `docs/architecture/generator.md`: the PostgreSQL binding step.
- `tests/SqlSource.Tests.Postgres/README.md`, `tests/README.md`, `tests/AGENTS.md`: the project's claims, the methods' tests, the allocation test's collection, and the floor project's methods.
- `CONTRIBUTING.md`: what the Npgsql 8 project proves.
- The epic outline: the row of 8.3.

## Order of work

One pull request, one commit for each step:

1. The outline's row says In progress.  Binding by `bindName`, with its generator tests.
2. The project's default `CodeGen`, the second claims of the Dapper tests, and the existing queries' methods called.
3. The new queries of the shapes, collection types, locations, tokens and suffix, with their sidecars.
4. The run-time arguments and the connection rule.
5. Notes through converters, and the id type.
6. Every type written and read, in the main project and on Npgsql 8.
7. The allocation test.
8. The documents, and the row set to Done.

## Left to the builder

- Which classes and files hold the new queries, and their names.
- The id type's name and the column it maps.
- The allocation test's query, counts and margin.

## Calls made in this spec

Not decided with the owner, and open to the review of this spec:

- Every PostgreSQL parameter is bound by `DataTypeName`, not by `DbType` where `DbType` would do: one rule for every row, the type the tool described.
- The end-to-end project's default output becomes `CodeGen`, and the Dapper tests read their queries through second, `Models` claims.
- The floor project proves the methods on the default image alone, as it proves the models.
- The allocation measure is the process's total, in a collection of its own with parallelization off and its own container, against a hand-written method in the test.
- The query classes' Dapper tests become method tests; Dapper stays for the recipe, `all_types` and the realistic corpus.
- The write of every type is a new query beside `InsertAllTypes`, which stays for Dapper; `cube` is proven by a query of its own.
- The converted value type comes from a new domain over `bigint` in `app`.

## Version

The pull request checks `VersionPrefix` against the release tags, as `AGENTS.md` requires.
