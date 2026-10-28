# Streaming Query Methods Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** `IAsyncEnumerable` becomes a value of the collection type, and the method of a `many` query that takes it returns `IAsyncEnumerable<TDto>`, yielding each row as it is read.

**Architecture:** The generated method stays `static` but is no longer `async`: it runs today's argument checks and returns a call to a `static async` local iterator function that holds today's body unchanged, except that each row is `yield return`ed instead of added to a buffer.  The local function captures nothing; it takes every argument as a parameter of the same name, its token marked `[EnumeratorCancellation]`.

**Tech Stack:** C# source generator (`netstandard2.0`, Roslyn 4.8.0 floor, generated code at C# 12 at most), xUnit v3 with Shouldly, PostgreSQL and SQL Server in Testcontainers.

**Spec:** [`docs/superpowers/specs/2026-10-26-streaming-query-methods-design.md`](../specs/2026-10-26-streaming-query-methods-design.md)

## Global Constraints

- **Every other collection type's method is the same, byte for byte.**  The existing `QueryMethodWriterTests` and every committed expected text must pass unchanged.
- **No new member the caller can see**, no new name that can collide; `SQLSRC274` is unchanged.
- **Argument errors throw at the call**, never on the first `MoveNextAsync`.
- **A loop that stops early drains**: the reader is disposed, the command is never cancelled.
- `CollectionKind.IAsyncEnumerable = 10` and `GeneratorCollectionType.IAsyncEnumerable = 10`, member for member.
- `RowBuffer.IsUsedBy` stays false for the new value; `no-array-pool` has no effect on it.
- Generated code: framework types from `global::`, strings through `SymbolDisplay.FormatLiteral`, `\n` line endings, nothing newer than C# 12, no compiler warning (`src/SqlSource/AGENTS.md`).
- Every local of the generated method, the local function's name included, comes from `LocalNames`.
- The allocation margin in both `EndToEnd/AllocationTests.cs` (`MarginInBytesPerCall = 64`) is never raised.
- Rule 4 of the root `AGENTS.md`: every manual page, the schema, the architecture page, the epic outline and both example projects change in this pull request.
- Every commit passes `./pre-commit-validation.sh`; after the describer-free end-to-end changes, also run each engine's project on its floor image before pushing (root `AGENTS.md`, Commits rule 2).

## Review Focus

1. **A token named like the local function or `yield`.**  A query whose token is `ReadRows` or `yield` must still compile at C# 12 and at the newest C#; the local function takes `ReadRows1`.  Pinned in Task 2.
2. **Enumerating the same result twice.**  Each `await foreach` over one returned `IAsyncEnumerable` runs the query again and yields the same rows, on a closed connection both times.  Pinned in Task 3.
3. **An exception thrown by the caller's loop body** (not by the method) must still close a connection the method opened.  Pinned in Task 3 and Task 4.
4. **Disposal without enumeration.**  `await using` of the enumerator from `GetAsyncEnumerator` with no `MoveNextAsync` must open nothing and leave a closed connection closed.  Pinned in Task 3.
5. **A type whose `many` methods all stream** gets no row buffer struct, and a file mixing a streaming and an array method still gets one.  Pinned in Task 2.

---

## File Structure

