# Synchronous Collection Methods (Part 2) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A query's collection method follows `method-type` as its query method does: under `sync` it is synchronous, under `both` the query gets a synchronous and an asynchronous collection method, each doing to the database and the caller exactly what the asynchronous collection method does.

**Architecture:** Part 1 already spells every call of a collection method's body through `context.Spelling` (`BatchWriter`, `BatchRowsWriter`, `ExecutionCapabilityWriter`), so a `QueryMethod` with `Batch` set and `Synchrony = Synchronous` already writes a synchronous collection method.  This part is the planner's: `MethodPlanner.MethodsOf` gives the query a collection method for each synchrony the query has, rather than one asynchronous collection method after its last method.  The rest is tests, the golden corpus, the manual, the architecture page and the examples.

**Tech Stack:** C# source generator (netstandard2.0), ADO.NET (.NET 8+), Npgsql 8+, Microsoft.Data.SqlClient 6.1+, xUnit v3, Shouldly, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-11-11-sync-methods-design.md`, "Collection methods", "Streaming" and "Phases", part 2.  Part 1's plan: `docs/superpowers/plans/2026-11-11-sync-methods-part-1.md`.

## Global Constraints

- A synchronous collection method fills `DbBatch`es in chunks of 1,000 and runs them with `ExecuteReader` or `ExecuteNonQuery`; it begins its own transaction with `BeginTransaction`, at the isolation level `transaction` gives, and ends it with `Commit` or `Rollback`.  Everything else is the asynchronous collection method's: an empty collection opens nothing, items are checked as their chunk is filled, a caller's or an ambient transaction is used and never ended, `CollectionItemException` carries the item's position, the PostgreSQL batch command's trailing comment is cut, SQL Server's session isolation is restored and its checks of several result sets apply.
- It takes the asynchronous collection method's arguments without `cancellationToken`, returns what that method's task holds (the collection, `int`, or `void` for `none`) and has its documentation without `cancellationToken`, `OperationCanceledException` and, for `none`, the `<returns>`.
- **Names.**  A synchronous collection method has the query's name, an overload by its `IEnumerable` argument; an asynchronous one has the query's asynchronous name (`MethodNames.AsyncNameOf`).
- **Which collection methods a query gets**, when `collection-method` is on and the query takes parameters:
  - output `methods`: `async` → the asynchronous one; `sync` → the synchronous one; `both` → both.  A stream (`IAsyncEnumerable`) under `both` gets both too, since its collection method returns an array; under `sync` it is still `SQLSRC298` with no method at all.
  - output `repository`: unchanged from part 1 (one asynchronous collection method, after the asynchronous method); part 3 changes it.
- **Order of a query's methods:** the synchronous method, the asynchronous method, the synchronous collection method, the asynchronous collection method, each when the query has it.
- **Name checks.**  A collection method that shares its name with a method of the same query is checked where that method is, as today.  A synchronous collection method of a stream under `both` has no such method (its query method is `<Query>Async`), so its name, the query's, is checked as a method's is: `FindTakenReason`, `FindTakenBySource`, `FindTakenByInterface` and the clash with another query's method (`SQLSRC274`), never CS0111.
- No generated code calls `.Result`, `.Wait()` or `GetAwaiter().GetResult()`.
- A driver difference between a sync and an async call that would change what a caller's `catch` sees or what is left in the database: stop and report it to the manager before compensating.  Any other difference is a driver limit: a note in the engine's manual page and a test that asserts what the driver does.
- Every rule of the root `AGENTS.md` holds: docs, examples and test readmes in the same PR; `./format.sh`, then `./pre-commit-validation.sh` before each commit; sidecars only from `tools/describe-end-to-end.sh`; golden files only from `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests`.

## Where the merged code differs from the spec

- The spec names `AsyncSpelling` as the strategy; part 1 built it as `CallSpelling` (`AsyncCallSpelling`, `SyncCallSpelling`) with `DriverCall`, and already moved the collection writers onto it.  No emitter change is planned; Task 1 proves it.
- The spec's testing section says copies "claimed by types of their own"; part 1 ruled, by `tests/DatabaseTests/*/EndToEnd/AGENTS.md`, that a variant is a copy `<Query>Sync` / `<Query>Both` in the same file with a `VariantCopyTests` row.  This part does the same in each engine's `Queries/Batches.sql`.

## Review Focus

