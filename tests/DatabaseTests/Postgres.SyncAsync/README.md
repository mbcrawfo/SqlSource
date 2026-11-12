# tests/DatabaseTests/Postgres.SyncAsync

This project runs each synchronous query method of PostgreSQL's end-to-end project beside its asynchronous twin, with the same inputs and the same setup, and compares what the two did to the caller and to the database.  It is a nightly signal and not a gate.  A red `sync-async-postgres` job of `.github/workflows/nightly.yml` says that a synchronous method may behave otherwise than its asynchronous twin: return another result, throw another exception, leave the connection or the caller's transaction in another state, or leave other rows behind.  No pull request waits for it.  [`tests/DatabaseTests/SqlServer.SyncAsync`](../SqlServer.SyncAsync/README.md) is SQL Server's.

## When a test fails

1. Read the failure.  A failed row of the theory `RunAsync_Scenario_SyncMatchesAsync` names its scenario, then one line for each field of the outcome that differs, the asynchronous method's value first, `Result: async = ...; sync = ...`:
   - `Result`, the returned value as text, rows in order, `<void>` for none and `<none>` when the call threw, with a NodaTime value or a geometry written by `LibraryRendering`, every sub-second digit, a date's era and calendar, and a geometry's SRID and every ordinate, which their own `ToString` drops;
   - `Failure`, the exception's type, its inner exception's type, its SQL state, its `ItemIndex` and its message;
   - `ConnectionState`, the connection's state after the call;
   - `TransactionState`, what became of the caller's transaction: `none` without one, `active`, `aborted` when an error left it unusable, or `completed`;
   - `Tables[n]`, the rows of the scenario's `n`th table after the call.
2. Reproduce it with the command for one scenario, below.
3. Fix the generated synchronous method, or its asynchronous twin, so that the two do the same.  When the difference is Npgsql's and no method can avoid it, see [the known differences](#the-known-differences).

A skipped row is a scenario whose two outcomes differed only by a known difference: the skip names it and its reason.

## Running

The project needs Docker running, but for `KnownDifferencesTests` and `LibraryRenderingTests`, which need no server, and it starts one container of the PostGIS image built on PostgreSQL's default image, which it builds on the first run.  The variable that names another image is ignored.

```bash
dotnet test --project tests/DatabaseTests/Postgres.SyncAsync -p:RunSyncAsync=true
```

One scenario, by its name, the theory's row:

```bash
dotnet test --project tests/DatabaseTests/Postgres.SyncAsync -p:RunSyncAsync=true --filter-display-name '*"CubeQueriesExtensions.EchoCube: open")'
```

The tests that need no server, without Docker:

```bash
dotnet test --project tests/DatabaseTests/Postgres.SyncAsync -p:RunSyncAsync=true --filter-class '*KnownDifferencesTests' --filter-class '*LibraryRenderingTests'
```

Without `-p:RunSyncAsync=true`, `dotnet test` finds no test project here.  That is how `dotnet test --solution SqlSource.slnx`, `pre-commit-validation.sh` and `.github/workflows/build.yml` leave the project out, so that only the nightly job runs it.  It still builds with the solution.

## The queries

The project compiles the end-to-end project's queries, under `method-type=both`, so that every query that can have a pair has one.  The target `SyncAsyncMirrorQueries` of the `.csproj` copies every `.sql`, `.sql.json` and `sqlsource.json` file of `tests/DatabaseTests/Postgres/EndToEnd/`, and the engine project's own `sqlsource.json`, into `obj/Mirror/`, below this project's `sqlsource.json`, which sets `method-type=both`.  They are copied rather than linked, since a linked `.sql` file takes the settings files of the folder it really is in, which would keep the end-to-end project's settings.  The copies are the end-to-end project's files, byte for byte, so no sidecar is written here: `tools/describe-end-to-end.sh` writes them in the end-to-end project.

A query's own marker still wins: a `<Query>Sync` copy keeps its synchronous method alone, and a stream has no synchronous method, so neither has a pair.  The claims at the root of the project, and under `Json/`, `NetTopologySuite/`, `NodaTime/` and `Reporting/`, are one for each claim of the end-to-end project.  The types the queries name, under `Types/` and `Json/Types/`, are this project's copies, in the end-to-end namespaces, since a test project compiles no file of another; both methods of a pair use the same ones.

