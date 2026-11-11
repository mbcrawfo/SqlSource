# Collection Methods Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** A query with `collection-method` on also gets an overload of its query method that takes `IEnumerable<TParams>`, runs the query once for each item as batches of commands, in one transaction by default, and returns every row as one flat collection.

**Architecture:** The planner gives such a query a second `QueryMethod`, its `Batch` set, beside its query method.  `QueryMethodWriter` sends that method to a new phase, `BatchWriter`, which reuses the SQL builder and the engine's `ParameterBinder` against a `DbBatchCommand` for each item, and reads one result set for each item.  Database errors are wrapped in a new generated exception, `CollectionItemException : DbException`.

**Tech Stack:** C# source generator (netstandard2.0), ADO.NET `DbBatch` (.NET 8+), Npgsql 8+, Microsoft.Data.SqlClient 6.1+, xUnit, Shouldly, Testcontainers.

**Spec:** `docs/superpowers/specs/2026-11-10-collection-methods-design.md`, "Part 2: collection methods".  Part 1 (generator parameters with values) is merged.

## Global Constraints

- The generated code uses ADO.NET alone: no Dapper, no reflection, no library of SqlSource's.
- Projects target .NET 8+, Npgsql 8+ and Microsoft.Data.SqlClient 6.1+.  `DbBatchCommand.CreateParameter()` is overridden by both drivers' batch commands at those floors (checked), so the engine binders run against a batch command unchanged.
- The collection method is an overload of the query method: same name (`Async` under `async-method-suffix`), same arguments in the same order, `IEnumerable<TParams> parameters` in place of `TParams parameters`.
- Chunks hold `1000` items, a constant.
- `transaction` values, in this order (their word indexes): `on`, `off`, `read-uncommitted`, `read-committed`, `repeatable-read`, `serializable`, `snapshot`; default `on`.  PostgreSQL has no `read-uncommitted` and no `snapshot`.
- A transaction the caller gave (`methods`), or an ambient `System.Transactions.Transaction.Current`, is the caller's: the method neither begins, commits nor rolls back one.
- Every rule of the root `AGENTS.md` holds: docs, examples and `AnalyzerReleases.Unshipped.md` in the same PR; `./pre-commit-validation.sh` before each commit and `--full` before each push; `branch-review:review` clean before the PR.

## Rulings in this plan the owner should see

- **PostgreSQL collection methods send the SQL without comments, even under `keep-comments`.**  Npgsql refuses a batch command with a line comment after its final `;`, and the comment-free segments (`SqlBlock.Segments`) never have one.  This replaces the spec's "cut what follows the last `;`", which needs a dialect-aware scan the emitter does not have.  The manual says so.
- **`UnexpectedRowCountException` gains the item's position.**  The owner ruled that its constructor may change, since SqlSource has had no release, so no referenced copy of another version exists to break.  It gets a second constructor and an `ItemIndex` property (Task 2).

## Review Focus

- **A lazy `IEnumerable` that throws or is consumed twice.**  The method enumerates once; a generator that yields a null at position 1,500 throws `ArgumentNullException` naming position 1500 after the first chunk ran, and under the default transaction nothing stays.  Test in Task 6 (`NullItemInTheSecondChunk_RollsBackTheFirst`).
- **Exactly 1,000 and 1,001 items.**  The chunk boundary: 1,000 is one batch, 1,001 two, and the rows keep input order.  Test in Task 6.
- **An item whose query returns zero rows under `one`.**  `UnexpectedRowCountException`, and the transaction rolled back.  Test in Task 6.
- **A caller's transaction that SQL Server rolled back mid-batch (`XACT_ABORT ON`).**  The caller sees the wrapped database error, not `TransactionRolledBackException`, and the method does not try to roll back.  Test in Task 7.
- **An empty collection with a closed connection.**  Returns at once and leaves the connection closed and no transaction begun.  Test in Task 6.

---

### Task 1: The `collection-method` and `transaction` parameters

