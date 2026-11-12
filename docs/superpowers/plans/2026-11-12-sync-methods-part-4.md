# Synchronous Methods (Part 4): the Nightly Sync/Async Comparison Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A nightly suite runs every synchronous query method of the end-to-end projects beside its asynchronous twin, with the same inputs and setup, compares what the two do to the caller and to the database, and goes red on any difference that is not a known driver limit.

**Architecture:** Two projects, `tests/DatabaseTests/Postgres.SyncAsync` and `tests/DatabaseTests/SqlServer.SyncAsync`, each copying its engine's end-to-end queries, sidecars and settings files into `obj/Mirror/` at build time, below a project `sqlsource.json` that sets `method-type=both`, with claims of their own.  A driver-free core in `tests/DatabaseTests/TestHarness/SyncAsync/` declares scenarios, finds each method pair by reflection, runs the asynchronous method and then the synchronous one each from the same starting database, records an `Outcome` of each, and compares them, rewriting through a list of known differences.  Each project holds its engine's adapter (connections, fresh databases, error codes, transaction state), its scenarios, one theory over every scenario and one coverage test.

**Tech Stack:** C# (.NET 10 test projects), ADO.NET, Npgsql, Microsoft.Data.SqlClient, xUnit v3, Shouldly, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-11-11-sync-methods-design.md`, "Nightly: sync and async side by side" and "Phases", part 4 ("The nightly suite").

## Global Constraints

- Built with the solution, run only by its own jobs in `.github/workflows/nightly.yml`, one per engine on its default image, as `QueryMethodAllocation` is.  A red job is a signal, not a gate.
- Every query of the end-to-end projects that can have a pair has one, under `method-type=both`.  A query's own marker still wins: a `<Query>Sync` copy keeps its synchronous method alone, and a stream (`IAsyncEnumerable`) has no synchronous method.
- A scenario names a query, its inputs and its setup: a closed or open connection, the caller's transaction or none, the `transaction` value of a collection method (that value is the query's own marker, so a scenario picks it by picking the query).  It runs the asynchronous method, records the outcome, puts the database back, runs the synchronous method, records its outcome, and compares the two.  It holds no expected values.
- An outcome is: the result, compared structurally (rows in order, counts); the exception, if any, by its type, its inner exception's type, the driver's error code or SQL state, `ItemIndex` and its message; the state of the connection and of the caller's transaction afterwards; and the rows of the tables the query touches.
- Coverage: every pair, with the inputs that give each shape's ordinary result, its refused row counts, a database error, and for collection methods an empty collection, a chunk boundary and a failure in a later chunk.
- A difference in the declared list of known differences, each a driver limit with its reason, is reported and does not fail.  The list starts with the driver limits Parts 1 to 3 recorded in the manual (`site/postgresql.md`, "A synchronous method needs a data source without multiplexing"; the SqlClient message of a pending transaction, "ExecuteReader" against "BeginExecuteReader").
- A driver difference that would change what a caller's `catch` sees or what is left in the database is not a known difference: stop and ask the manager before compensating.
- Root `AGENTS.md` rules hold: `./format.sh`, then `./pre-commit-validation.sh` before each commit; sidecars only from `tools/describe-end-to-end.sh` (this unit writes none: the mirror copies them); images by `EngineImage.<Engine>.Default`, never a tag.

## Rulings in this plan (where the merged code differs from the spec)