- Modify `src/SqlSource/Settings/CollectionKind.cs`: the member.
- Modify `src/SqlSource/Generation/AttributeSource.cs`: `GeneratorCollectionType.IAsyncEnumerable = 10`.
- Modify `schemas/settings-v1.schema.json`: the value in `collectionType`'s `enum` and pattern.
- Modify `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs`: the split method.  The body from `AppendSql` to the closing of `finally` moves, unchanged, into a private method both paths call.
- Tests: `tests/SqlSource.Tests/Settings/SettingValueTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs`, `tests/SqlSource.Tests/Generator/RowBufferTests.cs`, `tests/SqlSource.Testing/Generator/MethodRuns.cs`, `tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/MethodWriterTests.cs` (only if its sibling theory for a query's method lives there).
- End to end: `tests/SqlSource.Tests.Postgres/EndToEnd/` (`Queries/Streaming.sql`, `StreamingQueries.cs`, `StreamingQueryTests.cs`, `AllocationTests.cs`), the same in `tests/SqlSource.Tests.SqlServer/EndToEnd/`, and one case in each floor driver project.
- Docs and examples: listed in Task 6.

---

### Task 1: The value and the streaming method

**Files:**
- Modify: `src/SqlSource/Settings/CollectionKind.cs`
- Modify: `src/SqlSource/Generation/AttributeSource.cs` (the `GeneratorCollectionType` enum)
- Modify: `schemas/settings-v1.schema.json:114-125`
- Modify: `src/SqlSource/Generation/Emitting/QueryMethodWriter.cs`
- Test: `tests/SqlSource.Tests/Settings/SettingValueTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/QueryMethodWriterTests.cs`, `tests/SqlSource.Tests/Generation/Emitting/RowBufferTests.cs`

The value and the writer land together: `AttributeSourceTests` holds the two enums member for member, `SettingsFileSchemaTests` holds the schema to the enum, and `Append_EveryShapeAndCollectionType_Compiles` iterates every `CollectionKind`, so none of the three can pass with the value and without the method.

**Interfaces:**
- Produces: `CollectionKind.IAsyncEnumerable = 10`, `GeneratorCollectionType.IAsyncEnumerable = 10`; `QueryMethodWriter.Append` unchanged in signature, writing the streaming method when `method.Shape == ResultShape.Many && method.Collection == CollectionKind.IAsyncEnumerable`.

- [ ] **Step 1: Write the failing tests**

In `SettingValueTests.TryReadChoice_CollectionType_IsRead`, add `[InlineData("IAsyncEnumerable", 10)]` and `[InlineData("i-async enumerable", 10)]`.

In `RowBufferTests` (Emitting), assert `RowBuffer.IsUsedBy(method with { Shape = ResultShape.Many, Collection = CollectionKind.IAsyncEnumerable })` is false, with and without `UseArrayPool`.

In `QueryMethodWriterTests`, add a `Stream` helper value, `GetUser with { Shape = ResultShape.Many, Collection = CollectionKind.IAsyncEnumerable }`, and these tests:

- `Append_IAsyncEnumerable_IsTheWholeMethod`: the full text of `Write(Stream)`, in the style of `Append_OneOptionalWithParametersAndAToken_IsTheWholeMethod`.  It must hold, in order:
  - the documentation, whose `connection` param is `The connection to run the query on.  When it is closed, it is opened when the loop starts and closed again when the loop ends.` and whose `<returns>` is `The rows the query returns, each read as the loop asks for it.`;
  - `public static global::System.Collections.Generic.IAsyncEnumerable<global::App.GetUserDto> GetUser(` and today's argument list, `this` and defaults included, with no `async` and no `Task`;
  - `AppendChecks`'s lines, then a blank line, then `    return ReadRows(connection, parameters, orderBy, transaction, commandTimeout, cancellationToken);`, a blank line, and
  - `    static async global::System.Collections.Generic.IAsyncEnumerable<global::App.GetUserDto> ReadRows(` with one parameter a line at eight spaces: `global::System.Data.Common.DbConnection connection,`, `global::App.GetUserParams parameters,`, `string orderBy,`, `global::System.Data.Common.DbTransaction? transaction,`, `int? commandTimeout,`, `[global::System.Runtime.CompilerServices.EnumeratorCancellation] global::System.Threading.CancellationToken cancellationToken)`, no `this` and no defaults;
  - the local function's body: today's body from `var sql = string.Create(` to the end of `finally`, each line four spaces deeper, where the reading loop is `while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))` holding `yield return new global::App.GetUserDto(...);`, with no `var rows` and no `return` after the loop.
- `Append_IAsyncEnumerable_ChecksTheArgumentsInTheOuterMethod`: the text before `return ReadRows(` contains `ThrowIfNull(connection)`, `ThrowIfNull(parameters)` and `ThrowIfNullOrWhiteSpace(orderBy)`; the text after it contains none of them.
- `Append_IAsyncEnumerableWithoutParameters_PassesNoParameters`: with `Input = null`, the call is `ReadRows(connection, orderBy, transaction, commandTimeout, cancellationToken)` and no line contains `parameters`.
- `Append_IAsyncEnumerableTokenNamedReadRows_RenamesTheLocalFunction`: a token `ReadRows` gives `return ReadRows1(` and `> ReadRows1(\n`, and the argument `string ReadRows,` in both lists.
- `Append_IAsyncEnumerableTokenNamedAwait_WritesItVerbatim`: a token `await` is `@await` in the call, in both argument lists and in the body, as `Append_TokenNamedAwait_WritesItVerbatimWhereItIsAnIdentifier` asserts for today's method.
- `Append_IAsyncEnumerableNotNullVectorColumn_ChecksBeforeYielding`: with `VectorOutput(false)`, the `IsNull` check and its `throw` come before `yield return`, at the loop's depth.
- `Append_IAsyncEnumerableTypePlacement_TakesTheConnectionWithoutThis`: a theory over `public static`, `internal static` and `private static` with `isExtension: false`: the outer signature starts with the modifiers then ` global::System.Collections.Generic.IAsyncEnumerable<`, and `this ` appears nowhere.
- `Append_IAsyncEnumerableUnderNoArrayPool_IsTheSameMethod`: `Write(Stream with { UseArrayPool = false })` equals `Write(Stream)`.

Add `Token("ReadRows")` and `Token("yield")` to the awkward segments of `Append_EveryShapeAndCollectionType_Compiles`; it now also compiles every streaming method.

- [ ] **Step 2: Run the tests and see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~QueryMethodWriterTests|FullyQualifiedName~SettingValueTests|FullyQualifiedName~Emitting.RowBufferTests"`
Expected: compile error, `CollectionKind` has no `IAsyncEnumerable`.

- [ ] **Step 3: Add the value**

`IAsyncEnumerable = 10` in `CollectionKind`, documented `<c>IAsyncEnumerable&lt;T&gt;</c>, each row read as the loop asks for it.`  The same member in `GeneratorCollectionType` in `AttributeSource.Text`, documented as its siblings are there.  In `schemas/settings-v1.schema.json`, add `"IAsyncEnumerable"` to the `enum` and an alternative to the pattern in the form of its siblings: each letter as `[xX]` followed by `[- \\t]*`.

- [ ] **Step 4: Write the streaming method**

In `QueryMethodWriter`:
- Move the lines `Append` writes from `AppendSql` to the closing `}` of `finally` into `private static void AppendBody(StringBuilder builder, string indent, QueryMethod method, List<string> tokens, List<string> arguments, Locals locals, string rowBufferType)`, so `Append` writes today's method by calling it at `bodyIndent`.
- `Locals` gains `public string Iterator { get; }`, taken as `names.Take("ReadRows")` after `State`, and only when its constructor's new `bool streams` argument is true (`string.Empty` otherwise), so no other method's locals change.
- `private static bool Streams(QueryMethod method) => method.Shape == ResultShape.Many && method.Collection == CollectionKind.IAsyncEnumerable;`
- Split `AppendSignature`'s argument list into `private static void AppendArguments(StringBuilder builder, string indent, QueryMethod method, List<string> arguments, bool isExtension, bool isIterator)`: `isIterator` drops `this` and the three defaults and writes the attribute before the token.  `AppendSignature` writes its first line as today unless the method streams, when it writes `modifiers + " global::System.Collections.Generic.IAsyncEnumerable<" + row + "> "` with no `async`.
- When the method streams, `Append` writes: documentation, signature, `{`, `AppendChecks`, a blank line, the `return` of the call (its arguments in signature order: `connection`, `parameters` when there is an input, each argument, `transaction`, `commandTimeout`, `cancellationToken`), a blank line, the local function's first line and arguments at `bodyIndent`, `{`, `AppendBody` at `blockIndent`, and the two closing braces.
- In `AppendExecution`, the `Many` branch for a streaming method writes no `var rows` and no `return`, and writes `yield return ` and the row in place of `rows.Add(` and `);`.
- `AppendDocumentation` takes the two texts above for a streaming method; every other text is unchanged.
- `GetCollectionType` is not reached for a streaming method; give its switch an `IAsyncEnumerable` arm returning the same `IAsyncEnumerable<row>` text so that the switch lists every member, as `GetBuffer`'s does.

- [ ] **Step 5: Run the tests and see them pass**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~QueryMethodWriterTests|FullyQualifiedName~SettingValueTests|FullyQualifiedName~RowBufferTests|FullyQualifiedName~AttributeSourceTests|FullyQualifiedName~SettingsFileSchemaTests|FullyQualifiedName~SettingsLevelReaderTests|FullyQualifiedName~SqlMarkerScopeTests"`
Expected: PASS, every existing test of the writer unchanged.

- [ ] **Step 6: Validate and commit**

Run `./pre-commit-validation.sh` and see it pass.

```bash
git add src/SqlSource schemas tests/SqlSource.Tests
git commit -m "Write the method of a many query of IAsyncEnumerable as a local iterator"
```

---

### Task 2: The generator's runs

**Files:**
- Modify: `tests/SqlSource.Testing/Generator/MethodRuns.cs` (`RunTokenNamed`)
- Modify: `tests/SqlSource.Tests/Generator/GeneratedSourceMethodTests.cs`, and the file holding `Run_TokenWithAnAwkwardNameInAQueryMethod_CompilesOnTheNewestCSharp`
- Modify: `tests/SqlSource.Tests/Generator/RowBufferTests.cs`

**Interfaces:**
- Consumes: Task 1's value and method.

- [ ] **Step 1: Write the failing tests**

- In `RunTokenNamed`, add a third query, `-- name: ProbeStream\n-- collection-type: IAsyncEnumerable\n` + `body`, with its sidecar entry as `Probe`'s.  In `Run_TokenWithAnAwkwardNameInAQueryMethod_Compiles` and its newest-C# sibling, assert the methods file contains ` ProbeStream(\n` and `yield return `, and add the rows `ReadRows` and `yield`.
- In `Generator/RowBufferTests`, add `Run_EveryManyMethodStreams_WritesNoRowBuffer`: a type whose one `many` query has `-- collection-type: IAsyncEnumerable` runs clean and its methods file does not contain `SqlSourceRowBuffer`; and `Run_StreamingBesideAnArrayMethod_WritesTheRowBufferOnce`: one streaming and one default `many` query, the file contains `file struct SqlSourceRowBuffer<T>` exactly once.  Each asserts `GeneratedCodeWarnings` is empty.

- [ ] **Step 2: Run them**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~GeneratedSourceMethodTests|FullyQualifiedName~Generator.RowBufferTests|FullyQualifiedName~MethodWriterTests"`
Expected: PASS if Task 1 is right; a failure here is a defect of Task 1's method, fixed there.  The `RoslynFloor` project runs the same tests on Roslyn 4.8.0: run `dotnet test tests/SqlSource.Tests.RoslynFloor` too.

- [ ] **Step 3: Validate and commit**

```bash
git add tests/SqlSource.Testing tests/SqlSource.Tests
git commit -m "Compile a streaming method beside every awkward token, and give a file of streaming methods no row buffer"
```

---

### Task 3: End to end on PostgreSQL

**Files:**
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/Queries/Streaming.sql` and its sidecar `Streaming.sql.json` (written by `tools/describe-end-to-end.sh`)
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/StreamingQueries.cs` (`[SqlSourceGenerate(Path = "Queries/Streaming.sql")] internal static partial class StreamingQueries;` in the form of `CollectionQueries.cs`)
- Create: `tests/SqlSource.Tests.Postgres/EndToEnd/StreamingQueryTests.cs`
- Modify: `tests/SqlSource.Tests.Postgres/EndToEnd/AllocationTests.cs`

**Interfaces:**
- Consumes: Task 1's method.
- Produces: the queries below, which Task 4 mirrors.

- [ ] **Step 1: Write the queries**

The file header is `-- collection-type: IAsyncEnumerable`.  Queries:
- `StreamAccounts`: `SELECT id, name FROM accounts ORDER BY id;` into `-- output-model: CollectedAccount`.
- `StreamAccountsNamedFrom` with a parameter: `SELECT id, name FROM accounts WHERE id >= @firstId ORDER BY id;` into `CollectedAccount`.
- `StreamNullAtThird`: `SELECT id, CASE WHEN id = 3 THEN NULL ELSE name END AS name FROM accounts ORDER BY id;` with `-- column: name not null`.
- `StreamNumbers`: `SELECT n FROM generate_series(1, @count) AS n ORDER BY n;` (for `break` and cancellation over many rows).
- A converted column, in the form the existing end-to-end queries convert one (`Notes.sql`'s `-- column: ... -> AccountId`), as `StreamNoteAccounts`.

Run `tools/describe-end-to-end.sh` to write the sidecar.

- [ ] **Step 2: Write the tests, in `StreamingQueryTests`**, `[Collection(UsesPostgres.Name)]`, each with `TestContext.Current.CancellationToken`:

- `StreamAccounts_EveryAccount_YieldsTheArrayMethodsRows`: `ToListAsync` of the stream equals `CollectionQueries.ListAsArray`'s rows.
- `StreamAccounts_NullConnection_ThrowsAtTheCall`: `Should.Throw<ArgumentNullException>(() => StreamingQueries.StreamAccounts(null!))`, synchronously, with no enumeration.
- `StreamAccountsNamedFrom_NullParameters_ThrowsAtTheCall`: the same for `parameters`.
- A token query's blank token throws `ArgumentException` at the call (add `StreamFromTable` with a token `{{table:accounts}}` to the file).
- `StreamAccounts_ClosedConnection_IsOpenDuringTheLoopAndClosedAfter`: `connection.State` is `Open` inside the loop and `Closed` after it.
- `StreamAccounts_ClosedConnectionAndABreak_IsClosedAfterTheLoop`.
- `StreamAccounts_ClosedConnectionAndAThrowInTheLoopBody_IsClosedAfter` (review focus 3).
- `StreamAccounts_OpenConnection_StaysOpen`.
- `StreamAccounts_EnumeratedTwice_RunsTheQueryEachTime` (review focus 2): two `await foreach` over one returned value yield the same three rows each.
- `StreamAccounts_DisposedBeforeTheFirstRow_OpensNothing` (review focus 4): `await using var e = stream.GetAsyncEnumerator(); ` with no `MoveNextAsync`; the closed connection stays `Closed`.
- `StreamNumbers_BreakPartway_LeavesTheConnectionUsable`: on an open connection, break after 10 of 10,000 rows, then `ListAsArray` on the same connection returns three accounts.
- `StreamNumbers_BreakPartwayInATransaction_TheTransactionCommits`: insert a row in a transaction, stream and break, commit, and the row is there; clean up after.
- `StreamNumbers_MethodToken_Cancels` and `StreamNumbers_WithCancellation_Cancels`: a token cancelled after the first row; the loop throws `OperationCanceledException` (as `ShouldThrowAsync<OperationCanceledException>` matches derived types).
- `StreamNullAtThird_NullInANotNullColumn_ThrowsAtThatRowAfterTheOthers`: the first two rows are yielded, then `InvalidCastException`.
- `StreamNoteAccounts_ConvertedColumn_ReadsAsTheArrayMethodDoes`.

- [ ] **Step 3: Add the allocation pair to `AllocationTests`**

`StreamAccounts_PerCall_AllocatesNoMoreThanHandWritten`: `MeasureAsync` over two `Func<Task<int>>`s, each `await foreach`ing to the end and returning the count: the generated `StreamAccounts`, against `StreamAccountsByHand`, a `static` local async iterator inside a static method that makes the same calls (open if closed, command, transaction, timeout, `ExecuteReaderAsync(CommandBehavior.Default, token)`, `GetFieldValue` by ordinal, `yield return`, close in `finally`), its token `[EnumeratorCancellation]`.  Assert as `ListAccounts_PerCallThroughThePool_AllocatesNoMoreThanHandWritten` does, against `MarginInBytesPerCall`.  Extend the class's header comment with one sentence on the pair.

- [ ] **Step 4: Run them**

Run: `dotnet test tests/SqlSource.Tests.Postgres --filter "FullyQualifiedName~StreamingQueryTests|FullyQualifiedName~AllocationTests"`
Expected: PASS.  Then on the floor image: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/SqlSource.Tests.Postgres --filter "FullyQualifiedName~StreamingQueryTests"`.

- [ ] **Step 5: Validate and commit**

```bash
git add tests/SqlSource.Tests.Postgres
git commit -m "Prove the streaming method end to end on PostgreSQL, and that it allocates as a hand-written iterator"
```

---

### Task 4: End to end on SQL Server

**Files:**
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/Queries/Streaming.sql` and its sidecar
- Create: `tests/SqlSource.Tests.SqlServer/EndToEnd/StreamingQueries.cs`, `StreamingQueryTests.cs`
- Modify: `tests/SqlSource.Tests.SqlServer/EndToEnd/AllocationTests.cs`

**Interfaces:**
- Consumes: Task 3's list of queries and tests.

- [ ] **Step 1: Mirror Task 3**

The same queries in T-SQL against `dbo.accounts` (`StreamNumbers` over `GENERATE_SERIES` where the image has it, or a recursive CTE with `OPTION (MAXRECURSION 0)` so it runs on 2017), the same tests and the same allocation pair, with SqlClient's types.  Two more, each in its own query:
- `StreamThenSelect`: `SELECT id, name FROM dbo.accounts ORDER BY id; SELECT 1;` yields the three accounts.
- `StreamThenRaise`: `SELECT id, name FROM dbo.accounts ORDER BY id; THROW 50000, 'after the rows', 1;` yields the three accounts, then the `SqlException` with message `after the rows` is thrown at the end of the loop, from disposal.  Record which call throws, `MoveNextAsync` or `DisposeAsync`; the manual (Task 6) states what this test observes.

- [ ] **Step 2: Run them**

Run: `dotnet test tests/SqlSource.Tests.SqlServer --filter "FullyQualifiedName~StreamingQueryTests|FullyQualifiedName~AllocationTests"`, then with `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04`.
Expected: PASS.

- [ ] **Step 3: Validate and commit**

```bash
git add tests/SqlSource.Tests.SqlServer
git commit -m "Prove the streaming method end to end on SQL Server, a query of several statements among them"
```

---

### Task 5: The floor drivers

*(Planner's call.)*  Npgsql 8 and SqlClient 6.1 drain a disposed reader by their own code, so behaviour after a `break` can differ from the newest drivers.  One case each.

**Files:**
- Modify: `tests/SqlSource.Tests.Postgres.Npgsql8/` and `tests/SqlSource.Tests.SqlServer.SqlClient61/`: one query file of the project's existing form with a streaming query over many rows, its sidecar, and one test class.

- [ ] **Step 1: Write `StreamNumbers_BreakPartway_LeavesTheConnectionUsable` in each project**, as in Task 3, plus `..._YieldsEveryRow` over three rows.

- [ ] **Step 2: Run** `dotnet test tests/SqlSource.Tests.Postgres.Npgsql8` and `dotnet test tests/SqlSource.Tests.SqlServer.SqlClient61`.  Expected: PASS.

- [ ] **Step 3: Validate and commit**

```bash
git add tests/SqlSource.Tests.Postgres.Npgsql8 tests/SqlSource.Tests.SqlServer.SqlClient61
git commit -m "Break out of a streaming method on the floor drivers"
```

---

### Task 6: Documentation and examples

**Files:**
- Modify: `docs/manual/settings.md` (Collection type, All settings row, `no-array-pool` row, settings file section if it lists values)
- Modify: `docs/manual/generator.md` (query methods' return table and `many` bullet, its array-pool sentence, the connection section)
- Modify: `docs/manual/diagnostics.md:490` and `:694`
- Modify: `docs/manual/README.md` (What works today: a row for streaming rows, Available)
- Modify: `README.md` only where it lists the collection types or method shapes
- Modify: `docs/architecture/generator.md` (step 5 of a method, around line 574, and the row buffer section at line 597: the split method, the local function, and that `IsUsedBy` leaves it out)
- Modify: `docs/superpowers/specs/2026-10-07-query-generation-epic-design.md:971` and `:1064`: each says `IAsyncEnumerable<T>` was delivered by `2026-10-26-streaming-query-methods-design.md`
- Modify: `examples/PostgresExamples/CodeGen/Users.sql`, its sidecar, and a caller; the same in `examples/SqlServerExamples/`

- [ ] **Step 1: The manual**

`settings.md` Collection type: a row `| IAsyncEnumerable | IAsyncEnumerable<T> | Nothing: each row is read as the loop asks for it |`; the sentence on filling made true of the other types only; then, in short, the behaviour of the spec's "Behaviour the user sees", the several-statements line as Task 4 observed it.  The `no-array-pool` row adds that a streaming method holds no collection.  `generator.md`: the `many` row returns `Task<C>`, or `IAsyncEnumerable<T>` for `IAsyncEnumerable`; the connection section says a connection the method opened stays open until the loop ends and is busy meanwhile.  `diagnostics.md`: `IAsyncEnumerable` in both lists.  Keep to rule 4: effects, never internals; `sql` samples name their dialect.

*(Planner's call.)*  No recipe: the Collection type section carries the example and the behaviour, and a recipe would repeat it.

- [ ] **Step 2: The examples**

Add to each example's `CodeGen/Users.sql`, after `ListUsersNewestFirst`, `-- name: StreamUsersOldestFirst` with the summary `Every user, the oldest first, each read as the caller's loop asks for it.`, a comment line in the style of its neighbours saying the marker streams the rows, `-- collection-type: IAsyncEnumerable`, and `SELECT id, name, email FROM users ORDER BY created_at, id;` (the same in T-SQL against the SQL Server example's table).  Add a caller to each `CodeGen/StoreQueries.cs` that `await foreach`es it, in the form of its existing callers.  Run `tools/describe-end-to-end.sh` for the sidecars.  Update each example's `README.md` if it lists its queries.

- [ ] **Step 3: Run the documentation tests**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Documentation|FullyQualifiedName~Diagnostics"` and `dotnet build examples/PostgresExamples examples/SqlServerExamples`.
Expected: PASS, no warning.

- [ ] **Step 4: Validate and commit**

Run `./pre-commit-validation.sh` and see it pass.

```bash
git add docs README.md examples
git commit -m "Document streaming query methods, and stream rows in both examples"
```

---

### Before the pull request

- Check the version (root `AGENTS.md`, Pull requests rule 1): `git fetch --tags origin`, compare `VersionPrefix` in `Directory.Build.props` with `git tag --list 'v*' --sort=-v:refname | head -n 1`, and ask the owner for the new version if it is not greater.
- Bring the branch up to date with `main`, run `./pre-commit-validation.sh` on the tree as it will be pushed, and turn on Auto-fix after opening the pull request (`CLAUDE.local.md`).
