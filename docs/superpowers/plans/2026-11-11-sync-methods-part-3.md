# Synchronous Repository Methods (Part 3) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A repository's methods, and their members of its interface, follow `method-type` as the extension methods do: under `sync` a repository query gets a synchronous method, under `both` it gets both.  A synchronous method gets its connection from a source that returns no task; a database may have a pair of marked sources, one that returns a task for the asynchronous methods and one that does not for the synchronous ones.

**Architecture:** Parts 1 and 2 already spell every call of a method's body, the interface's members included, through `context.Spelling`, and plan a synchronous and an asynchronous form of each query (`MethodPlanner.FormsOf`); `MethodNames.HasSynchronous` and `HasAsynchronous` keep a repository asynchronous.  This part lifts that rule, lets `ConnectionResolver` resolve a pair of sources for one database, gives each form of a repository query the source of its synchrony, adds `SQLSRC299` for a synchronous form whose database has only a source that returns a task, and teaches `CommandWriter.AppendAcquire` to pass `default` to a factory's token in a synchronous method.  The rest is tests, the golden corpus, the manual, the architecture page and the examples.

**Tech Stack:** C# source generator (netstandard2.0), ADO.NET (.NET 8+), Npgsql 8+, Microsoft.Data.SqlClient 6.1+, xUnit v3, Shouldly, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-11-11-sync-methods-design.md`, "Repositories", "Diagnostics" and "Phases", part 3.  Earlier plans: `docs/superpowers/plans/2026-11-11-sync-methods-part-1.md`, `…-part-2.md`.

## Global Constraints

