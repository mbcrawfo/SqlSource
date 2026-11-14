# tests/DatabaseTests/SqlServer.SyncAsync

This project runs each synchronous query method of SQL Server's end-to-end project beside its asynchronous twin, with the same inputs and the same setup, and compares what the two did to the caller and to the database.  It is a nightly signal and not a gate.  A red `sync-async-sqlserver` job of `.github/workflows/nightly.yml` says that a synchronous method may behave otherwise than its asynchronous twin: return another result, throw another exception, leave the connection or the caller's transaction in another state, or leave other rows behind.  No pull request waits for it.  [`tests/DatabaseTests/Postgres.SyncAsync`](../Postgres.SyncAsync/README.md) is PostgreSQL's, and is built the same way.

## When a test fails

1. Read the failure.  A failed row of the theory `RunAsync_Scenario_SyncMatchesAsync` names its scenario, then one line for each field of the outcome that differs, the asynchronous method's value first, `Result: async = ...; sync = ...`:
   - `Result`, the returned value as text, rows in order, `<void>` for none and `<none>` when the call threw, with a NodaTime value or a geometry written by `LibraryRendering`, every sub-second digit, a date's era and calendar, and a geometry's SRID and every ordinate, a `SqlGeography`'s or a `SqlGeometry`'s SRID too, which their own `ToString` drops;
   - `Failure`, the exception's type, its inner exception's type, the number of the `SqlException` in it or under it, its `ItemIndex` and its message;
   - `ConnectionState`, the connection's state after the call;
   - `TransactionState`, what became of the caller's transaction: `none` without one, `completed` when the call or the server ended it, else `xact_state=<n> trancount=<n>`, as the server reports it in the transaction;
   - `Tables[n]`, the rows of the scenario's `n`th table after the call.
2. Reproduce it with the command for one scenario, below.
3. Fix the generated synchronous method, or its asynchronous twin, so that the two do the same.  When the difference is Microsoft.Data.SqlClient's and no method can avoid it, see [the known differences](#the-known-differences).

A skipped row is a scenario whose two outcomes differed only by a known difference: the skip names it and its reason.

## Running

The project needs Docker running, but for `KnownDifferencesTests` and `LibraryRenderingTests`, which need no server, and it starts one container of SQL Server's default image.  The variable that names another image is ignored.  A run takes minutes, most of them restoring the fresh databases of the scenarios that write, and longer on Apple silicon, where Docker runs the image under emulation.

```bash
dotnet test --project tests/DatabaseTests/SqlServer.SyncAsync -p:RunSyncAsync=true
```

One scenario, by its name, the theory's row:

```bash
dotnet test --project tests/DatabaseTests/SqlServer.SyncAsync -p:RunSyncAsync=true --filter-display-name '*"MethodQueriesExtensions.TouchAccount: open")'
```

The tests that need no server, without Docker:

```bash
dotnet test --project tests/DatabaseTests/SqlServer.SyncAsync -p:RunSyncAsync=true --filter-class '*KnownDifferencesTests' --filter-class '*LibraryRenderingTests'
```

Without `-p:RunSyncAsync=true`, `dotnet test` finds no test project here.  That is how `dotnet test --solution SqlSource.slnx`, `pre-commit-validation.sh` and `.github/workflows/build.yml` leave the project out, so that only the nightly job runs it.  It still builds with the solution.

## The queries

The project compiles the end-to-end project's queries, under `method-type=both`, so that every query that can have a pair has one.  The target `SyncAsyncMirrorQueries` of the `.csproj` copies every `.sql`, `.sql.json` and `sqlsource.json` file of `tests/DatabaseTests/SqlServer/EndToEnd/`, and the engine project's own `sqlsource.json`, into `obj/Mirror/`, below this project's `sqlsource.json`, which sets `method-type=both`.  They are copied rather than linked, since a linked `.sql` file takes the settings files of the folder it really is in, which would keep the end-to-end project's settings.  The copies are the end-to-end project's files, byte for byte, so no sidecar is written here: `tools/describe-end-to-end.sh` writes them in the end-to-end project.

A query's own marker still wins: a `<Query>Sync` copy keeps its synchronous method alone, and a stream has no synchronous method, so neither has a pair.  The claims at the root of the project, and under `Json/`, `NetTopologySuite/`, `NodaTime/` and `Reporting/`, are one for each claim of the end-to-end project.  The types the queries name, under `Types/` and `Json/Types/`, are this project's copies, in the end-to-end namespaces, since a test project compiles no file of another; both methods of a pair use the same ones.

It is a project of its own, apart from PostgreSQL's, since the generator finds a type that a marker names by its short name across the whole compilation, and both engines' end-to-end projects have an `AccountId` and an `OrderMetadata`.

## The scenarios

`Scenarios/` has a file for each query file of the end-to-end project, `<File>Scenarios.cs`, whose `All` lists its scenarios, and `AllScenarios.cs` lists the files.  `Calls.cs` has the setups they share and the helpers that name a scenario `<Host>.<Method>: <what>`, or `<Host>.<Method>, collection: <what>` for a collection method.  A scenario names its pair by the type that declares both methods and the synchronous method's name, and gives:

- its input model or a collection method's items, and its tokens: the inserts of `Batches.sql` are given `dbo.batch_notes` by their token `notes`, a table that the fixture adds to `app` with the columns of `dbo.notes` and no identity, so that an insert writes the id it is given, and so that the rows stay after the call has closed its connection, where a temporary table would not;
- its setup: a connection open or closed, a caller's transaction begun, begun and rolled back before the call (`CompletedTransaction`), begun and not given to the method (`TransactionNotPassed`), or none, the data source by name (`default`, `unpooled` or `billing`), SQL to run on the connection before the call, whether it writes, and the `SELECT`s whose rows the outcome records.  A scenario whose SQL changes a setting of the session, such as `SET XACT_ABORT ON`, runs on `unpooled`, so that the pool hands the setting to no other scenario;
- a tag, which says what it shows: an ordinary result, a refused row count, a database error, or for a collection method an empty collection, a chunk boundary or a failure in a later chunk.

A scenario holds no expected value: the asynchronous method's outcome is the one the synchronous method's is compared with.  A collection method's `transaction` value is its query's marker, so a scenario picks it by picking the query.

The asynchronous method runs first, then the synchronous one, each on a connection of its own.  A scenario that only reads runs both on the shared seeded databases.  One that writes gives each run a fresh database: the fixture, `SqlServerDatabases`, backs each seeded database up once, to `<db>_seed.bak` in the container, and each run gets `RESTORE DATABASE run_<n>` from it, dropped after it.  [`TestHarness/SyncAsync/`](../TestHarness/SyncAsync/PairRunner.cs) runs the scenario through `SqlServerEngine`, which gives it the data sources, the fresh databases, the error numbers and the state of the caller's transaction.

Before two messages are compared, every GUID in them, such as a connection's id, is masked as `<guid>`, and the name of a run's fresh database as `<fresh database>`, since each run has a copy of its own and SQL Server names it in the message of a constraint or a truncation.

## Coverage

`Find_GeneratedPairs_EachHasItsScenarios` fails when a generated pair has no scenario, when a scenario names no pair, when two scenarios share a name, or when a collection method has no `Empty`, `ChunkBoundary` or `LaterChunkFailure` scenario.  Every pair has a scenario on an open and on a closed connection, an ordinary one but where every call fails, and each file of pairs has one in a caller's transaction, but `RepositoryScenarios.cs`, whose methods take none.  The rest is written for each query where its SQL can give it:

- **A refused row count** only for a `one` or `one-optional` query that an input can give no row or two rows: a query by a primary key, or an `INSERT ... OUTPUT` of one row, never gives two, and a `many` query refuses no count.
- **A database error** only where an input can make the server raise one, such as a duplicate key, a missing account, a truncation or an overflow; a query that only reads by key has none.
- **A failure in a later chunk of a collection method that only reads** is a null item after the first chunk, where no input makes the read fail on the server.
- **A failure in a later chunk** is not possible for five collection methods, `FindAccountThenOther`, `FindAccountThenEmpty`, `GetAccountThenEmpty`, `FindAccountThenText` and `InsertNoteThenOther`, whose query returns a second result set, which the method reads as the next item's or refuses, so that every collection of two items or more fails in its first round trip.  Their `LaterChunkFailure` scenario holds the tag for the coverage test, and its name says its input.
- **No `commandTimeout` scenario**: no query waits, a command held by a timer is not allowed in the tests ([`tests/AGENTS.md`](../../AGENTS.md)), and a scenario's SQL before the call runs on the call's own connection, which cannot hold a lock against itself.  The end-to-end project's own tests cover the timeout.
- **No cancellation**: a synchronous method takes no token, and `Cancellation.sql`'s method is called without one.
- **`FindByRowversion` finds no row**: no `rowversion` is known before the database is read, and a scenario holds its input before.

## The known differences

`KnownDifferences.cs` lists the differences that are Microsoft.Data.SqlClient's limits, each a rewrite of the pair of outcomes that is applied only when the two differ.  A scenario that the rewrites make equal is skipped with each one's name and reason; one that still differs fails, showing the rewritten pair.  `KnownDifferencesTests` checks each condition of a rewrite on outcomes it writes itself.

| Name | Reason | Manual |
|----|----|----|
| `SqlClientPendingTransactionMessage` | Microsoft.Data.SqlClient refuses a command on a connection with a pending transaction that the command was not given, with `InvalidOperationException`, from both methods, and its message names the call it refused: `BeginExecuteReader` or `BeginExecuteNonQuery` from the asynchronous method, and `ExecuteReader`, `ExecuteScalar` or `ExecuteNonQuery` from the synchronous one.  The rewrite gives the synchronous failure the asynchronous message when that is all that differs | [Give each `methods` method the transaction](../../../site/reference/databases/sqlserver/query-methods.md#method-results) |