**Files:**
- Modify: `src/SqlSource/Settings/GeneratorParameters.cs` (add `CollectionMethod = 256`, `Transaction = 512`)
- Create: `src/SqlSource/Settings/TransactionMode.cs`
- Modify: `src/SqlSource/Settings/GeneratorParameterList.cs` (`All`, `Definitions`)
- Modify: `src/SqlSource/Settings/QuerySettings.cs` (two accessors)
- Modify: `schemas/settings-v1.schema.json` (enum and pattern of `generatorParameters`)
- Modify: `tests/SqlSource.Repository.Tests/Diagnostics/DiagnosticsDocumentTests.cs` and `docs/manual/diagnostics.md` (SQLSRC109's list of parameters)
- Test: `tests/SqlSource.Tests/Settings/GeneratorParameterListTests.cs`, `tests/SqlSource.Tests/Settings/QuerySettingsTests.cs`

**Interfaces:**
- Produces: `internal enum TransactionMode { On, Off, ReadUncommitted, ReadCommitted, RepeatableRead, Serializable, Snapshot }` (members in word order, so `(TransactionMode)values.Get(GeneratorParameters.Transaction)` reads it); `QuerySettings.CollectionMethod : bool`; `QuerySettings.Transaction : TransactionMode`.

- [ ] **Step 1: Write the failing tests**

```csharp
// GeneratorParameterListTests
[Theory]
[InlineData("transaction=serializable", 5)]
[InlineData("Transaction=READ-COMMITTED", 3)]
[InlineData("transaction", 0)]
public void ReadElements_TransactionValue_IsItsWord(string element, int value) =>
    GeneratorParameterList.ReadElements([element], [], []).ValueOf(GeneratorParameters.Transaction).ShouldBe(value);

[Fact]
public void ReadElements_CollectionMethod_IsASwitchOffByDefault() =>
    GeneratorParameterChoices.None.Resolve().IsOn(GeneratorParameters.CollectionMethod).ShouldBeFalse();

// QuerySettingsTests
[Fact]
public void Resolve_NoLevel_HasTransactionOnAndNoCollectionMethod()
{
    var settings = QuerySettings.Resolve(SettingsLevel.None, SettingsLevel.None);
    settings.Transaction.ShouldBe(TransactionMode.On);
    settings.CollectionMethod.ShouldBeFalse();
}
```

- [ ] **Step 2: Run them to see them fail**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~GeneratorParameterListTests|FullyQualifiedName~QuerySettingsTests"`
Expected: build fails, `CollectionMethod`, `Transaction` and `TransactionMode` do not exist.

- [ ] **Step 3: Add the flags, the enum, the definitions and the accessors**

`collection-method` is `GeneratorParameterDefinition.Switch(..., onByDefault: false)`; `transaction` is `new GeneratorParameterDefinition("transaction", GeneratorParameters.Transaction, ["on", "off", "read-uncommitted", "read-committed", "repeatable-read", "serializable", "snapshot"], 0)`.  Add both to `All`.  Neither goes in `Defaults` (`transaction` is not a switch).

- [ ] **Step 4: Update the schema and SQLSRC109's list**

Add both names to the schema's `enum`, and each to the pattern with its own words, as `SettingsFileSchemaTests.Schema_PatternOfGeneratorParameters_IsGeneratedFromTheParameters` builds it.  Add both to SQLSRC109's section and to the list in `DiagnosticsDocumentTests`.

- [ ] **Step 5: Run the tests to see them pass**

Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~GeneratorParameter|FullyQualifiedName~QuerySettings|FullyQualifiedName~SettingsFileSchema"` then `dotnet test tests/SqlSource.Repository.Tests`.
Expected: PASS.

- [ ] **Step 6: Commit** (`./pre-commit-validation.sh` first): `feat: add the collection-method and transaction generator parameters`

### Task 2: `CollectionItemException`, and the item's position in `UnexpectedRowCountException`

**Files:**
- Modify: `src/SqlSource/Generation/Exceptions/ExceptionSource.cs` (the template, a third class, its metadata name)
- Modify: `src/SqlSource/Generation/Exceptions/ExceptionProbe.cs` (find a referenced copy of the third type as of the other two)
- Test: the tests of `ExceptionSource` and `ExceptionProbe` (`grep -rln ExceptionSource tests/SqlSource.Tests`)

**Interfaces:**
- Produces: `ExceptionSource.CollectionItemMetadataName = "SqlSource.CollectionItemException"`; the generated type, in namespace `SqlSource`, accessibility as the other two:

```csharp
$accessibility$ sealed class CollectionItemException : global::System.Data.Common.DbException
{
    public CollectionItemException(string queryName, int itemIndex, global::System.Data.Common.DbException inner)
    // Message: "The query '<queryName>' failed at item <itemIndex>: <inner.Message>"
    public string QueryName { get; }
    public int ItemIndex { get; }
    public override string? SqlState { get; }               // inner.SqlState
    public override bool IsTransient { get; }                // inner.IsTransient
    protected override global::System.Data.Common.DbBatchCommand? DbBatchCommand { get; }  // inner.BatchCommand
    // base(message, inner) with ErrorCode = inner.ErrorCode: use the DbException(string, int) constructor is not
    // possible together with an inner exception, so set HResult = inner.HResult in the constructor.
}
```

- `UnexpectedRowCountException` keeps `UnexpectedRowCountException(string queryName, bool moreThanOne)` and gains `UnexpectedRowCountException(string queryName, bool moreThanOne, int itemIndex)`, whose message is `"The query '<queryName>' returned no rows for item <itemIndex>."` or `"… returned more than one row for item <itemIndex>."`, and `public int? ItemIndex { get; }`, null from the first constructor.  `docs/manual/generator.md`'s section of the exception says so (Task 8).

- [ ] **Step 1: Write the failing tests**: `new UnexpectedRowCountException("AddUser", false, 3)` has the message `"The query 'AddUser' returned no rows for item 3."` and `ItemIndex == 3`, and the two-argument constructor's message and a null `ItemIndex` are unchanged; the generated source declares `CollectionItemException : global::System.Data.Common.DbException` with the constructor and the four members above; a project that references an assembly holding `SqlSource.CollectionItemException` (as the existing probe test builds one for `UnexpectedRowCountException`) generates none of its own; under `SqlSourcePublicExceptions` it is `public`.  A runtime test (compile the generated source into the test's compilation, as the existing exception tests do, or construct it through reflection) asserts `new CollectionItemException("AddUser", 3, inner).Message == "The query 'AddUser' failed at item 3: " + inner.Message`, `ItemIndex == 3`, `InnerException == inner`, `SqlState == inner.SqlState`.
- [ ] **Step 2: Run them to see them fail.**  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~Exception"`.  Expected: FAIL.
- [ ] **Step 3: Add the class to the template and the probe.**
- [ ] **Step 4: Run the tests to see them pass**, then the whole `tests/SqlSource.Tests` (the golden files of every generated exceptions file change: update them with the project's golden-file procedure in `tests/README.md`).
- [ ] **Step 5: Commit**: `feat: generate CollectionItemException and give a row count's item`

### Task 3: The planner gives a query its collection method

**Files:**
- Modify: `src/SqlSource/Generation/Methods/QueryMethod.cs` (add `BatchCall`, `QueryMethod.Batch`)
- Modify: `src/SqlSource/Generation/Methods/MethodPlanner.cs` (after a query's method is built)
- Modify: `src/SqlSource/Diagnostics/SqlDiagnostics.cs`, `src/SqlSource/AnalyzerReleases.Unshipped.md`, `docs/manual/diagnostics.md` (SQLSRC297, and the sentence of the ranges that names the method ids)
- Create: `tests/SqlSource.Tests/Generator/CollectionMethodTests.cs`

**Interfaces:**
- Consumes: `QuerySettings.CollectionMethod`, `QuerySettings.Transaction` (Task 1).
- Produces:
  - `internal sealed record BatchCall(TransactionMode Transaction);` and `public BatchCall? Batch { get; init; }` on `QueryMethod`.
  - For each query whose method is built, whose settings have `CollectionMethod`, and whose method has an `Input`: a second `QueryMethod`, `method with { Batch = new(settings.Transaction), Collection = method.Collection == CollectionKind.IAsyncEnumerable ? CollectionKind.Array : method.Collection }`, placed right after it in `TypeMethods.Methods`, so it flows to `MethodSignatures`, the interface and the emitter like any method.  Its `ParameterKey` is the query method's with the input model's type text replaced by `global::System.Collections.Generic.IEnumerable<T>`.
  - The name check (`FindTakenReason`, SQLSRC274) is not run again for the collection method: it shares its query method's name by design.
  - `SqlDiagnostics.UnsupportedTransactionIsolation`: id `SQLSRC297`, error, at the query's name, message `"The query '{0}' asks for the transaction isolation level '{1}', which {2} does not have"` ({2}: `PostgreSQL`).  It applies when the engine is PostgreSQL and `Transaction` is `ReadUncommitted` or `Snapshot`; the query keeps its query method and gets no collection method.

- [ ] **Step 1: Write the failing generator tests** in `CollectionMethodTests`, through the generator harness as `RepositoryMethodTests` runs it, with a PostgreSQL query `-- name: AddUser -> one` with one parameter.  They read the planned methods from the tracked step `TrackingNames.TypeMethods` (`StepOutputs.Single<TypeMethods>(run, TrackingNames.TypeMethods)`, as `GeneratorHarnessTests` reads a step), not the emitted text, which Tasks 4 and 5 write:
  - `Run_CollectionMethodOn_PlansASecondMethodWithBatch`: two methods named `AddUser`, the second with `Batch == new BatchCall(TransactionMode.On)`, right after the first; no diagnostic.
  - `Run_CollectionMethodOff_PlansNoSecondMethod`.
  - `Run_CollectionMethodOnAQueryWithoutParameters_PlansNoSecondMethodAndReportsNothing`.
  - `Run_StreamingCollectionType_GivesTheSecondMethodAnArray`: under `-- collection-type: IAsyncEnumerable` and `-> many`, the second method's `Collection` is `CollectionKind.Array`.
  - `Run_TransactionSerializable_IsTheSecondMethodsMode`.
  - `Run_TransactionSnapshotOnPostgres_IsSqlsrc297AndKeepsTheQueryMethod` and the same for `read-uncommitted`; `Run_TransactionSnapshotOnSqlServer_IsNoError`.
  Until Task 4, `QueryMethodWriter` writes nothing for a method with `Batch` set, so the run compiles; Task 4 removes that skip.
- [ ] **Step 2: Run them to see them fail.**  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~CollectionMethodTests"`.  Expected: FAIL (one method planned).
- [ ] **Step 3: Add `BatchCall`, the planner's second method, and SQLSRC297** with its descriptor, its `AnalyzerReleases.Unshipped.md` row and its section in `docs/manual/diagnostics.md`, which a test there holds to the descriptors.
- [ ] **Step 4: Run the tests to see them pass**, then `dotnet test tests/SqlSource.Repository.Tests`.
- [ ] **Step 5: Commit**: `feat: plan a collection method beside a query method`

### Task 4: The collection method's signature, documentation and checks

**Files:**
- Modify: `src/SqlSource/Generation/Methods/MethodArguments.cs` (the input argument's type for a method with `Batch`)
- Modify: `src/SqlSource/Generation/Methods/MethodSignature.cs` (the `ParameterKey`, as Task 3 says)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/HeaderWriter.cs` (documentation: the summary says it runs the query once for each item and returns the rows of every item in order; `<exception>` for `CollectionItemException`; the checks: `ThrowIfNull(parameters)` stays, the per-item list counts move to Task 5)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodLocals.cs` (add `Input`, the expression the bindings and lists read: `parameters` for a query method, the item local for a collection method)
- Modify: `CommandWriter.cs:241`, `ListWriter.cs:46,56`, `ListCountWriter.cs:36` (read `context.Locals.Input`, not `MethodArguments.Parameters`; `ListCountWriter.cs:201`, the argument's name in a message, stays `parameters`)
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/QueryMethodWriter.cs` (a method with `Batch` is now written: its header, and until Task 5 a body of `throw new global::System.NotImplementedException();`, which no release holds since Task 5 lands in the same PR)
- Test: `tests/SqlSource.Tests/Generation/Emitting/QueryMethods/BatchMethodWriterTests.cs` (create), `tests/SqlSource.Tests/Generation/Methods/MethodArgumentsTests.cs`, `MethodSignatureTests.cs`

**Interfaces:**
- Consumes: `QueryMethod.Batch` (Task 3).
- Produces: `QueryMethodLocals.Input : string`; for a batch method, `MethodArguments` gives the input argument the type `global::System.Collections.Generic.IEnumerable<{Input.TypeText}>` and the name `parameters`.

- [ ] **Step 1: Write the failing tests**: `MethodArgumentsTests.Of_BatchMethod_TakesTheItemsAsParameters` (the input argument's type text above); `MethodSignatureTests.Of_BatchMethod_DiffersFromItsQueryMethod` (the two `ParameterKey`s differ); `BatchMethodWriterTests.Append_BatchMethod_HasTheOverloadsSignature` for `methods` (connection, `IEnumerable<…> parameters`, tokens, `transaction`, `commandTimeout`, `cancellationToken`) and for a repository (`IEnumerable<…> parameters`, tokens, `cancellationToken`), and `…_DocumentsCollectionItemException`.  `QueryMethodWriterTests` keeps passing: a query method's text does not change.
- [ ] **Step 2: Run them to see them fail.**  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~BatchMethodWriterTests|FullyQualifiedName~MethodArgumentsTests|FullyQualifiedName~MethodSignatureTests"`.  Expected: FAIL.
- [ ] **Step 3: Implement**, then run every test of `tests/SqlSource.Tests`; no golden file of a query method changes.
- [ ] **Step 4: Commit**: `feat: give the collection method its signature`

### Task 5: The body of a collection method

**Files:**
- Create: `src/SqlSource/Generation/Emitting/QueryMethods/BatchWriter.cs` (the body), `BatchRowsWriter.cs` (executing a batch and reading its result sets)
- Modify: `CommandWriter.cs` (expose the SQL and the bindings of one command for reuse: `AppendSql(in context, indent)` and `AppendBindings(in context, command, indent)` taking the command's local name), `RowsWriter.cs` (expose reading one row into the result, as `RowReaderWriter` already does), `ExecutionCapabilityRules.cs` (a batch method's flags, Task 7 for SQL Server)
- Modify: `src/SqlSource/Generation/Methods/MethodPlanner.cs` (a PostgreSQL batch method's `Segments` are the block's `Segments`, without comments: see the ruling above)
- Test: `BatchMethodWriterTests.cs`; a new end-to-end class in Task 6

**Interfaces:**
- Consumes: Tasks 2–4.
- Produces: `BatchWriter.AppendBody(in QueryMethodContext context, string indent)` and the constant `BatchWriter.ChunkSize = 1000`.

The body, in this order, is the spec's "Running it" and "Transactions" made concrete:

1. `ArgumentNullException.ThrowIfNull(parameters)` (and the connection under `methods`), and the token checks, as the query method has them.  Under `RefusesCompletedTransaction`, the completed-transaction check of the caller's `transaction`.
2. `using var items = parameters.GetEnumerator();` and `if (!items.MoveNext())` return the empty result (`[]` of the collection type, `0`, or nothing) before anything else.
3. The connection: acquire (repository) or take the argument, and open it when `Closed`, closed again in a `finally`, as `CommandWriter` does.
4. The transaction: `var ownsTransaction = transaction is null && global::System.Transactions.Transaction.Current is null && <mode is not Off>;` (a repository has no `transaction` argument: test the ambient one only).  When it owns one, `transaction = await connection.BeginTransactionAsync(<IsolationLevel or none>, cancellationToken)`, `IsolationLevel.ReadUncommitted`/`ReadCommitted`/`RepeatableRead`/`Serializable`/`Snapshot` for the five isolation words and no argument for `on`.
5. A `try`: a loop over chunks.  Each chunk is `connection.CreateBatch()`, its `Transaction` the transaction or null, its `Timeout` `commandTimeout.Value` when set; then, for up to `ChunkSize` items from the enumerator, the item checked (`item is null` throws `ArgumentNullException` with the message `"Item <index> of the collection is null."` and the argument name `parameters`), its lists counted and checked as the query method checks them, a `batch.CreateBatchCommand()` with `CommandText` the item's SQL (built as `CommandWriter.AppendSql` builds it, from the item's lists), its parameters bound by `ParameterBinder.For(engine)` with the batch command as the command and the item as the input, and `batch.BatchCommands.Add`.  The index of each chunk's first item is kept for the exception.
6. Executing a chunk: `none` and `rowcount` call `ExecuteNonQueryAsync` and add each `BatchCommands[i].RecordsAffected` that is not negative to the total (`rowcount` returns the total); the others call `ExecuteReaderAsync(CommandBehavior.Default)` and, for each command in order, read its result set with `RowReaderWriter`, then `NextResultAsync`: `many` adds every row, `one` adds the one row and throws `UnexpectedRowCountException(queryName, moreThanOne, itemIndex)` for none or a second, `one-optional` adds the row when there is one and throws for a second, with the item's index likewise.  Rows go into the method's buffer (`RowBuffer` under `array-pool`, else a `List<T>`), ended as a `many` query method ends it.
7. A `catch (global::System.Data.Common.DbException exception) when exception is not global::SqlSource.CollectionItemException` that throws `new CollectionItemException(queryName, firstIndexOfTheChunk + batch.BatchCommands.IndexOf(exception.BatchCommand), exception)` when `exception.BatchCommand` is in the batch, and rethrows otherwise.
8. After the last chunk, `await transaction.CommitAsync(cancellationToken)` when it owns it.  A `catch` of anything, when it owns the transaction and `transaction.Connection is not null`, rolls back (`RollbackAsync(CancellationToken.None)` in its own `try` that swallows nothing but lets the first exception propagate), then `throw;`.  The owned transaction is disposed in a `finally`.

- [ ] **Step 1: Write the failing writer tests** in `BatchMethodWriterTests`, on PostgreSQL methods built like `QueryMethodWriterTests.GetUser` with `Batch = new(TransactionMode.On)`, asserting with `ShouldContain` the lines that pin each numbered point above: the empty return; `BeginTransactionAsync(cancellationToken)` for `On`, `BeginTransactionAsync(global::System.Data.IsolationLevel.Serializable, cancellationToken)` for `Serializable`, no `BeginTransactionAsync` for `Off`; `global::System.Transactions.Transaction.Current is null`; `CreateBatch()`; `CreateBatchCommand()`; `ChunkSize`'s `1000`; `new global::SqlSource.CollectionItemException(`; `CommitAsync`; `RollbackAsync`; for `rowcount`, `RecordsAffected`; for a query under `keep-comments`, no comment in the SQL.  Then compile the generated source of each shape with the generator harness (as the repository's generator tests compile theirs), and expect no error.
- [ ] **Step 2: Run them to see them fail.**  Run: `dotnet test tests/SqlSource.Tests --filter "FullyQualifiedName~BatchMethodWriterTests"`.  Expected: FAIL.
- [ ] **Step 3: Implement `BatchWriter` and `BatchRowsWriter`** with the shared pieces of `CommandWriter` and `RowsWriter`; a method with `Batch` set is written by `BatchWriter` from `QueryMethodWriter.Append`.
- [ ] **Step 4: Run every test of `tests/SqlSource.Tests`.**  Expected: PASS; no query method's golden text changes.
- [ ] **Step 5: Commit**: `feat: write the body of a collection method`

### Task 6: PostgreSQL end to end

**Files:**
- Create: `tests/DatabaseTests/Postgres/EndToEnd/Queries/Batches.sql` (its sidecar written by `tools/describe-end-to-end.sh`), `tests/DatabaseTests/Postgres/EndToEnd/BatchQueries.cs`, `tests/DatabaseTests/Postgres/EndToEnd/BatchQueryTests.cs`
- Modify: `EndToEnd/app/schema.sql` only if no table fits (prefer a temporary table of the test's connection, as `StreamingQueryTests` does for a commit)

**Interfaces:**
- Consumes: the generated methods of Tasks 3–5.

`Batches.sql`, `-- generator: collection-method`, `-- output: methods`: `InsertNote -> one` (`INSERT … RETURNING id, …`), `InsertNoteRowCount -> rowcount`, `FindNotes -> many` (a `SELECT … WHERE author = @author`), `FindNote -> one-optional`, `InsertNoteSerializable` (`-- generator: transaction=serializable`), `InsertNoteNoTransaction` (`-- generator: transaction=off`), and a repository copy for one test.

- [ ] **Step 1: Write the failing tests** (each in a transaction that is rolled back, or on a temporary table for a commit, per the folder's `AGENTS.md`): `InsertNote_ThreeItems_ReturnsTheirRowsInOrder`; `InsertNoteRowCount_ThreeItems_ReturnsTheTotal`; `FindNotes_TwoItems_ReturnsEveryRowOfEachInOrder`; `FindNote_ItemWithNoRow_IsLeftOut`; `InsertNote_Empty_ReturnsEmptyAndLeavesTheConnectionClosed`; `InsertNote_OneThousandAndOneItems_ReturnsAllInOrder` and `…_OneThousandItems_…`; `InsertNote_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwoAndLeavesNothing` (`ItemIndex == 2`, `InnerException is PostgresException { SqlState: "23505" }`, no row committed); `NullItemInTheSecondChunk_RollsBackTheFirst` (1,500 items, the 1,500th null: `ArgumentNullException`, no row committed); `One_ItemWithNoRow_ThrowsUnexpectedRowCountAndRollsBack`; `InsertNoteNoTransaction_FailureInTheSecondChunk_KeepsTheFirstChunk` (PostgreSQL rolls back only the failing chunk); `InsertNote_CallersTransaction_IsUsedAndNotCommitted`; `InsertNote_InATransactionScope_BeginsNoTransactionOfItsOwn`; `InsertNoteSerializable_RunsAtSerializable` (`SHOW transaction_isolation` inside a query of the batch, or a query that returns `current_setting('transaction_isolation')`); `Repository_InsertNote_CommitsItsOwnTransaction` (on a temporary table, or a row it deletes after).
- [ ] **Step 2: Run them to see them fail.**  Run: `tools/describe-end-to-end.sh` for the new sidecar, then `dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~BatchQueryTests"`.  Expected: FAIL only where Task 5 has a defect; fix the generator (TDD: a writer test first) until each passes.
- [ ] **Step 3: Run the project on the floor image**: `SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 dotnet test tests/DatabaseTests/Postgres --filter "FullyQualifiedName~BatchQueryTests"`.  Expected: PASS.
- [ ] **Step 4: Commit**: `test: run collection methods on PostgreSQL`

### Task 7: SQL Server

**Files:**
- Modify: `src/SqlSource/Generation/Emitting/QueryMethods/ExecutionCapabilityRules.cs` (a batch method on SQL Server: `RefusesCompletedTransaction` when it takes a transaction, no `DrainsLaterResults`, no `DrainsWhenLeftAtARow`; the rollback checks as below)
- Modify: `BatchWriter.cs`, `BatchRowsWriter.cs`
- Create: `tests/DatabaseTests/SqlServer/EndToEnd/Queries/Batches.sql` (+ sidecar), `EndToEnd/BatchQueries.cs`, `EndToEnd/BatchQueryTests.cs`
- Test: `BatchMethodWriterTests.cs`

**Interfaces:**
- Consumes: Task 5's writer.

SQL Server's additions, from the spec's "Transactions" and "SQL Server queries with several result sets":
- **The result-set guard** (methods that read rows): before reading command `i`'s result set, `if (reader.FieldCount != <the output's column count>)` throw `InvalidOperationException("The query '<name>' returned a result set of another shape for item <index>, so it cannot run over a collection: a query whose statements return more than one result set cannot.")`; after the last command of a chunk, `if (await reader.NextResultAsync(...))` throw the same.  The owned transaction rolls back (point 8).
- **With the caller's transaction**: an exception of the method's own (`UnexpectedRowCountException`, a converter's, a null in a column that cannot be null, the guard's) after which `transaction.Connection is null` is thrown as `new TransactionRolledBackException(queryName, exception)`; a `DbException` from the database is wrapped (point 7) and thrown as it is, whatever the transaction's state.
- **With its own transaction**, point 8 skips the rollback when `transaction.Connection is null`.

- [ ] **Step 1: Write the failing writer tests**: the guard's `FieldCount` check and its message; `TransactionRolledBackException` only behind `transaction.Connection is null` after a non-database exception; no drain loop.
- [ ] **Step 2: Write the failing end-to-end tests**, each as Task 6's equivalents (`OUTPUT inserted.…` for `RETURNING`) plus: `InsertNote_DuplicateKeyNoTransaction_…` is **not** written (SQL Server commits each item without a transaction: covered in the manual, not a promise to test beyond `InsertNoteNoTransaction_FailureAtItemTwo_KeepsTheOthers`, which asserts items 0, 1 and 3 committed on a temporary table); `XactAbortOn_CallersTransaction_ThrowsTheWrappedErrorAndDoesNotRollBack` (`SET XACT_ABORT ON` on the connection, a duplicate key: `CollectionItemException` whose inner is a `SqlException`, the transaction's `Connection` null afterward); `ConversionErrorInOwnTransaction_RollsBackWithoutAnError`; `TwoResultSetQuery_Throws` (a query `SELECT @id AS id; SELECT 2 AS other`, `-> many`: `InvalidOperationException` naming the query); `InsertNoteSnapshot_RunsAtSnapshot` (the test database allows snapshot isolation, or the test sets it up in its own database as an existing test does, else it is skipped with a reason).
- [ ] **Step 3: Run them to see them fail**: `tools/describe-end-to-end.sh`, then `dotnet test tests/DatabaseTests/SqlServer --filter "FullyQualifiedName~BatchQueryTests"`.  Fix until they pass.
- [ ] **Step 4: Run on the floor image**: `SQLSOURCE_TESTS_SQLSERVER_IMAGE=mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04 dotnet test tests/DatabaseTests/SqlServer --filter "FullyQualifiedName~BatchQueryTests"`.  Expected: PASS.
- [ ] **Step 5: Commit**: `feat: collection methods on SQL Server`

### Task 8: Documentation and examples

**Files:**
- Modify: `docs/manual/generator.md` (a "Collection methods" section after "What each shape returns": the opt-in, the signature, returns by shape, transactions and the caller's, chunks, `CollectionItemException` with the catch `catch (CollectionItemException e) when e.InnerException is SqlException { Number: 2627 }`, the PostgreSQL comment ruling; "Transactions and timeouts" of the repository says a collection method begins its own transaction; a `CollectionItemException` section beside the other two exceptions)
- Modify: `docs/manual/settings.md` (the two parameters in the generator-parameters table, with `transaction`'s values; the section says a parameter takes one of its own words, `on` and `off` being the common case; the Values cell of the Generator parameters row)
- Modify: `docs/manual/concepts.md` (the glossary's generator parameter: "turned on or off, or given one of its values")
- Modify: `docs/manual/postgresql.md`, `docs/manual/sqlserver.md` (a failure with and without a transaction; on SQL Server, which queries can run over a collection and the guard's exception)
- Modify: `docs/manual/recipes.md` (inserting many rows), `docs/manual/README.md` ("What works today" row), `README.md` (its transactions FAQ, if it names transactions)
- Modify: `docs/architecture/generator.md` (the batch method: `QueryMethod.Batch`, the planner's second method, `BatchWriter`, `BatchRowsWriter`, the capabilities of a batch method, `CollectionItemException` in "The exceptions"), `src/SqlSource/Generation/Emitting/QueryMethods/AGENTS.md` or the folder's nearest `AGENTS.md` if it lists the phase files
- Modify: `examples/PostgresExamples` and `examples/SqlServerExamples`: an `InsertUsers`-style query with `-- generator: collection-method` in an existing `Sql/` file of each, its call in the example's code beside the single-item call, sidecars by `tools/describe-end-to-end.sh`, each example's `README.md`
- Test: `tests/SqlSource.Repository.Tests` (the docs' link, sample and diagnostics tests)

- [ ] **Step 1: Write the docs and the examples.**  Each `sql` sample's info string names its dialect.
- [ ] **Step 2: Run** `tools/describe-end-to-end.sh` and `dotnet test tests/SqlSource.Repository.Tests`, and build both example projects.  Expected: PASS.
- [ ] **Step 3: Commit**: `docs: document collection methods`

### Task 9: Validate and review the branch

- [ ] **Step 1:** `./pre-commit-validation.sh --full` on the branch as it will be pushed.  Expected: every check passed.
- [ ] **Step 2:** `branch-review:review`, fixing every finding, minor ones included, until it is clean.
- [ ] **Step 3:** The version check before the PR: `git fetch --tags origin`; compare `VersionPrefix` with the highest `v*` tag; ask the owner when it is not greater (there are no tags today).
