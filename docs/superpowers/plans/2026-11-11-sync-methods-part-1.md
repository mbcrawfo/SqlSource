# Synchronous Query Methods (Part 1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A new generator parameter, `method-type=async|sync|both`, gives a `methods` query a synchronous method in place of its asynchronous one, or beside it, doing exactly what the asynchronous method does to the database and the caller.

**Architecture:** `QueryMethod` gains a `MethodSynchrony` value.  `AsyncSpelling` becomes a strategy, `CallSpelling`, with an asynchronous and a synchronous implementation chosen in the output step by one exhaustive switch, as `ParameterBinder.For` chooses a binder; every phase file spells `await`, `await using`, the return type and the driver's calls through it.  The planner reads `method-type` and adds a synchronous `QueryMethod` (under `sync`, in place of the asynchronous one; under `both`, before it), with the names rule of the spec.  Collection methods and repository methods stay asynchronous in this part.

**Tech Stack:** C# source generator (netstandard2.0), ADO.NET (.NET 8+), Npgsql 8+, Microsoft.Data.SqlClient 6.1+, xUnit v3, Shouldly, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-11-11-sync-methods-design.md`, "Phases", part 1 ("Synchronous query methods").

## Global Constraints

- Values of `method-type`, in this order (their word indexes): `async`, `sync`, `both`; default `async`.  `method-type` alone reads as `on`, which is no word of it, so it is `SQLSRC111`.
- `method-type` applies at the outputs `methods` and `repository`; at `sql` and `models` it does nothing.
- A synchronous method has the query's name, never with `Async`.  Under `both`, every asynchronous method of the query ends in `Async`, whatever `async-method-suffix` says, a stream's and a collection method's too.
- A synchronous method takes the asynchronous method's arguments without `cancellationToken`; `MethodArguments.IsReserved` does not change, so `cancellationToken` stays reserved (`SQLSRC275`).
- It returns what the task holds (`T`, `T?`, the collection, `int`), or `void` for `none`.  Its body is the asynchronous body with `using` for `await using` and the synchronous driver call for each `*Async` call; its documentation is the asynchronous method's without `cancellationToken`.
- No generated code calls `.Result`, `.Wait()` or `GetAwaiter().GetResult()`.
- A query whose collection type is `IAsyncEnumerable`: under `both` it gets its asynchronous stream only (and its collection method, which returns an array); under `sync` it is the new error `SQLSRC298` at the query, and it gets no method and no collection method.  This applies to a repository's query too.
- In this part, collection methods and repository methods stay asynchronous under `sync` and `both`; the manual says so in a "Coming in a later release" callout on each.
- Driver floors unchanged: .NET 8, Npgsql 8, Microsoft.Data.SqlClient 6.1.
- An Npgsql data source with `Multiplexing=true` refuses synchronous I/O with `NotSupportedException`: a note in `docs/manual/postgresql.md`, not tech debt.
- A driver difference between a sync and an async call that would change what a caller's `catch` sees or what is left in the database: stop and report it to the manager before compensating.  Any other difference is a driver limit: a note in the engine's manual page and a test that asserts what the driver does.
- Every rule of the root `AGENTS.md` holds: docs, examples, schema, `AnalyzerReleases.Unshipped.md` and test readmes in the same PR; `./format.sh`, then `./pre-commit-validation.sh` before each commit; sidecars only from `tools/describe-end-to-end.sh`; golden files only from `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests`.

## Rulings in this plan

- **The sync copies of end-to-end queries are copies in their own file, `<Query>Sync` (or `<Query>Both`), each with `-- generator: method-type=sync` (or `both`)**, as `tests/DatabaseTests/*/EndToEnd/AGENTS.md` requires of a variant ("never a second claim of the file"), not new files claimed by types of their own as the spec's testing section says.  Each copy gets a `VariantCopyTests` row.
- **Interim names (this part only).**  Under `sync`, a query's collection method (still asynchronous) takes the synchronous method's name, the query's, as an overload by its `IEnumerable` argument, so part 2 makes it synchronous without renaming it.  Under `sync`, a repository query (still asynchronous) is named as under `async`: the query's name, with `Async` under `async-method-suffix`.  Under `both`, a repository query gets only its asynchronous method, named with `Async`.
- **The strategy is an abstract class, `CallSpelling`, with two singletons, `AsyncCallSpelling.Instance` and `SyncCallSpelling.Instance`**, chosen by `CallSpelling.For(MethodSynchrony)`.  `AsyncSpelling.cs` and `AsyncSpellingTests.cs` are replaced.
- **Order of a query's methods under `both`:** the synchronous method, then the asynchronous one, then the collection method.