- **Which methods a repository query gets** is what an extension method's query gets, by `method-type`: `async` → asynchronous; `sync` → synchronous, named as the query; `both` → both, the asynchronous one always with `Async`.  Its collection method follows in the same way (part 2's `MethodsOf`).  A stream is still `SQLSRC298` under `sync`, and under `both` keeps its asynchronous stream alone, with both collection methods when it has one.
- **The interface** has a member for each public repository method on the type: under `both`, both members.
- **Sources.**  A connection string, a data source, a borrowed connection and a factory that returns a `DbConnection` serve both kinds of method.  A factory that returns `Task<…>` or `ValueTask<…>` serves asynchronous methods only.
- **A pair.**  A database whose marked members are exactly two, of which exactly one returns a task, resolves to both: asynchronous methods use the one that returns a task, synchronous methods the other.  Two that both return a task, or both do not, or three or more, are `SQLSRC029` as today.  Each member of a pair goes through `Take`'s checks (`SQLSRC036`, `SQLSRC033`); a problem of either fails the database.  A pair whose non-task source no synchronous method uses is not an error.  Conventions never find a factory, so a pair is always two marked members.
- **A synchronous method gets its connection** with `new …Connection(string)` and `Open()`, `(dataSource).OpenConnection()`, the factory's call and `Open()` when closed, or the borrowed connection and `Open()` when closed.  A factory that takes a `CancellationToken` is passed `default` by a synchronous method, and the method's own `cancellationToken` by an asynchronous one.  `default` is spelled by `CallSpelling`, never by `CommandWriter`.
- **`SQLSRC299`** (Error, `NotConfigurable`, at the query's name, from `MethodPlanner`): a repository query that would have a synchronous form, whose database's only source returns a task.  The message names the query, the database, the type and the source, and says to mark a source that returns no task beside it, or to give the query `method-type=async`.  Under `sync` the query gets no method at all; under `both` it keeps its asynchronous method (and its asynchronous collection method) and loses only the synchronous form.  It is reported whatever other problems the query has, and it never stops the asynchronous method on its own.
- **`SQLSRC029`** keeps its id and title; its message adds that a pair of which exactly one returns a task is allowed (exact words are the implementer's), and its manual section explains the pair.
- **Unchanged:** `MethodArguments.IsReserved`, `SQLSRC275`'s reserved names and `SQLSRC036` for a primary-constructor parameter named `cancellationToken`, whatever the methods' `method-type`.
- **Name checks** run per form with that form's own source: `FindTakenBySource`, `FindTakenByInterface`, and `HiddenSourceReason` (`SQLSRC275`) for each source a kept form uses.  A form dropped by `SQLSRC299` checks no name.
- **A synchronous repository method with no argument** (no input, no tokens) is declared `Name()` on the type and in the interface, never `Name(\n)`.
- No generated code calls `.Result`, `.Wait()` or `GetAwaiter().GetResult()`, and a synchronous method never calls a `Task`-returning source.
- A driver difference between a sync and an async call that would change what a caller's `catch` sees or what is left in the database: stop and report it to the manager before compensating.  Any other difference is a driver limit: a note in the engine's manual page and a test that asserts what the driver does.
- Every rule of the root `AGENTS.md` holds: docs, examples and test readmes in the same PR; `./format.sh`, then `./pre-commit-validation.sh` before each commit; sidecars only from `tools/describe-end-to-end.sh`; golden files only from `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests`.

## Where the merged code differs from the spec

- The spec names `AsyncSpelling`; parts 1 and 2 built `CallSpelling` (`AsyncCallSpelling`, `SyncCallSpelling`).  The token passed to a factory goes through a new member of it.
- The spec's "claimed by types of their own" for end-to-end copies: part 1 ruled a variant is a copy `<Query>Sync` / `<Query>Both` in its own file, with a `VariantCopyTests` row.  This part adds them to each engine's `Queries/Repository.sql`, which every repository of `RepositoryQueries.cs` claims, so `FactoryRepository` gets a synchronous factory beside its asynchronous one (the pair), or its synchronous copies would be `SQLSRC299`.
- Part 1 left a synchronous repository method with no argument written `Name(\n)`; this part fixes it (Global Constraints).

## Review Focus

- **The pair.**  An asynchronous method calls the task factory with its token, the synchronous one the other factory with `default`; neither calls the other's.  Tasks 1, 2, 4 and 5.
- **`SQLSRC299` under `both`.**  The asynchronous method, its interface member and its asynchronous collection method stay; nothing synchronous is written; the build fails with the error.  Task 2.
- **A borrowed closed connection** is closed again after a synchronous method returns and after it throws, and an open one is left open.  Tasks 4 and 5.
- **SQLSRC029** is still reported for two non-task or two task sources, and for three.  Task 1.

---

### Task 1: Resolve a pair of sources

**Files:**
- Modify: `src/SqlSource/Generation/Repositories/ConnectionResolver.cs` (`Resolve`'s `switch` on `members.Count`, `Resolving.Take`), `ResolvedConnection.cs`, `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (`SQLSRC029`'s message), `src/SqlSource/Generation/Methods/MethodPlanner.cs` (`SourceOf`, only to compile: it keeps returning the asynchronous source until Task 2), `docs/manual/diagnostics.md` (`SQLSRC029`'s message in its sample, and its explanation of the pair)
- Test: `tests/SqlSource.Tests/Generation/Repositories/ConnectionResolverTests.cs`

**Interfaces:**
- Produces: `ResolvedConnection(string Database, ConnectionSource Asynchronous, ConnectionSource? Synchronous)` (names are the implementer's), where `Asynchronous` is the source of asynchronous methods (the one source, or the pair's task one) and `Synchronous` that of synchronous methods (the one source when it returns no task, the pair's other one, or null when the only source returns a task), and a member `SourceFor(MethodSynchrony)`.

- [ ] **Step 1: Write the failing tests** in `ConnectionResolverTests`: a pair of an async factory and a sync factory resolves both, each to its side; a pair of an async factory and a data source, a connection string or a borrowed connection; two async factories → `SQLSRC029`; two non-task marked members → `SQLSRC029` (existing test still passes); three marked, one async → `SQLSRC029`; a pair for a named `Database` beside another database's single source; a pair whose one member fails `SQLSRC033` or `SQLSRC036` fails the database; a lone async factory resolves with no synchronous source; a lone non-task source is both.
- [ ] **Step 2: Run them** (`dotnet test --project tests/SqlSource.Tests -- --filter-class *ConnectionResolverTests`) and see them fail.
- [ ] **Step 3: Implement.**  Update `SQLSRC029`'s message and its manual sample and explanation (`tests/SqlSource.Repository.Tests` checks the manual against the descriptor).
- [ ] **Step 4: Run** `tests/SqlSource.Tests` and `tests/SqlSource.Repository.Tests`: pass, no golden file changed.
- [ ] **Step 5: Commit** `feat: resolve a repository's pair of task and non-task connection sources`.

### Task 2: Plan and write synchronous repository methods

**Files:**
- Modify: `src/SqlSource/Generation/Methods/MethodNames.cs` (`HasSynchronous`, `HasAsynchronous` and their docs: a repository follows `method-type` as `methods` does), `MethodPlanner.cs` (`QueryContext` carries the resolved connection; each `MethodForm` carries its source; `MethodsOf` stamps `Source` per form; `SQLSRC299`; name and token checks per source; the class summary and comments that say a repository stays asynchronous), `src/SqlSource/Generation/Emitting/QueryMethods/CallSpelling.cs`, `AsyncCallSpelling.cs`, `SyncCallSpelling.cs` (a member for the token passed to a connection source: the token's name, or `default`), `CommandWriter.cs` (`AppendSourceOrThrow` uses it), `src/SqlSource/Generation/Methods/MethodArguments.cs` or `HeaderWriter.cs` (an empty argument list is `()`), `src/SqlSource/Diagnostics/SqlDiagnostics.cs` (`SQLSRC299`, in `All`), `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (its row, its section, and the paragraph after the table that lists the methods' errors), `src/SqlSource/Diagnostics/AGENTS.md` ("`SQLSRC200` to `SQLSRC299` are taken"), `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md` if its spelling rule needs the new member named
- Test: `tests/SqlSource.Tests/Generator/SynchronousMethodTests.cs` (replace `Run_MethodTypeSyncRepositoryCollectionMethod_StaysAsynchronous`, `Run_MethodTypeSyncRepository_WritesTheAsynchronousMethod` and `Run_MethodTypeBothRepository_WritesOnlyTheAsynchronousMethodNamedAsync`), a new `tests/SqlSource.Tests/Generator/SynchronousRepositoryTests.cs` if `SynchronousMethodTests` grows past what one class should hold, `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/RepositoryMethodWriterTests.cs`, `tests/SqlSource.Tests/Generation/Methods/MethodNamesTests.cs` if it exists, `tests/SqlSource.Tests/Generation/Placement/TypePlacementTests.cs` (`SQLSRC009` for a repository's nested class named as its synchronous method under `both`)

**Interfaces:**
- Consumes: `ResolvedConnection.SourceFor` from Task 1; `MethodForm`, `FormsOf`, `MethodsOf` from parts 1 and 2.
- Produces: the methods of a repository query as the Global Constraints say, in part 2's order (sync method, async method, sync collection method, async collection method).

- [ ] **Step 1: Write the failing tests** (driver tests, compiled with no error or warning unless an error is expected):
  - `sync` with each source kind (connection string, data source, borrowed, non-task factory without and with a token): one `public GetUser(` with no `async`, `Task`, `await`, `ConfigureAwait` or `cancellationToken`, which acquires as the Global Constraints say (`.OpenConnection()`, `new global::Npgsql.NpgsqlConnection(`, `Open(default)` for the factory with a token), and an interface member `global::App.GetUserDto GetUser(`.
  - `both`: `GetUser` and `GetUserAsync` on the type and in the interface; with a collection method, the four methods in order and their four interface members.
  - A pair (async factory with a token + sync factory with a token) under `both`: `GetUserAsync` calls `OpenAsync(cancellationToken)`, `GetUser` calls `Open(default)`, and neither calls the other's; under `async` the sync factory is unused and nothing is reported.
  - `SQLSRC299`: only an async factory, under `sync` → the error at the query's name and no method on the type or interface; under `both` → the error, `GetUserAsync` on the type and in the interface, no `GetUser`; under `both` with `collection-method` → the async collection method kept, the sync one gone; under `async` → nothing reported.
  - `SQLSRC274` for a synchronous method named as the pair's synchronous member (a query `Open` beside a marked `Open()`), and `SQLSRC275` for a token named as the pair's synchronous member's primary-constructor parameter.
  - `SQLSRC036` still for a parameter named `cancellationToken` under `sync`.
  - A synchronous repository method of a query without parameters or tokens is `CountUsers()`, on the type and in the interface.
  - A `none` query under `sync`: `public void …(` with no `<returns>`.
- [ ] **Step 2: Run them** and see them fail.
- [ ] **Step 3: Implement.**  Fix any emitted text in its phase file through `context.Spelling`, never by spelling a call there.
- [ ] **Step 4: Run** `tests/SqlSource.Tests` whole without `SQLSOURCE_UPDATE_GOLDEN`: only `repository-connection-string-sync` and `repository-connection-string-both` may differ (Task 3's); every other test passes.  Run `tests/SqlSource.Repository.Tests` and `tests/SqlSource.Tool.Tests` (the tool's planning parity).
- [ ] **Step 5: Commit** `feat: give a repository synchronous methods under method-type` (with the two golden files regenerated if the commit cannot validate without them).

### Task 3: The golden corpus

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/Corpus/CorpusRepositoryScenarios.cs`, `QueryMethodCorpus.cs`
- Regenerate: `tests/SqlSource.Tests/Generator/Golden/{postgres,sqlserver}/repository-*.txt` by `SQLSOURCE_UPDATE_GOLDEN=1` on `tests/SqlSource.Tests` alone

- [ ] **Step 1: Add scenarios** `repository-data-source-sync`, `repository-borrowed-sync`, and `repository-pair-both`: an async factory `ValueTask<DbConnection> OpenAsync(CancellationToken)` and a sync factory `DbConnection Open(CancellationToken)`, both marked, under `method-type=both`.  The repository's queries stay `FindUser`, `ListUsers` and `StreamUsers`.
- [ ] **Step 2: Regenerate**, then run `tests/SqlSource.Tests` and `tests/SqlSource.Tests.RoslynFloor` without the variable: pass.
- [ ] **Step 3: Read the new and changed files**: each synchronous method has no `await`, `Task`, `ConfigureAwait` or `cancellationToken` but the factory's `default`; the streams are `SQLSRC298` under `sync` and asynchronous alone under `both`; no golden file outside `repository-*` changed.
- [ ] **Step 4: Commit** `test: give synchronous repository methods to the golden corpus`.

### Task 4: PostgreSQL end-to-end tests

**Files:**
- Modify: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Repository.sql` (copies `GetNoteSync`, `ListNotesSync` with a collection type read whole, `TouchNoteSync`, `GetNoteBoth`, each `-- generator: method-type=sync` or `both`; and a synchronous query that throws `UnexpectedRowCountException`, such as one row or none by account, which has two), its sidecar by `tools/describe-end-to-end.sh`, `RepositoryQueries.cs` (`FactoryRepository` gains a marked synchronous factory `Open(CancellationToken)` beside `OpenAsync`, each recording what it opened, and the synchronous one the token it was given), `VariantCopyTests.cs` (a row per copy), `tests/DatabaseTests/Postgres/README.md` where it lists what the end-to-end project covers
- Create: `tests/DatabaseTests/Postgres/EndToEnd/SynchronousRepositoryTests.cs`

- [ ] **Step 1: Add the copies and the factory**, describe them, add the `VariantCopyTests` rows.
- [ ] **Step 2: Write `SynchronousRepositoryTests`**, synchronous calls only, as `RepositoryQueryTests` is written:
  - each source (data source, connection string, factory, borrowed) reads `GetNoteSync`; `ListNotesSync` reads every note; `TouchNoteSync` counts the row through the interface;
  - the pair: `GetNoteBoth` calls the sync factory, with `default`, and `GetNoteBothAsync` the async one, each once and never the other's; the connection the sync factory gave is disposed after a return and after an exception;
  - borrowed: a closed connection is closed again after a return and after the throwing query's `UnexpectedRowCountException`; an open one is left open;
  - each null source throws the `InvalidOperationException` that names it, before it opens anything;
  - on a `Multiplexing=true` data source, what the driver does with a synchronous repository method (assert it; the manual's PostgreSQL page already notes the limit).
- [ ] **Step 3: Run** `tests/DatabaseTests/Postgres` on the default image and with `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24`, and build `tests/DatabaseTests/Postgres.Npgsql8`: pass.  Any difference from the asynchronous method's behaviour: stop and report it.
- [ ] **Step 4: Commit** `test: run synchronous repository methods end to end on PostgreSQL`.

### Task 5: SQL Server end-to-end tests

**Files:**
- As Task 4, under `tests/DatabaseTests/SqlServer/EndToEnd/` (`Queries/Repository.sql`, `RepositoryQueries.cs`, `VariantCopyTests.cs`, a new `SynchronousRepositoryTests.cs`) and `tests/DatabaseTests/SqlServer/README.md`.  SqlClient has no data source, so the sources are a connection string, the factory pair and a borrowed connection.

- [ ] **Step 1: Add the copies and the factory**, describe them, add the `VariantCopyTests` rows.
- [ ] **Step 2: Write `SynchronousRepositoryTests`** with the cases of Task 4 Step 2 that apply, and the factory's connection disposed once (its `Disposed` event), as the asynchronous test counts it; a borrowed connection with a pending transaction refused with `InvalidOperationException`, as the asynchronous method is.
- [ ] **Step 3: Run** `tests/DatabaseTests/SqlServer` on the default image and with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`, and build `tests/DatabaseTests/SqlServer.SqlClient61`: pass.  Any difference from the asynchronous method: stop and report it.
- [ ] **Step 4: Commit** `test: run synchronous repository methods end to end on SQL Server`.

### Task 6: The manual, the architecture page and the examples

**Files:**
- Modify: `docs/manual/generator.md` ("Synchronous methods": replace the repository paragraph and its "Coming in a later release" callout with what a synchronous repository method is, its interface members, and the sources it can use, the pair and `SQLSRC299`; "The signature" of repository methods: its arguments without `cancellationToken` when synchronous; "The connection": the table says how a synchronous method gets each source's connection, the factory that returns a task serves only asynchronous methods, the pair and its rule, `default` to a factory's token), `docs/manual/settings.md` (the `method-type` row: repository methods follow it), `docs/manual/README.md` ("What works today": the synchronous row covers repository methods and their interface; the "Coming" row goes), `docs/manual/recipes.md` (the "run without awaiting" recipe and the connection-from-a-provider recipe where they say a repository is asynchronous), `docs/manual/postgresql.md` (the `Multiplexing=true` note covers a repository's synchronous method), `README.md` (the sentence that says a query whose output is `methods` gets a synchronous method), `docs/architecture/generator.md` (the planner's per-form source, the resolver's pair, `ResolvedConnection`, the spelling's source token), `src/SqlSource/Generation/Repositories/AGENTS.md` if a rule about the pair is needed
- Modify: `examples/PostgresExamples/Repository/` and `examples/SqlServerExamples/Repository/` (a query under `method-type=sync` or `both`, and code that calls its synchronous method, through the interface), their sidecars by `tools/describe-end-to-end.sh`, each example `README.md` where it lists what the project shows

- [ ] **Step 1: Write the docs and the examples.**  `git grep -n "Coming in a later release" -- README.md src/SqlSource.Tool/README.md docs/manual`: none remains for synchronous methods.
- [ ] **Step 2: Run** `tests/SqlSource.Repository.Tests` (links, anchors, SQL samples, diagnostics, `AGENTS.md` limits) and build the examples: pass.
- [ ] **Step 3: Commit** `docs: document synchronous repository methods`.

### Task 7: Hand-over notes

- [ ] Write `p3-notes.md` in the shared notes folder: `ResolvedConnection`'s shape and `SourceFor`, the per-form source in `MethodPlanner`, `SQLSRC299`, the spelling's source-token member, the copies added to each `Repository.sql` and the factory pair of `FactoryRepository`, for part 4's nightly suite.