It is a project of its own, apart from SQL Server's, since the generator finds a type that a marker names by its short name across the whole compilation, and both engines' end-to-end projects have an `AccountId` and an `OrderMetadata`.

## The scenarios

`Scenarios/` has a file for each query file of the end-to-end project, `<File>Scenarios.cs`, whose `All` lists its scenarios, and `AllScenarios.cs` lists the files.  `Calls.cs` has the setups they share and the helpers that name a scenario `<Host>.<Method>: <what>`, or `<Host>.<Method>, collection: <what>` for a collection method.  A scenario names its pair by the type that declares both methods and the synchronous method's name, and gives:

- its input model or a collection method's items, and its tokens;
- its setup: a connection open or closed, a caller's transaction begun or none, the data source by name (`default`, `multiplexing`, `nodatime`, `gis` or `billing`, each set up as `EndToEndSources` of the end-to-end project sets up its own), SQL to run on the connection before the call, whether it writes, and the `SELECT`s whose rows the outcome records;
- a tag, which says what it shows: an ordinary result, a refused row count, a database error, or for a collection method an empty collection, a chunk boundary or a failure in a later chunk.

A scenario holds no expected value: the asynchronous method's outcome is the one the synchronous method's is compared with.  A collection method's `transaction` value is its query's marker, so a scenario picks it by picking the query.

The asynchronous method runs first, then the synchronous one, each on a connection of its own.  A scenario that only reads runs both on the shared seeded databases.  One that writes gives each run a fresh database: the fixture, `PostgresDatabases`, makes a template of each seeded database once, `<db>_seed`, and each run gets `CREATE DATABASE run_<n> TEMPLATE <db>_seed`, dropped after it.  [`TestHarness/SyncAsync/`](../TestHarness/SyncAsync/PairRunner.cs) runs the scenario through `PostgresEngine`, which gives it the data sources, the fresh databases, the SQL states and the state of the caller's transaction.

Before two messages are compared, every GUID in them is masked as `<guid>`, and the name of a run's fresh database as `<fresh database>`, since each run has a copy of its own.

## Coverage

`Find_GeneratedPairs_EachHasItsScenarios` fails when a generated pair has no scenario, when a scenario names no pair, when two scenarios share a name, or when a collection method has no `Empty`, `ChunkBoundary` or `LaterChunkFailure` scenario.  Every pair has a scenario on an open and on a closed connection, an ordinary one but where every call fails, and each file of pairs has one in a caller's transaction, but `RepositoryScenarios.cs`, whose methods take none.  The rest is written for each query where its SQL can give it:

- **A refused row count** only for a `one` or `one-optional` query that an input can give no row or two rows: a query by a primary key or a unique column never gives two, and a `many` query refuses no count.
- **A database error** only where an input can make the server raise one, such as a duplicate key, a missing account or an invalid cast; a query that only reads by key has none.
- **No `commandTimeout` scenario**: no query sleeps, and a scenario's SQL before the call runs on the call's own connection, which cannot hold a lock against itself.  The end-to-end project's own tests cover the timeout.
- **A failure in a later chunk of a collection method that only reads** is a null item after the first chunk, since no input makes the read fail on the server.
- **No cancellation**: a synchronous method takes no token.

## The known differences

`KnownDifferences.cs` lists the differences that are Npgsql's limits, each a rewrite of the pair of outcomes that is applied only when the two differ.  A scenario that the rewrites make equal is skipped with each one's name and reason; one that still differs fails, showing the rewritten pair.  `KnownDifferencesTests` checks each condition of a rewrite on outcomes it writes itself.

| Name | Reason | Manual |
|----|----|----|
| `NpgsqlMultiplexingRefusesSyncIo` | With `Multiplexing=true` in the data source's connection string, Npgsql refuses a synchronous command on a connection with no transaction open, with `NotSupportedException`, where the asynchronous method runs | [A synchronous method needs a data source without multiplexing](../../../site/postgresql.md#method-results) |