## Review Focus

- **A synchronous method that throws on a connection it opened.**  The connection is closed again, as after a return: `UnexpectedRowCountException` on a closed connection leaves it `Closed`.  Test in Tasks 5 and 6.
- **Two queries `GetUser` and `GetUserAsync` under `both`.**  `GetUser`'s asynchronous name is taken by the other query's method: `SQLSRC274`, never CS0111.  Test in Task 3.
- **A token named `await` in a synchronous method.**  It is written `@await` and compiles.  Test in Task 2 (awkward-name rows).
- **`method-type` set in a settings file and `method-type=default` in a query marker.**  The query is `async`: the parameters merge per parameter.  Test in Task 1.
- **SQL Server's later error in a synchronous drain.**  A batch whose second statement fails after the rows throws the database's error, and a rolled-back caller's transaction gives `TransactionRolledBackException`, as the asynchronous method does.  Test in Task 6.

---

### Task 1: The `method-type` parameter

**Files:**
- Create: `src/SqlSource/Settings/MethodType.cs`
- Modify: `src/SqlSource/Settings/GeneratorParameters.cs`, `src/SqlSource/Settings/GeneratorParameterList.cs`, `src/SqlSource/Settings/QuerySettings.cs`, `schemas/settings-v1.schema.json`
- Modify: `docs/manual/settings.md` (Generator parameters row's Values cell, the table under `#setting-generator-parameters`), `docs/manual/diagnostics.md` (section of `SQLSRC109`), `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs` (its list of parameters)
- Test: `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`, `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`, `tests/SqlSource.Tests/Generator/GeneratorParameterLevelTests.cs`

**Interfaces:**
- Produces: `internal enum MethodType { Async = 0, Sync = 1, Both = 2 }`; `GeneratorParameters.MethodType = 1024`; `QuerySettings.MethodType` (`MethodType`, from `Parameters.Get(GeneratorParameters.MethodType)`).

- [ ] **Step 1: Write the failing tests**: `ReadWord_MethodTypeWords_ReadsEachIndex` (`method-type=async` → 0, `sync` → 1, `both` → 2, case ignored); `ReadWord_MethodTypeAlone_IsInvalidState`; `MethodType_NoLevelNamesIt_IsAsync`; `MethodType_SettingsFileSyncAndQueryDefault_IsAsync` (driver test through `LevelSettings`); a marker `-- generator: method-type` reports `SQLSRC111`.
- [ ] **Step 2: Run them** (`dotnet test --project tests/SqlSource.Tests -- --filter-class ...`) and see them fail to compile or fail.
- [ ] **Step 3: Implement**: the enum (members in word order, documented like `TransactionMode`), the flag, `new GeneratorParameterDefinition("method-type", GeneratorParameters.MethodType, ["async", "sync", "both"], 0)` after `transaction`, `All` includes it, `QuerySettings.MethodType`.  The schema's list and pattern; `SettingsFileSchemaTests` checks them against the definitions.  The manual rows say what each value does (status: accepted, effective at `methods`; collection and repository methods asynchronous for now with the callout from Task 7).
- [ ] **Step 4: Run** `tests/SqlSource.Tests` settings tests, `SettingsFileSchemaTests` and `tests/SqlSource.Repository.Tests` `DiagnosticsDocumentTests`: pass.
- [ ] **Step 5: Commit** `feat: add the method-type generator parameter`.

### Task 2: The spelling strategy and the synchronous method's text

**Files:**
- Create: `src/SqlSource/Generation/Methods/MethodSynchrony.cs`, `src/SqlSource/Generation/Emitting/QueryMethods/CallSpelling.cs`, `AsyncCallSpelling.cs`, `SyncCallSpelling.cs`, `DriverCall.cs` (same folder)
- Delete: `src/SqlSource/Generation/Emitting/QueryMethods/AsyncSpelling.cs`, `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/AsyncSpellingTests.cs`
- Modify: `QueryMethod.cs` (`Synchrony` init property), `MethodArguments.cs`, `QueryMethodContext.cs` (`Spelling`), `QueryMethodWriter.cs`, `HeaderWriter.cs`, `CommandWriter.cs`, `RowsWriter.cs`, `ExecutionCapabilityWriter.cs`, `CollectionMethods/BatchWriter.cs`, `CollectionMethods/BatchRowsWriter.cs`, `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md` (the `AsyncSpelling.cs` rule names `CallSpelling` and its two implementations)
- Test: create `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/CallSpellingTests.cs`; modify `QueryMethodWriterTests.cs`, `tests/SqlSource.Tests/Generation/Methods/MethodArgumentsTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/MethodWriterTests.cs` and `tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs` (awkward names in a sync method)

**Interfaces:**
- Produces:
  - `internal enum MethodSynchrony { Asynchronous = 0, Synchronous = 1 }`; `QueryMethod.Synchrony { get; init; }` (default `Asynchronous`, so every existing method is unchanged).
  - `internal enum DriverCall { Open, Close, OpenConnection, ExecuteReader, ExecuteNonQuery, ExecuteScalar, Read, NextResult, Dispose, BeginTransaction, Commit, Rollback }`.
  - `internal abstract class CallSpelling` with `static CallSpelling For(MethodSynchrony synchrony)` (one exhaustive switch, `UnexpectedEnumValueException` discard) and abstract members:
    - `StringBuilder AppendCallStart(StringBuilder builder, string target, DriverCall call)` → `await target.ReadAsync(` / `target.Read(`;
    - `StringBuilder AppendToken(StringBuilder builder, string token, bool afterArgument)` → async `[, ]token`, sync nothing;
    - `StringBuilder AppendCallEnd(StringBuilder builder)` → `).ConfigureAwait(false)` / `)`;
    - `StringBuilder AppendAwait(StringBuilder builder)` → `await ` / nothing (an awaited expression that is not a driver call, such as a factory's);
    - `StringBuilder AppendUsing(StringBuilder builder, string indent, string scope, string disposable)` → today's `await using var scope = ...ConfigureAwait(d, false);` / `using var scope = d;`;
    - `StringBuilder AppendReturnType(StringBuilder builder, string? resultType)` → ` async Task[<T>]` / ` T`, or ` void` for null;
    - `StringBuilder AppendInterfaceReturnType(StringBuilder builder, string? resultType)` → `Task[<T>]` / `T` or `void`.
  - `AsyncCallSpelling.IteratorModifiersOf(bool captures)` stays a static of the asynchronous spelling alone: a synchronous method never streams.
  - `QueryMethodContext.Spelling` (`CallSpelling`), set in `QueryMethodWriter.ContextOf` from `CallSpelling.For(method.Synchrony)`.
  - `MethodArguments.Of(QueryMethod, List<string>)` drops the cancellation token for a `Synchronous` method; the declaration's closing `)` (or `);` of an interface member) follows the last argument, whatever its role.
- No phase file spells `await`, `Async`, `ConfigureAwait` or a driver call's member name after this task: `grep -n 'Async"\|await \|ConfigureAwait' src/SqlSource/Generation/Emitting/QueryMethods/*.cs src/SqlSource/Generation/Emitting/QueryMethods/CollectionMethods/*.cs` finds them only in `AsyncCallSpelling.cs`.

- [ ] **Step 1: Write the failing tests**:
  - `CallSpellingTests`: each `DriverCall` of each spelling gives the expected text (`Open` → `await c.OpenAsync(` / `c.Open(`, `Close` → `CloseAsync` / `Close`, and the other ten pairs), the token, the end, `AppendUsing` and both return types, `void` for null.
  - `MethodArgumentsTests`: a synchronous method's declaration has no `cancellationToken` and ends `int? commandTimeout = null)` ; the `GetEnumerator_AndEachForm_AllocatesNothing` test covers a synchronous form too.
  - `QueryMethodWriterTests`: a synchronous method of each shape (`none` → `void`, `rowcount` → `int`, `one`, `one-optional`, `many` in a `List`) has no `async`, `await`, `Task`, `ConfigureAwait` or `cancellationToken`, opens with `connection.Open()`, closes with `connection.Close()`, reads with `reader.Read()`, and a SQL Server one drains with `reader.NextResult()` and disposes with `reader.Dispose()`; its documentation has no `<param name="cancellationToken">`.
  - Awkward names: the rows of `Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles` and its `_CompilesOnTheNewestCSharp` sibling run once more under `-- generator: method-type=sync` (a token `await` included).
- [ ] **Step 2: Run them** and see them fail.
- [ ] **Step 3: Implement** the types, then move every phase file onto `context.Spelling`.  The asynchronous text must stay byte for byte the same.
- [ ] **Step 4: Run** `tests/SqlSource.Tests` whole, the golden corpus included, without `SQLSOURCE_UPDATE_GOLDEN`: every golden file unchanged, every test passes.
- [ ] **Step 5: Commit** `refactor: make the query method's spellings a strategy with a synchronous form`.

### Task 3: Planning synchronous methods, the names and `SQLSRC298`

**Files:**
- Create: `src/SqlSource/Generation/Methods/MethodNames.cs`
- Modify: `src/SqlSource/Generation/Methods/MethodPlanner.cs`, `src/SqlSource/Generation/Placement/TypePlacement.cs` (`SQLSRC009` through `MethodNames`), `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/Diagnostics/AGENTS.md` ("`SQLSRC200` to `SQLSRC298` are taken"), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (section `SQLSRC298`; `SQLSRC274` and `SQLSRC009` sections mention the names under `both`)
- Test: create `tests/SqlSource.Tests/Generator/SynchronousMethodTests.cs`; modify `tests/SqlSource.Tests/Generator/NestedClassTests.cs` or `PlacementDiagnosticsTests.cs` (`SQLSRC009` under `both`)

**Interfaces:**
- Consumes: `QuerySettings.MethodType` (Task 1), `QueryMethod.Synchrony` (Task 2).
- Produces:
  - `internal static class MethodNames` with `string AsyncNameOf(string query, QuerySettings settings)` (the query's name, with `Async` under `async-method-suffix` or `method-type=both`) and `bool HasSynchronous(QuerySettings settings)` / `bool HasAsynchronous(QuerySettings settings)` (whether the query's output can give it such a method, shape aside, with this part's interim rules); `TypePlacement` and `MethodPlanner` both use it, and their own `AsyncSuffix` constants go.
  - `SqlDiagnostics.StreamNeedsAsyncMethod`: `SQLSRC298`, error, `NotConfigurable`, at the query's name; message `"The query '{0}' streams its rows as an IAsyncEnumerable, which has no synchronous method; give it method-type=async or method-type=both"`; title `"A stream has no synchronous method"`.
- The planner, for a query whose output has a method:
  - `async`: today's methods.
  - `sync`, output `methods`: a `Synchronous` method named the query's name, then its collection method (asynchronous, `Batch` set, the same name); a stream: `SQLSRC298` and nothing.
  - `sync`, output `repository`: the asynchronous method as under `async`, unless it streams (`SQLSRC298`, nothing).
  - `both`, output `methods`: the `Synchronous` method (the query's name), the asynchronous one (`<Query>Async`), then the collection method (`<Query>Async`); a stream: only the asynchronous stream and its collection method.
  - `both`, output `repository`: only the asynchronous method, `<Query>Async`.
  - Each name of a query's methods other than a collection method's goes through `FindTakenReason`, `FindTakenBySource`, `FindTakenByInterface` and `AddPlanned`'s clash check, which report `SQLSRC274` naming the name that clashed.

- [ ] **Step 1: Write the failing driver tests** in `SynchronousMethodTests`:
  - `Run_MethodTypeSync_WritesOnlyASynchronousMethod` (the generated source has `public static GetUserDto GetUser(` and no `Task`), and the run is clean and compiles;
  - `Run_MethodTypeBoth_WritesGetUserAndGetUserAsync`, also with `async-method-suffix=off`;
  - `Run_MethodTypeBothAndAQueryNamedWithAsync_ReportsSQLSRC274` (queries `GetUser` and `GetUserAsync`);
  - `Run_MethodTypeBothAndAMemberNamedGetUserAsync_ReportsSQLSRC274`;
  - `Run_MethodTypeSyncStream_ReportsSQLSRC298AndWritesNoMethod` (with `collection-method` on: no collection method either);
  - `Run_MethodTypeBothStream_WritesOnlyTheAsynchronousStreamNamedAsync`;
  - `Run_MethodTypeSyncCollectionMethod_WritesTheSynchronousMethodAndAnAsynchronousCollectionMethodOfOneName`;
  - `Run_MethodTypeSyncRepository_WritesTheAsynchronousMethod` and `Run_MethodTypeBothRepository_WritesOnlyTheAsynchronousMethodNamedAsync`, interface member included;
  - `Run_MethodTypeSyncRepositoryStream_ReportsSQLSRC298`;
  - `Run_MethodTypeSyncAtModels_WritesNoMethod` (no diagnostic, same output as `async`);
  - `Run_MethodTypeBothNestedClassNamedGetUserAsync_ReportsSQLSRC009`.
- [ ] **Step 2: Run them** and see them fail.
- [ ] **Step 3: Implement** `MethodNames`, the planner's branches (a helper that builds the query's list of methods, beside `AddCollectionMethod`), the descriptor (in `All`, in id order), the release row and the manual section.
- [ ] **Step 4: Run** `tests/SqlSource.Tests` and `tests/SqlSource.Repository.Tests`: pass; golden files unchanged.
- [ ] **Step 5: Commit** `feat: give a query synchronous methods under method-type`.

### Task 4: The golden corpus dimension

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/Corpus/QueryMethodCorpus.cs` (and the part files whose scenario functions need a `settings` argument), `tests/README.md` if its corpus section names scenarios
- Create: golden files, by `SQLSOURCE_UPDATE_GOLDEN=1` only

**Interfaces:**
- Consumes: Tasks 1-3.
- Produces shared scenarios, each run on both engines, over the project's settings file with `{"generatorParameters": ["method-type=sync"]}` or `["method-type=both"]`: `shapes-sync`, `shapes-both`, `collections-sync` (the stream's `SQLSRC298` in its diagnostics), `collections-both`, `tokens-sync`, `lists-sync`, `converters-sync`, `repository-connection-string-sync`, `repository-connection-string-both`, and `collection-method-sync` and `collection-method-both` (a query with `collection-method` on, one of each shape).  SQL Server's drains and rollback checks are in its `shapes-*` files.

- [ ] **Step 1: Add the scenarios** and run the corpus with `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests` alone.
- [ ] **Step 2: Run it again without the variable**: pass, on `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor`.
- [ ] **Step 3: Read every new golden file**: no `await`, `Task`, `ConfigureAwait` or `cancellationToken` in a synchronous method, no compiler error or warning, existing files unchanged.
- [ ] **Step 4: Commit** `test: add method-type to the golden corpus`.

### Task 5: PostgreSQL end-to-end tests and the Npgsql 8 project

**Files:**
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Methods.sql`, `Lists.sql`, `Streaming.sql`, `Accounts.sql` and the query file of a token and of a converter (each copy beside its original); their sidecars by `tools/describe-end-to-end.sh`; `VariantCopyTests.cs` (a row per copy); `tests/DatabaseTests/Postgres/EndToEnd/AGENTS.md` if it names test classes
- Create: `tests/DatabaseTests/Postgres/EndToEnd/SynchronousMethodTests.cs`
- Modify: `tests/DatabaseTests/Postgres.Npgsql8/` (its linked queries and a `SynchronousQueryTests.cs`), its `AGENTS.md` if it lists files; `tests/DatabaseTests/Postgres/README.md` where it lists what the project covers

**Interfaces:**
- Consumes: Tasks 1-3.  The copies: `TouchAccountSync` (rowcount), `TouchAccountQuietlySync` (none), `GetAccountInRangeSync` (one), `FindAccountInRangeSync` (one-optional), `ListAccountsWhereSync` (many), `GetThenRaiseSync` (error after the row), `ListAccountsByIdsSync` (a list), a token query's and a converter query's `…Sync`, `ListThenWaitSync` or the query `RunTimeTests` times out with, and `StreamAccountsBoth` (a stream under `both`); plus `GetAccountInRangeBoth` (both methods).

- [ ] **Step 1: Add the copies** and run `tools/describe-end-to-end.sh` to write their entries; add the `VariantCopyTests` rows.
- [ ] **Step 2: Write the tests** in `SynchronousMethodTests` (synchronous calls only: `source.OpenConnection()`, no `await` on the method), as the existing tests are written:
  - each shape's rows on an open and a closed connection, and its refused counts (`UnexpectedRowCountException` for none and two rows of `one`, two rows of `one-optional`), with the connection `Closed` again after the return and after the exception when the method opened it, and `Open` when the caller had opened it;
  - `GetThenRaiseSync`: the database's `PostgresException`, with its `SqlState`, after the row;
  - the caller's transaction: a write inside it, rolled back, leaves nothing;
  - `commandTimeout` expiring throws what the asynchronous method throws (compare type and `SqlState`);
  - one list, one token and one conversion;
  - `GetAccountInRangeBoth` and `GetAccountInRangeBothAsync` read the same row;
  - `StreamAccountsBoth` has no synchronous method: `typeof(…).GetMethod("StreamAccountsBoth")` is null, and `StreamAccountsBothAsync` streams.
- [ ] **Step 3: Npgsql 8**: synchronous copies of each shape's row reading (one test for each shape), linked as that project links its queries.
- [ ] **Step 4: Run** `tests/DatabaseTests/Postgres` and `tests/DatabaseTests/Postgres.Npgsql8` on the default image and with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`: pass.  Any difference from the asynchronous method's behaviour: stop and report it.
- [ ] **Step 5: Commit** `test: run synchronous methods end to end on PostgreSQL`.

### Task 6: SQL Server end-to-end tests and the SqlClient 6.1 project

**Files:**
- As Task 5, under `tests/DatabaseTests/SqlServer/EndToEnd/` and `tests/DatabaseTests/SqlServer.SqlClient61/`, with `tests/DatabaseTests/SqlServer/README.md`.

**Interfaces:**
- Consumes: Tasks 1-3.  The same kinds of copies, from the SQL Server project's files, plus copies of the queries its existing tests use for the rollback checks: one whose later statement rolls back the caller's transaction (`TransactionRolledBackException`), the refusal of a completed transaction, and an error after the rows (`SqlException` with its `Number`).

- [ ] **Step 1: Add the copies**, describe them, add `VariantCopyTests` rows.
- [ ] **Step 2: Write `SynchronousMethodTests`** with the cases of Task 5 Step 2 and the three SQL Server checks above; the timeout test holds its command on a lock as `RunTimeArgumentTests` does (root `tests/AGENTS.md`, "Flaky tests").
- [ ] **Step 3: SqlClient 6.1**: synchronous copies of each shape's row reading.
- [ ] **Step 4: Run** `tests/DatabaseTests/SqlServer` and `tests/DatabaseTests/SqlServer.SqlClient61` on the default image and with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`: pass.
- [ ] **Step 5: Commit** `test: run synchronous methods end to end on SQL Server`.

### Task 7: Synchronous allocation pairs

**Files:**
- Modify: `tests/DatabaseTests/QueryMethodAllocation/Postgres/AllocationTests.cs`, `SqlServer/AllocationTests.cs`, its `.csproj` links if a new query file is linked, `README.md`, `AGENTS.md` (a line for the synchronous pairs)

**Interfaces:**
- Consumes: the `…Sync` copies of Tasks 5 and 6 of every query the project measures that is not a stream (add the missing copies, `GetAccountNameUnpooledSync` and the like, in the engine project's files, described and with `VariantCopyTests` rows).
- Produces: for each, a hand-written synchronous ADO.NET method of the same calls, and a pair test under the same `Allocations` assertions and margins.

- [ ] **Step 1: Add the copies and the hand-written methods and pairs.**
- [ ] **Step 2: Run** `dotnet test --project tests/DatabaseTests/QueryMethodAllocation -p:RunQueryMethodAllocation=true` for each engine's namespace, as its readme says, alone and without coverage: pass.
- [ ] **Step 3: Commit** `test: measure the allocations of synchronous methods`.

### Task 8: The manual, the architecture page and the examples

**Files:**
- Modify: `docs/manual/generator.md` (a section on synchronous methods: `method-type`, names, arguments, return types, what is not there — cancellation, streams — and a "Coming in a later release" callout for synchronous collection methods and one for synchronous repository methods, word for word as root `AGENTS.md` gives), `docs/manual/settings.md` (status), `docs/manual/postgresql.md` (the `Multiplexing=true` note), `docs/manual/README.md` ("What works today"), `README.md` (an FAQ answer, absolute links), `docs/architecture/generator.md` (the strategy row of "Adding a kind of method"'s table and its paragraph, `MethodSynchrony`, `MethodNames`, `SQLSRC298`), `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md`
- Modify: `examples/PostgresExamples/Methods/` and `examples/SqlServerExamples/Methods/` (a query under `method-type=sync` or `both` and a call of it, sidecars by `tools/describe-end-to-end.sh`), each example `README.md`

- [ ] **Step 1: Write the docs and the examples.**
- [ ] **Step 2: Run** `tests/SqlSource.Repository.Tests` (links, anchors, SQL samples, `AGENTS.md` limits) and build the examples: pass.
- [ ] **Step 3: Commit** `docs: document synchronous query methods`.

### Task 9: Hand-over notes

- [ ] Write `p1-notes.md` in the shared notes folder: the names `MethodSynchrony`, `CallSpelling`, `DriverCall`, `MethodNames`, the interim rules parts 2 and 3 remove, where the callouts are, and `SQLSRC298` (so part 3 takes `SQLSRC299`).