- **Two projects, not one.**  The generator resolves a short type name of a marker (`AccountId`, `OrderMetadata`) across the whole compilation, and both engines' end-to-end projects have one of each name, so one assembly over both engines fails with SQLSRC250.  Each engine gets its project: `Postgres.SyncAsync` and `SqlServer.SyncAsync`, named as `Postgres.Npgsql8` names a variant of `Postgres`.
- **Copied, not linked.**  The spec says the project links the queries "under a settings file that sets `method-type=both`".  A linked `.sql` file takes the settings files of the folder it really is in (`docs/architecture/msbuild-and-manifest.md`, "Finding the settings files"), so a link keeps the end-to-end project's settings.  The target `SyncAsyncMirrorQueries` copies the `.sql`, `.sql.json` and `sqlsource.json` files of the end-to-end project (and the engine project's root `sqlsource.json`) into `obj/Mirror/`, before `SqlSourceListFiles` as the manual tells a target that adds `.sql` files to, and adds them as `AdditionalFiles`; the project's own `sqlsource.json` above `obj/` gives `method-type=both`, which no end-to-end settings file sets.  The copies are not sidecars written by hand: they are the end-to-end project's, byte for byte.
- **The types the queries name are the projects' own copies**, in the end-to-end namespaces (`SqlSource.DatabaseTests.Postgres.EndToEnd.Types`, `...Json.Types`, and SQL Server's), since `TestLayoutTests` forbids compiling another project's file.  They only need to compile and convert: both methods of a pair use the same ones.
- **Putting the database back** is a fresh database for each run of a scenario that writes: PostgreSQL clones a template (`CREATE DATABASE ... TEMPLATE`), SQL Server restores a backup taken after the seed.  A scenario that only reads runs on the shared seeded database.
- **A known difference is a rewrite of the pair of outcomes**, applied only after the two differ; the scenario is skipped with the names and reasons of the rewrites that made them equal, and fails, showing the rewritten pair, when they still differ.  A skip shows in the run's summary, which lists skipped tests.
- **Coverage is checked by a test**: every pair the reflection finds has a scenario, every scenario names a pair, and every collection-method pair has an `Empty`, a `ChunkBoundary` and a `LaterChunkFailure` scenario.  The other kinds (refused counts, database error) depend on what a query's SQL can return, so they are written per query where they exist, and the README says so.

## File Structure

`tests/DatabaseTests/TestHarness/SyncAsync/` (driver-free, internal, `InternalsVisibleTo` both projects; unit tests in `tests/SqlSource.Tests/TestHarness/SyncAsync/`):

- `Scenario.cs` — `Scenario`, `Setup`, `ConnectionStart`, `CallerTransaction`, `MethodKind`, `ScenarioTag`.
- `MethodPair.cs` — `MethodPair` and `MethodPairs.Find(Assembly)`: the reflection that finds pairs and invokes them.
- `Rendering.cs` — `Rendering.Of(object?)`: the canonical text of a result or a row.
- `Outcome.cs` — `Outcome`, `Failure`.
- `KnownDifference.cs` — `KnownDifference` and `PairVerdict`, `Verdicts.Of(...)`.
- `IEngine.cs` — what an engine project gives the runner.
- `PairRunner.cs` — runs one scenario's two calls and returns the verdict.
- `ScenarioCoverage.cs` — the coverage checks.

Each project (`tests/DatabaseTests/<Engine>.SyncAsync/`):

- `<Engine>.SyncAsync.csproj`, `sqlsource.json`, `packages.lock.json`, `README.md`, `AGENTS.md`.
- Claims mirroring the end-to-end project's, one file each (from the prototype, see Task 3), and `Types/`, `Json/Types/` copies.
- `<Engine>Engine.cs` (the `IEngine`), the fixture (`<Engine>Databases.cs`) and its collection definition.
- `Scenarios/<File>Scenarios.cs`, one per end-to-end query file, each a `static IEnumerable<Scenario> All`.
- `KnownDifferences.cs`, `PairTests.cs` (the theory and the coverage test).

## Review Focus