- **A synchronous collection method that throws in its second chunk under `transaction=on`.**  Its own transaction is rolled back and nothing is left; the connection it opened is closed again.  Tasks 3 and 4.
- **A stream under `both` with `collection-method`.**  It gets `<Query>Async` (stream), `<Query>(IEnumerable<…>)` (synchronous, array) and `<Query>Async(IEnumerable<…>, …)` (asynchronous, array); a member named as the query is `SQLSRC274`.  Task 1.
- **SQL Server's session isolation after a synchronous collection method under a level.**  The connection's next statement runs at the level it ran at before.  Task 4.
- **An ambient `TransactionScope`.**  The synchronous method begins no transaction of its own.  Tasks 3 and 4.

---

### Task 1: Plan synchronous collection methods

**Files:**
- Modify: `src/SqlSource/Generation/Methods/MethodPlanner.cs` (`FormsOf`, `MethodsOf`, `CollectionMethodOf`, the name checks before `new QueryMethod` and the clash loop that skips a `Batch` method; their comments lose "In this part it is asynchronous"), `src/SqlSource/Generation/Methods/MethodNames.cs` (the doc of `HasAsynchronous` loses its sentence on a collection method under `sync`)
- Test: `tests/SqlSource.Tests/Generator/SynchronousMethodTests.cs` (replace `Run_MethodTypeSyncCollectionMethod_WritesTheSynchronousMethodAndAnAsynchronousCollectionMethodOfOneName`, add the tests below), `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/QueryMethodWriterTests.cs` (a synchronous collection method of each shape)

**Interfaces:**
- Consumes: `MethodSynchrony`, `QueryMethod.Synchrony`, `MethodNames`, `CallSpelling` from part 1.
- Produces: the planner's list of a query's methods as the Global Constraints say.  How `FormsOf` marks a form that has a collection method and no query method (a stream's synchronous form under `both`) is the implementer's choice, as long as the name checks cover it.

- [ ] **Step 1: Write the failing tests** in `SynchronousMethodTests` (driver tests, compiled with no error or warning):
  - `Run_MethodTypeSyncCollectionMethod_WritesASynchronousCollectionMethod`: `-- generator: collection-method, method-type=sync`, a `one` query: the source has `GetUser(` returning the row and `GetUser(` taking `IEnumerable<GetUserParams>` returning `GetUserDto[]`, neither with `Task` or `cancellationToken`.
  - `Run_MethodTypeBothCollectionMethod_WritesBothCollectionMethods`: four methods in the order of the Global Constraints, the synchronous collection method named `GetUser`, the asynchronous one `GetUserAsync`.
  - `Run_MethodTypeBothStreamCollectionMethod_WritesTheStreamAndBothCollectionMethods`: `StreamUsersAsync` returns `IAsyncEnumerable`, `StreamUsers(IEnumerable<…>)` returns an array, `StreamUsersAsync(IEnumerable<…>, …)` returns `Task<…[]>`.
  - `Run_MethodTypeBothStreamCollectionMethodAndAMemberOfItsName_ReportsSQLSRC274` (the type has a method `StreamUsers`).
  - `Run_MethodTypeBothStreamCollectionMethodAndAnotherQueryOfItsName_ReportsSQLSRC274`, when the type's queries allow it (another query `StreamUsers` would be a duplicate query; use a nested-class or extension-class clash that can happen, or drop this test with a line in the hand-back saying why).
  - `Run_MethodTypeSyncNoneCollectionMethod_ReturnsVoidAndDocumentsNoReturn`.
  - `Run_MethodTypeSyncRepositoryCollectionMethod_StaysAsynchronous` (part 3 changes it).
  - In `QueryMethodWriterTests`: a synchronous collection method of each shape has no `async`, `await`, `Task`, `ConfigureAwait` or `cancellationToken`; calls `BeginTransaction(`, `ExecuteReader(` or `ExecuteNonQuery(`, `Commit(`, `Rollback(`; and on SQL Server under a level reads and restores the session's isolation with `ExecuteScalar(` and `ExecuteNonQuery(`.
- [ ] **Step 2: Run them** (`dotnet test --project tests/SqlSource.Tests -- --filter-class …`) and see them fail.
- [ ] **Step 3: Implement** the planner change.  If any emitter text is wrong for a synchronous collection method, fix it in the phase file through `context.Spelling`, never by spelling a call there.
- [ ] **Step 4: Run** `tests/SqlSource.Tests` whole without `SQLSOURCE_UPDATE_GOLDEN`: only `collection-method-sync` and `collection-method-both` golden files may differ (they become Task 2's); every other test passes.
- [ ] **Step 5: Commit** `feat: give a query synchronous collection methods under method-type` (with the two golden files regenerated by `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests` if that is the only way the commit validates; otherwise in Task 2).

### Task 2: The golden corpus

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/Corpus/QueryMethodCorpus.cs` (`CollectionMethod`: under `method-type=both` only, a stream query `StreamUsers` with `collection-type: IAsyncEnumerable` and `collection-method`, with its sidecar entry)
- Regenerate: `tests/SqlSource.Tests/Generator/Golden/{postgres,sqlserver}/collection-method-sync.txt`, `collection-method-both.txt`, by `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests` alone

- [ ] **Step 1: Add the stream** and run the corpus with `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests` alone.
- [ ] **Step 2: Run it again without the variable** on `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`: pass.
- [ ] **Step 3: Read the four files**: each synchronous collection method has no `await`, `Task`, `ConfigureAwait` or `cancellationToken`; the SQL Server ones read and restore the isolation level synchronously; no other golden file changed.
- [ ] **Step 4: Commit** `test: give synchronous collection methods to the golden corpus`.

### Task 3: PostgreSQL end-to-end tests and the Npgsql 8 project

**Files:**
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Batches.sql` (copies, each `-- generator: method-type=sync` or `both` beside the header's `collection-method`), its sidecar by `tools/describe-end-to-end.sh`, `tests/DatabaseTests/Postgres/EndToEnd/VariantCopyTests.cs` (a row per copy)
- Create: `tests/DatabaseTests/Postgres/EndToEnd/SynchronousBatchTests.cs`
- Modify: `tests/DatabaseTests/Postgres.Npgsql8/BatchQueryTests.cs` (synchronous copies of its failed-item tests), its `AGENTS.md` if it lists what it covers; `tests/DatabaseTests/Postgres/README.md` where it lists what the project covers

**Interfaces:**
- The copies: `InsertNoteSync` (one), `InsertNoteRowCountSync` (rowcount), `InsertNoteQuietlySync` (none), `FindNotesSync` (many), `FindNoteSync` (one-optional), `InsertNoteNoTransactionSync` (off), `InsertNoteSerializableSync`, `InsertNoteReadCommittedSync`, `InsertNoteRepeatableReadSync`, `InsertNoteBoth` (both), and a stream copy `FindNotesStreamBoth` (`collection-type: IAsyncEnumerable`, `both`).

- [ ] **Step 1: Add the copies**, run `tools/describe-end-to-end.sh` for their entries, add the `VariantCopyTests` rows.
- [ ] **Step 2: Write `SynchronousBatchTests`**, synchronous calls only (`source.OpenConnection()`, no `await` on a method under test), as `BatchQueryTests` is written (temporary `notes` table for a test whose method commits):
  - each shape over one item, three items and 1,001 items, with its rows in the order of the items; an empty collection returns empty and leaves a closed connection closed; a closed connection is closed again after a return and after an exception;
  - a failure in the second chunk under `on` (`InsertNoteSync`, a duplicate key: nothing left), `off` (`InsertNoteNoTransactionSync`: the first chunk kept), each level (`…SerializableSync`, `…ReadCommittedSync`, `…RepeatableReadSync`: nothing left), and the caller's transaction (used, not committed; after an item's error, its rollback left to the caller);
  - `CollectionItemException` at item 2 with its `ItemIndex` and its `PostgresException` (`SqlState` `23505`); a null item in the second chunk rolls the first back; an item with no row under `one` throws `UnexpectedRowCountException` and rolls back;
  - a deferred constraint failing at the commit throws the database's error, as the asynchronous test does;
  - each level copy runs at its level (`current_setting('transaction_isolation')`);
  - an ambient `TransactionScope` (synchronous, no `TransactionScopeAsyncFlowOption`): the method begins none, and a rolled-back scope leaves nothing;
  - `InsertNoteBoth` and `InsertNoteBothAsync` each insert their items; `FindNotesStreamBoth(items)` returns an array and `FindNotesStreamBothAsync(items, …)` too, and there is no synchronous `FindNotesStreamBoth` without items (reflection).
- [ ] **Step 3: Npgsql 8**: synchronous copies of `BatchQueryTests`' failed-item tests (the duplicate key at item 2 through `InsertNoteSync`), linked as that project links `Batches.sql`.
- [ ] **Step 4: Run** `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/Postgres.Npgsql8` on the default image and with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`: pass.  Any difference from the asynchronous method's behaviour: stop and report it.
- [ ] **Step 5: Commit** `test: run synchronous collection methods end to end on PostgreSQL`.

### Task 4: SQL Server end-to-end tests and the SqlClient 6.1 project

**Files:**
- As Task 3, under `tests/DatabaseTests/SqlServer/EndToEnd/` (`Queries/Batches.sql`, `VariantCopyTests.cs`, a new `SynchronousBatchTests.cs`), `tests/DatabaseTests/SqlServer.SqlClient61/BatchQueryTests.cs` and `tests/DatabaseTests/SqlServer/README.md`.

**Interfaces:**
- The copies of Task 3 that SQL Server's `Batches.sql` has, plus its `transaction` values PostgreSQL lacks (`read-uncommitted`, `snapshot`) where the file has them, and copies of the queries its tests use for: the refusal of a completed transaction, `TransactionRolledBackException` after a later rollback in the caller's transaction, a result set of another width (`InsertNoteThenOther`), the same-width checks (`FindAccountThenEmpty`), `XACT_ABORT ON`, and a conversion error in the method's own transaction.

- [ ] **Step 1: Add the copies**, describe them, add `VariantCopyTests` rows.
- [ ] **Step 2: Write `SynchronousBatchTests`** with the cases of Task 3 Step 2 that apply, and: the completed transaction refused; `TransactionRolledBackException`; the result-set checks; `XACT_ABORT ON` in the method's own and the caller's transaction; a level copy in the caller's `RepeatableRead` transaction and in a `TransactionScope` leaves the session's level as it was; `ItemIndex` and the `SqlException`'s `Number`.
- [ ] **Step 3: SqlClient 6.1**: synchronous copies of its failed-item tests.
- [ ] **Step 4: Run** `tests/DatabaseTests/SqlServer` and `tests/DatabaseTests/SqlServer.SqlClient61` on the default image and with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`: pass.  Any difference from the asynchronous method: stop and report it.
- [ ] **Step 5: Commit** `test: run synchronous collection methods end to end on SQL Server`.

### Task 5: The manual, the architecture page and the examples

**Files:**
- Modify: `docs/manual/generator.md` (in "Synchronous methods", replace the paragraph on the collection method staying asynchronous and its callout with what a synchronous collection method is: its names under `sync` and `both`, its arguments and return, the stream under `both`; and in "Collection methods" a line that `method-type` gives a synchronous one, with `BeginTransaction`/`Commit` not named — effects only), `docs/manual/settings.md` (the `method-type` row: collection methods follow it; repository methods still asynchronous), `docs/manual/README.md` ("What works today": the synchronous row covers collection methods; the "Coming" row narrows to repository methods), `docs/manual/recipes.md` if its "run without awaiting" recipe says collection methods are asynchronous, `README.md` if its FAQ says so, `docs/architecture/generator.md` (the planner paragraph: a collection method for each synchrony, the order, the name check of a stream's synchronous collection method)
- Modify: `examples/PostgresExamples/Methods/UserQueries.sql`, `.cs` and `examples/SqlServerExamples/Methods/UserQueries.sql`, `.cs` (a query with `collection-method` under `method-type=sync`, and a call of its synchronous collection method), their sidecars by `tools/describe-end-to-end.sh`, each example `README.md` where it lists what the project shows

- [ ] **Step 1: Write the docs and the examples.**  `git grep -n "Coming in a later release" -- README.md src/SqlSource.Tool/README.md docs/manual`: only the repository callout remains for synchronous methods.
- [ ] **Step 2: Run** `tests/SqlSource.Repository.Tests` (links, anchors, SQL samples, `AGENTS.md` limits) and build the examples: pass.
- [ ] **Step 3: Commit** `docs: document synchronous collection methods`.

### Task 6: Hand-over notes

- [ ] Write `p2-notes.md` in the shared notes folder: how `MethodsOf` now orders a query's methods, how the stream's synchronous collection method's name is checked, the copies added to each `Batches.sql`, and that a repository's collection method is still asynchronous for part 3 to change.