- **A result that is a pooled collection or holds a `JsonElement`, a geometry or a NodaTime value.**  `Rendering.Of` gives each a stable text (no `object.ToString()` type name, no recursion into a geometry's factory), so equal results render equal.  Tests in Task 1.
- **An exception message holding text that changes between runs (a SqlClient `ClientConnectionId`, a GUID).**  Masked before comparison, or every SQL Server failure would differ.  Test in Task 1.
- **The second run starting from a database the first changed** (an identity value, a row).  A writing scenario gets a fresh database each run; test in Task 3 (`PairRunner` gives each run its own database when `Writes`).
- **A method that leaves the connection open after an exception, or a transaction aborted.**  The outcome records `ConnectionState` and the transaction's state after each call, read before anything is disposed.  Test in Task 3 (a `DatabaseError` scenario on a closed connection, and one in a caller's transaction).
- **A pair the reflection misses** (a private method, a repository's instance method, a collection overload).  The coverage test fails on a generated pair with no scenario; Tasks 3 and 5 run it.

---

### Task 1: Rendering, outcomes and verdicts (driver-free, unit-tested)

**Files:**
- Create: `tests/DatabaseTests/TestHarness/SyncAsync/{Rendering,Outcome,KnownDifference}.cs`
- Test: `tests/SqlSource.Tests/TestHarness/SyncAsync/{RenderingTests,VerdictsTests}.cs`

**Interfaces (produces):**

```csharp
internal static class Rendering
{
    // Canonical, culture-invariant text of a value: null; string quoted; numbers "R"/invariant; bool; enum Type.Name;
    // DateTime with Kind, DateTimeOffset round-trip ("O"); Guid; byte[] hex; char;
    // JsonElement/JsonDocument/JsonNode raw text; a type whose ToString is overridden outside a compiler-generated
    // record (NodaTime values, NetTopologySuite geometries via their own ToString/AsText) by ToString();
    // IEnumerable (not string) "[a, b]"; any other object "TypeName { P = v, ... }" over public instance properties in
    // declaration order (ModelReflection), depth-limited to 8.
    public static string Of(object? value);

    // A message with every GUID replaced by "<guid>".
    public static string Masked(string message);
}

internal sealed record Failure(string Type, string? InnerType, string? Code, int? ItemIndex, string Message);

internal sealed record Outcome(
    string? Result,                       // Rendering.Of of the returned value, "<void>" for none; null when it threw
    Failure? Failure,
    string ConnectionState,               // DbConnection.State after the call, before disposal
    string TransactionState,              // "none" without a caller's transaction, else the engine's text
    IReadOnlyList<string> Tables);        // one rendered table per Setup.Tables entry, in order

internal sealed record KnownDifference(
    string Name, string Reason, Func<Scenario, Outcome, Outcome, (Outcome Async, Outcome Sync)> Rewrite);

internal enum Agreement { Same, Known, Different }

internal sealed record PairVerdict(Agreement Agreement, IReadOnlyList<KnownDifference> Applied, string Report);

internal static class Verdicts
{
    // Same when equal; else applies each known difference in order, keeping those that changed the pair: Known when
    // equal after, Different otherwise.  Report names each field that differs, async then sync, after the rewrites.
    public static PairVerdict Of(Scenario scenario, Outcome async, Outcome sync, IReadOnlyList<KnownDifference> known);
}
```

- [ ] **Step 1: Write the failing tests.**  `RenderingTests`: `Of_Null_IsNull`; `Of_RecordWithArray_RendersElements` (`new Row(1, [1, 2])` → `Row { Id = 1, Values = [1, 2] }`); `Of_DecimalAndDouble_AreInvariant` under `CultureScope("de-DE")`; `Of_JsonElement_IsRawText`; `Of_ListOfRecords_KeepsOrder`; `Of_DateTime_IncludesKind`; `Masked_Guid_IsReplaced` (`"ClientConnectionId:0f8fad5b-d9cb-469f-a165-70867728950e"` → `"ClientConnectionId:<guid>"`).  `VerdictsTests`: `Of_EqualOutcomes_IsSame`; `Of_DifferentMessage_IsDifferent` (Report contains both messages); `Of_DifferenceAKnownRewriteRemoves_IsKnown` (Applied names it); `Of_KnownRewriteThatLeavesADifference_IsDifferent`; `Of_KnownRewriteThatChangesNothing_IsNotApplied`.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tests --filter-class "*SyncAsync*"`.  Expected: FAIL (types missing).
- [ ] **Step 3:** Implement.  `Scenario` is needed by `KnownDifference`; define it here as in Task 2's Interfaces.
- [ ] **Step 4:** Run the same command.  Expected: PASS.
- [ ] **Step 5:** `./format.sh`, `./pre-commit-validation.sh`, commit.

### Task 2: Scenarios, pairs and the runner (driver-free)

**Files:**
- Create: `tests/DatabaseTests/TestHarness/SyncAsync/{Scenario,MethodPair,IEngine,PairRunner,ScenarioCoverage}.cs`
- Test: `tests/SqlSource.Tests/TestHarness/SyncAsync/{MethodPairsTests,ScenarioCoverageTests}.cs` (on hand-written fake pairs in the test file: a static class with `Get(DbConnection connection, Args parameters, DbTransaction? transaction = null, int? commandTimeout = null)` and `GetAsync(..., CancellationToken cancellationToken = default)`, a collection overload, and an instance repository class)

**Interfaces (produces):**

```csharp
internal enum MethodKind { Query, Collection }
internal enum ConnectionStart { Closed, Open }
internal enum CallerTransaction { None, Begun }
internal enum ScenarioTag { Ordinary, NoRow, TooManyRows, DatabaseError, Empty, ChunkBoundary, LaterChunkFailure, Other }

internal sealed record Setup(
    ConnectionStart Connection = ConnectionStart.Open,
    CallerTransaction Transaction = CallerTransaction.None,
    string Source = "default",            // the engine's name of a data source: "default", "multiplexing", "nodatime", "gis", "billing"
    bool Writes = false,                  // a fresh database for each run
    IReadOnlyList<string>? Tables = null, // SELECTs whose rows the outcome records, each with its ORDER BY
    string? Prepare = null);              // SQL run on the connection before the call

internal sealed record Scenario(
    string Name,                          // "<Host>.<Method>: <what>", unique per engine; the theory's row
    Type Host,                            // the type that declares both methods
    string Method,                        // the synchronous method's name; the asynchronous one is Method + "Async"
    MethodKind Kind,
    ScenarioTag Tag,
    Setup Setup,
    object? Parameters = null,            // the input model, or the items of a collection method
    IReadOnlyDictionary<string, string>? Tokens = null,
    int? CommandTimeout = null,
    Func<DbConnection, DbDataSource, object>? Instance = null);   // makes the instance of a repository's method (a pair with IsInstance)

internal sealed record MethodPair(Type Host, string Method, MethodKind Kind, bool IsInstance, MethodInfo Sync, MethodInfo Async)
{
    // Calls the method with arguments by parameter name: connection, parameters, each token, transaction,
    // commandTimeout, cancellationToken (CancellationToken.None); awaits a Task, reads Task<T>.Result.  Returns the
    // value, or rethrows the method's own exception (unwrapping TargetInvocationException).
    public Task<object?> InvokeAsync(MethodInfo method, object? instance, DbConnection connection, DbTransaction? transaction, Scenario scenario);
}

internal static class MethodPairs
{
    // Every method M and MAsync declared on one non-interface type of the assembly (any accessibility, static or
    // instance), where MAsync returns Task or Task<T> and takes M's parameters plus a trailing CancellationToken.
    // Kind: Collection when "parameters" is IEnumerable<T> of a model, else Query; IsInstance for a repository's
    // instance methods.  A pair is keyed by Host, Method and Kind, since a collection method overloads the query's.
    public static IReadOnlyList<MethodPair> Find(Assembly assembly);
}

internal interface IEngine
{
    DbDataSource SourceOf(string source, string? database);   // database null: the shared seeded one
    Task<string> FreshDatabaseAsync(string source);            // a copy of the seed; returns its name
    Task DropDatabaseAsync(string database);
    string? CodeOf(DbException exception);                     // SqlState on PostgreSQL, Number on SQL Server
    Task<string> TransactionStateAsync(DbConnection connection, DbTransaction transaction);
}

internal static class PairRunner
{
    // For each of Async then Sync: a fresh database when Setup.Writes; a connection from SourceOf, opened when
    // Setup.Connection is Open (also when Transaction is Begun); Prepare run; a transaction begun when Begun; the call;
    // ConnectionState and TransactionState read; each Setup.Tables SELECT run on the call's connection in its
    // transaction when both still work, else on a new connection; the transaction rolled back and everything
    // disposed, then the fresh database dropped.  ItemIndex is read from the exception's "ItemIndex" property when
    // it has one.  Returns Verdicts.Of(...).
    public static Task<PairVerdict> RunAsync(IEngine engine, MethodPair pair, Scenario scenario, IReadOnlyList<KnownDifference> known);
}

internal static class ScenarioCoverage
{
    // Lines naming each pair with no scenario, each scenario with no pair, each collection pair missing one of
    // Empty, ChunkBoundary, LaterChunkFailure, and each duplicate scenario name.  Empty when covered.
    public static IReadOnlyList<string> GapsOf(IReadOnlyList<MethodPair> pairs, IReadOnlyList<Scenario> scenarios);
}
```

- [ ] **Step 1: Write the failing tests.**  `MethodPairsTests`: `Find_FakePairs_FindsQueryCollectionAndRepository` (three pairs: a static query pair, its collection overload, and an instance pair with `IsInstance`); `Find_MethodWithoutAsyncTwin_IsNotAPair`; `InvokeAsync_AsyncMethod_ReturnsTaskResult`; `InvokeAsync_MethodThatThrows_ThrowsItsException` (not `TargetInvocationException`).  `ScenarioCoverageTests`: `GapsOf_PairWithoutScenario_NamesIt`; `GapsOf_ScenarioWithoutPair_NamesIt`; `GapsOf_CollectionPairWithoutChunkBoundary_NamesIt`; `GapsOf_DuplicateName_NamesIt`; `GapsOf_Covered_IsEmpty`.
- [ ] **Step 2:** Run `dotnet test --project tests/SqlSource.Tests --filter-class "*SyncAsync*"`.  Expected: FAIL.
- [ ] **Step 3:** Implement.  `PairRunner` has no unit test: it needs a server, and Tasks 3 and 5 run it.  `ScenarioCoverage` matches a scenario to a pair by `Host`, `Method` and `Kind`.
- [ ] **Step 4:** Run the same command.  Expected: PASS.
- [ ] **Step 5:** `./format.sh`, `./pre-commit-validation.sh`, commit.

### Task 3: PostgreSQL: the project, engine, fixture, tests, and scenarios of the core query files

A prototype of both projects (`.csproj` with the `SyncAsyncMirrorQueries` target, `sqlsource.json`, and the claims, one file per end-to-end claim file, in the end-to-end layout under `SqlSource.DatabaseTests.<Engine>.SyncAsync`) is kept in the scratchpad folder named in the task's brief; it builds with only CA1812 (claims never instantiated) and links the end-to-end `Types/` files, which `TestLayoutTests` forbids.

**Files:**
- Create: the project from the prototype, with the linked `Compile` items replaced by copies in `Types/` and `Json/Types/` (same namespaces and members as the end-to-end originals, and a comment that they are this project's copies), and `packages.lock.json` (by `dotnet restore`).
- Modify: `SqlSource.slnx`; `tools/test-scopes.txt` (`none tests/DatabaseTests/Postgres.SyncAsync/*` beside the `QueryMethodAllocation` line, its comment widened); `tests/SqlSource.Repository.Tests/Validation/TestScopesFileTests.cs` (its row beside the allocation row); `tests/DatabaseTests/TestHarness/TestHarness.csproj` (`InternalsVisibleTo` of both projects, and its comment's count of projects).
- Create in `tests/DatabaseTests/Postgres.SyncAsync/`: `PostgresEngine.cs`, `PostgresDatabases.cs` (fixture: a subclass of `PostgisContainer` on `EngineImage.Postgres.Default` with `app`, `billing` and `gis` from `EndToEndFiles.SchemaAndSeed`, then one template each, `<db>_seed`, made with `CREATE DATABASE <db>_seed TEMPLATE <db>` after `NpgsqlConnection.ClearAllPools()`), `UsesPostgres.cs` (collection, `DisableParallelization = true`), `KnownDifferences.cs`, `PairTests.cs`, `Scenarios/AllScenarios.cs` (concatenates each file's `All`), and `Scenarios/<File>Scenarios.cs` for `Accounts`, `Methods`, `Shapes`, `Lists`, `Notes`, `Collections`, `Locations`, `Cube`, `Constants`, `RowBuffers`, `Streaming`.

**Interfaces:**
- Consumes Task 1 and 2.
- `PostgresEngine.SourceOf`: `"default"` maps `NoteKind` as `EndToEndSources.App` does; `"multiplexing"` adds `Multiplexing=true`; `"nodatime"` adds `UseNodaTime()`; `"gis"` uses database `gis` with `UseNetTopologySuite()`; `"billing"` the `billing` database.  `FreshDatabaseAsync` creates `run_<n>` from the source's `_seed` template; `DropDatabaseAsync` clears the pool and runs `DROP DATABASE ... WITH (FORCE)`.  `CodeOf` is `PostgresException.SqlState` (or `DbException.SqlState`).  `TransactionStateAsync`: `"completed"` when `transaction.Connection` is null, else `"active"`, or `"aborted"` when `SELECT 1` in it fails with `25P02`.
- `PairTests` (`[Collection(UsesPostgres.Name)]`): `RunAsync_Scenario_SyncMatchesAsync(string name)`, a theory over `AllScenarios.Names`: Same passes, Known calls `Assert.Skip` with each applied name and reason, Different fails with the Report.  `Find_GeneratedPairs_EachHasItsScenarios` asserts `ScenarioCoverage.GapsOf(MethodPairs.Find(typeof(PairTests).Assembly), AllScenarios.List)` is empty.
- `KnownDifferences.All`: `NpgsqlMultiplexingRefusesSyncIo` — when `Setup.Source == "multiplexing"` and the sync outcome's failure is `System.NotSupportedException`, the sync outcome is replaced by the async one; reason quoting the manual's note.

- [ ] **Step 0:** Run `dotnet test --project tests/SqlSource.Repository.Tests` after adding the project.  Expected: PASS (layout, scopes, references).
- [ ] **Step 1:** Write `PairTests` and the coverage test with `AllScenarios` empty; run `dotnet test --project tests/DatabaseTests/Postgres.SyncAsync -p:RunSyncAsync=true --filter-method "*Find_GeneratedPairs*"`.  Expected: FAIL listing every pair.
- [ ] **Step 2:** Write the scenarios of the listed files, reading each query's SQL and the seed (`tests/DatabaseTests/Postgres/EndToEnd/app/seed.sql`) to pick inputs: for each pair an `Ordinary` scenario on an open connection and one on a closed connection; `NoRow` and `TooManyRows` where the shape refuses them and some input gives them; `DatabaseError` where the query can raise one (the `*ThenRaise` queries, a constraint); one `CallerTransaction.Begun` scenario per file for a query that writes, with its `Tables`; tokens and lists once each where a file has them; a `commandTimeout` that expires once (`ListThenWait` held by an advisory lock taken in `Prepare` on another connection is not possible within one connection: use `pg_sleep` only if a query has it, else skip timeouts here and say so in the README); two `"multiplexing"` scenarios (one query method on a closed connection, one in a caller's transaction).
- [ ] **Step 3:** Run the coverage test again.  Expected: it lists only the pairs of files Task 4 covers.
- [ ] **Step 4:** Run the theory: `dotnet test --project tests/DatabaseTests/Postgres.SyncAsync -p:RunSyncAsync=true --filter-method "*RunAsync_Scenario*"`.  Expected: every scenario passes or is skipped as known.  A failure is either a runner bug (fix it) or a driver difference: report it to the manager, as Global Constraints says, before going on.
- [ ] **Step 5:** `./format.sh`, `./pre-commit-validation.sh`, commit.

### Task 4: PostgreSQL: the remaining query files

**Files:** `Scenarios/<File>Scenarios.cs` for `Batches`, `Repository`, `AllTypes`, `Billing/Invoices` (claimed by `InvoiceQueries`), `NodaTime`, `NetTopologySuite`, `Json`; `AllScenarios.cs`.

- [ ] **Step 1:** Write the scenarios.  `Batches.sql` is claimed by `BatchQueries`, so its methods are extension methods of the connection or, for its repository queries, instance methods of `BatchQueries` (`Instance = (connection, _) => new BatchQueries(connection)`).  For each collection pair: `Empty` (no items), `ChunkBoundary` (1,000 and 1,001 items: two scenarios, the second tagged `ChunkBoundary` too), `LaterChunkFailure` (item 1,500 names an account that fails a constraint, or repeats an id), each with `Writes = true` and `Tables = ["SELECT id, account_id, body FROM notes ORDER BY id"]`; and one `CallerTransaction.Begun` scenario per `transaction` copy.  Repository: each of the four repositories (`DataSourceRepository`, `ConnectionStringRepository`, `FactoryRepository`, `BorrowedRepository`) for `GetNote` (found, missing), `ListNotesSync`'s original has no pair (stream) and `TouchNote` with `Writes = true`.
- [ ] **Step 2:** Run the coverage test.  Expected: PASS.
- [ ] **Step 3:** Run the whole project: `dotnet test --project tests/DatabaseTests/Postgres.SyncAsync -p:RunSyncAsync=true`.  Expected: PASS (known differences skipped).  Note the wall time for the job's timeout.
- [ ] **Step 4:** `./format.sh`, `./pre-commit-validation.sh`, commit.

### Task 5: SQL Server: the project, engine, fixture, tests, and scenarios of the core query files

**Files:** as Task 3 (the project from the prototype included), in `tests/DatabaseTests/SqlServer.SyncAsync/`, for `Accounts`, `Methods`, `Shapes`, `Lists`, `Notes`, `Collections/*` (three claims), `Locations`, `Constants`, `RowBuffers`, `Streaming`, `Cancellation`, `ClrTypes`, `ExternalRefs`, `NewTypes`, `Writes`.

**Interfaces:**
- `SqlServerDatabases`: a subclass of `SqlServerContainer` on `EngineImage.SqlServer.Default` with `app` (schema, new types, seed) and `billing`, then `BACKUP DATABASE <db> TO DISK = '/var/opt/mssql/data/<db>_seed.bak' WITH INIT` each, and the logical file names from `RESTORE FILELISTONLY`.
- `SqlServerEngine.FreshDatabaseAsync` restores the backup as `run_<n>` `WITH MOVE` each file to `/var/opt/mssql/data/run_<n>_<logical>`; `DropDatabaseAsync` clears the pool, `ALTER DATABASE ... SET SINGLE_USER WITH ROLLBACK IMMEDIATE`, `DROP DATABASE`.  `CodeOf` is `SqlException.Number` as text.  `TransactionStateAsync`: `"completed"` when `transaction.Connection` is null, else `"xact_state=<XACT_STATE()> trancount=<@@TRANCOUNT>"` read in the transaction.  Sources: `"default"`, `"billing"` (database `billing`), `"unpooled"` (`Pooling=false`, for a scenario whose `Prepare` sets a session option such as `SET XACT_ABORT ON`).
- `KnownDifferences.All`: `SqlClientPendingTransactionMessage` — when both failures are `System.InvalidOperationException` and the messages differ only in `BeginExecuteReader`/`ExecuteReader` (also `BeginExecuteNonQuery`/`ExecuteNonQuery`), the sync message is replaced by the async one; reason quoting the manual.

- [ ] **Steps 0–5:** as Task 3, with SQL Server's scenarios: the rollback checks of the end-to-end tests (`TransactionRolledBackException` by a query that rolls back inside, the refusal of a completed transaction, the error after the rows: the `*ThenRaise` queries), a caller's transaction that is not passed (`BorrowedRepository`'s or a method's, a known difference), and a `commandTimeout` that expires (`WAITFOR DELAY` only if a query has it; otherwise as Task 3 says).

### Task 6: SQL Server: the remaining query files

As Task 4, for `Batches` (each `transaction` copy, `XACT_ABORT` ones on `"unpooled"`), `Repository` (three repositories), `AllTypes`, `Billing/Invoices`, `NodaTime/*`, `NetTopologySuite/*`, `Json/*` (`JsonColumns.sql` needs 2025's `json` type: the default image has it).

### Task 7: The jobs and the documents

**Files:**
- Modify: `.github/workflows/nightly.yml` (jobs `sync-async-postgres` and `sync-async-sqlserver`, modelled on `allocation-*`: build the project, `tools/pull-test-images.sh`, `dotnet test --project ... -p:RunSyncAsync=true`, `timeout-minutes` from Task 4's and Task 6's wall time plus half; the header comment and the Test step's comment name the two projects), `.github/workflows/README.md`, `.github/AGENTS.md` (a rule like the allocation one), `CONTRIBUTING.md` (one sentence per project), `tests/README.md` (counts, the project table, the graph, the references section), `tests/DatabaseTests/README.md` (table rows and the run section), `tests/DatabaseTests/AGENTS.md` (the containers and references rules), `tests/DatabaseTests/Postgres/README.md` and `tests/DatabaseTests/SqlServer/README.md` where they list who links the end-to-end queries, `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md` and `SqlServer/EndToEnd/AGENTS.md` (a rule: a new query with a pair gets its scenarios in the engine's `SyncAsync` project, or the coverage test fails at night).
- Create: `README.md` and `AGENTS.md` of each project.  The README: what a red job means; how to read a failure (the Report's fields); the command to run it; how scenarios are declared and how the database is put back; coverage and what is not covered (with reasons); the table of known differences, each with its reason and manual link.  The AGENTS.md: the mirror and why; own types; every pair needs scenarios; no expected values; a known difference only for a driver limit, with its manual note, never one that changes a `catch` or the database; the maintenance footer.

- [ ] **Step 1:** Write them; run `dotnet test --project tests/SqlSource.Repository.Tests` (links, AGENTS.md limits).  Expected: PASS.
- [ ] **Step 2:** `./format.sh`, `./pre-commit-validation.sh`, commit.
- [ ] **Step 3:** Full check: `./pre-commit-validation.sh --full`, and both projects run with `-p:RunSyncAsync=true`.  Expected: PASS.
