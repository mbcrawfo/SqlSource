# Streaming query methods - design

Date: 2026-10-26

Corrected on 2026-10-27 from the end-to-end tests: what a query of several statements does on SQL Server, in "Behaviour the user sees" and "Testing".

Corrected on 2026-10-28 from the end-to-end tests: what leaving the loop early does when a row still to come fails, in "Behaviour the user sees".

Corrected on 2026-10-28 from a branch review's reproduction and the end-to-end tests: on SQL Server under `SET XACT_ABORT ON`, an error that is not reported rolls back the caller's transaction with nothing thrown, in "Behaviour the user sees".  Corrected again the same day from a second review's probes: whether such an error rolls back the transaction depends on the error as well as on `XACT_ABORT`, since with it off a conversion of text that is not a valid value of its type rolls the transaction back too, and an overflow does not.

A feature outside the [query generation epic](2026-10-07-query-generation-epic-design.md), which listed `IAsyncEnumerable<T>` as out of its scope.  It lets the generated method of a `many` query hand its rows to the caller one at a time, as an `IAsyncEnumerable<T>`, instead of reading them all into a collection first.

## Intent

The owner's request: "Add support for IAsyncEnumerable query methods", for **processing rows as they arrive**: a caller starts work on the first row while the rest are still being read, and pipes the rows into LINQ over async streams, a response stream or another consumer without holding them all.

The owner's direction:

- **It is a value of the collection type.**  "From a user POV it states what data is returned, not the literal return type."  So `CollectionType` gains `IAsyncEnumerable`, and nothing new is added to the `.sql` grammar or the attribute.
- **The arguments are checked at the call.**  The method is split so that a null `connection` or `parameters`, or a blank token, throws when the method is called, not on the first `MoveNextAsync`.
- **A loop that stops early drains.**  When the caller leaves the loop before the last row, the reader is disposed and the driver reads and discards the rest.  The command is not cancelled: on PostgreSQL a cancelled statement aborts the caller's transaction, and a `break` that silently kills a transaction is worse than a slow one.
- **The work is in a local function** of the generated method (approach A below).

## Base

The owner's answer: this is built on `main` after `integration/pooled-row-buffer` and `integration/settings-files` have both merged.  Both have (#153 and #158).  The first rewrote the writer's `many` path (`GetBuffer`, `AppendExecution`, the `Append` signature), and added `RowBuffer.IsUsedBy`, which must not count a streaming method, so the file of a type whose `many` methods all stream gets no row buffer, and the `no-array-pool` generator parameter.  The second added `sqlsource.json`, whose `collectionType` takes the new value, and removed the MSBuild settings.  "What exists today" below describes `main` after both.

## Goals

1. `IAsyncEnumerable` is a value of the collection type at every level it has *(designer's call)*: `"collectionType": "IAsyncEnumerable"` in a `sqlsource.json`, which `schemas/settings-v1.schema.json` accepts, `CollectionType = GeneratorCollectionType.IAsyncEnumerable` on the attribute, and `-- collection-type: IAsyncEnumerable` in the file header and inside a query, matched as the other values are (any case, with or without hyphens and spaces).  Adding a value accepts more files and makes none invalid, so it is an edit of the `v1` schema, as `schemas/AGENTS.md` allows.
2. The method of a `many` query whose collection type is `IAsyncEnumerable` returns `IAsyncEnumerable<TDto>`, yielding each row as it is read.
3. Its argument checks run at the call.
4. Each row is read exactly as the buffered method reads it: by position, with the same null checks, vector checks and converters.
5. The connection follows today's rule over the life of the loop: a closed connection is opened and closed again when the loop ends, by finishing, throwing, `break` or disposal; one that was not closed is left as it was.
6. `cancellationToken` and `WithCancellation(...)` both cancel, either one.

## Non-goals

- **Streaming any shape but `many`.**  The collection type changes nothing for `one`, `one-optional`, `none` and `rowcount`, as today.
- **A synchronous `IEnumerable<T>` that streams.**  The `IEnumerable` value stays a filled array; there are no synchronous methods.
- **Cancelling the command when the loop stops early.**  Decided above.
- **Several result sets**, which stay `D-0006`.
- **Anything under the output `sql` or `models`**, where the collection type changes nothing.

## Anti-goals

- **A change to a query that does not use it.**  Every other collection type's method is the same, byte for byte.
- **A new member the caller can see.**  No helper method on the user's type or the extension class, no new name that can collide (`SQLSRC274` is unchanged).
- **An argument error that waits for the loop.**

## What exists today

- **`CollectionKind`** (`src/SqlSource/Settings/CollectionKind.cs`) is the generator's form of the generated enum `GeneratorCollectionType` (`AttributeSource`), member for member, values 0 to 9.  `SettingsLevelReader` reads it with `source.Choice<CollectionKind>(SettingId.CollectionType)` at every level: a settings file, the attribute, the file header and the query.  `schemas/settings-v1.schema.json` lists the values twice, as an `enum` and as a pattern that ignores case, hyphens and spaces.
- **`QueryMethodWriter.Append`** (`src/SqlSource/Generation/Emitting/`) writes one `async Task<...>` method: `AppendChecks` (the `ArgumentNullException` and token checks), `AppendSql`, then the open-if-closed, a `try` holding the command, the bound parameters and `AppendExecution`, and a `finally` that closes the connection when the method opened it.  For `many`, `AppendExecution` adds each row to the buffer from `GetBuffer` in a `while (await reader.ReadAsync(...))` loop and returns the result.  `GetBuffer` uses the pooled `file struct` of `RowBuffer` when `RowBuffer.IsUsedBy(method)`, which needs `UseArrayPool` (false under `no-array-pool`) and one of the eight collection types it lists, and an allocated collection otherwise.  `GetResultType` and `GetCollectionType` give the return type, and `AppendDocumentation` the `<returns>`.
- **`LocalNames`** gives every local a name no argument has; a token named `await` is written `@await`.
- **Tests** of the collection type: `SettingValueTests` and `SettingsLevelReaderTests` (`tests/SqlSource.Tests/Settings/`), `SettingsFileReaderTests` and `SettingsFileSchemaTests` (`SettingsFiles/`), `SqlMarkerScopeTests` (`Parsing/Markers/`), `AttributeSourceTests` (`Generation/`), the writer's in `QueryMethodWriterTests` and the buffer's in the two `RowBufferTests` and `ArrayPoolParameterTests`, and end to end `tests/SqlSource.Tests.Postgres/EndToEnd/CollectionQueryTests.cs` with `Queries/Collections.sql`, and `tests/SqlSource.Tests.SqlServer/EndToEnd/MethodCollectionTests.cs` with `Queries/Collections/ByMarkers.sql`.
- **The manual** says, in [Collection type](../../manual/settings.md#setting-collection-type), that every collection is filled before the task completes, `IEnumerable` included; [the generator page](../../manual/generator.md#query-methods) gives `many` the return `Task<C>` and says which collection types read through the array pool; and the `no-array-pool` row of [generator parameters](../../manual/settings.md#setting-generator-parameters) says every `many` method reads its rows into a collection.

## Approaches

The owner picked A.

- **A. A local async iterator function (chosen).**  The generated method is not `async`: it runs the checks, then returns a call to a local `async IAsyncEnumerable<TDto>` function that holds today's body, with `yield return` where a row is added to the buffer.  It fits the writer's existing split, adds no member and no name a caller sees, and C# allows `await` in the `finally` and `yield return` inside a `try` that has only a `finally`.  The cost is reserving the local function's name, and its parameter's, in `LocalNames`.
- **B. A private static helper beside the method.**  The same work as a second member: under `MethodLocation` `Public`, `Internal` or `Private` it adds a member to the user's type, it needs a naming rule and a new collision case, and it gains nothing over A.
- **C. A generated enumerator class.**  Dropped: much generated code for what the compiler's iterator gives.

## Design

### What the user writes and gets

```sql postgres
-- name: ListUsers
-- collection-type: IAsyncEnumerable
SELECT id, name FROM users;
```

```csharp
// Generated, shown without global::
public static IAsyncEnumerable<ListUsersDto> ListUsers(
    this DbConnection connection,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)

// Your code
await foreach (var user in connection.ListUsers())
{
    ...
}
```

The arguments are the same as every method's.  The name takes `Async` under `async-method-suffix`, as every method's does.

### The generated method

```csharp
public static global::System.Collections.Generic.IAsyncEnumerable<ListUsersDto> ListUsers(
    this global::System.Data.Common.DbConnection connection,
    ...,
    global::System.Threading.CancellationToken cancellationToken = default)
{
    global::System.ArgumentNullException.ThrowIfNull(connection);
    // parameters and token checks, as AppendChecks writes them today

    return ReadRows(cancellationToken);

    async global::System.Collections.Generic.IAsyncEnumerable<ListUsersDto> ReadRows(
        [global::System.Runtime.CompilerServices.EnumeratorCancellation] global::System.Threading.CancellationToken cancellationToken)
    {
        // AppendSql, open if closed, try { command, parameters, reader,
        //     while (await reader.ReadAsync(cancellationToken)...) { vector checks; yield return row; } }
        // finally { close if opened }
    }
}
```

- **The outer method** is `static`, not `async`, with today's modifiers, `this` and arguments.  It holds the documentation, `AppendChecks`, and the `return` of the local function.
- **The local function** is `static async` and returns `IAsyncEnumerable<TDto>`.  *(Designer's call, after the builder check.)*  It captures nothing: it takes every argument of the outer method as a parameter of the same name, so the body's text is unchanged and no closure is allocated beside the iterator, which keeps the method within the allocation margin of `tests/AGENTS.md` against a hand-written iterator.  Its `cancellationToken` parameter is marked `[EnumeratorCancellation]`, so the compiler combines the token given to the call with one given to `WithCancellation`.  Everything after `AppendChecks` moves into it unchanged, except that `many`'s buffer and `return` become `yield return` of each row.
- **Names.**  The parameters shadow the outer arguments of the same names, which C# allows.  The local function's name comes from `LocalNames`, so a token cannot take it.
- **SQL built from tokens** may be built in the outer method or the local function; the builder chooses.  Either way the token checks are in the outer method.
- **The documentation**'s `<returns>` says the rows are read as the loop asks for them, and the `connection` text says the connection is closed again when the loop ends, when the method opened it.

### Behaviour the user sees

- **Nothing runs until the loop starts.**  The call checks the arguments and returns; the connection is opened, the parameters read from the input model and the query sent on the first `MoveNextAsync`.  Each enumeration runs the query again.
- **The connection is busy while the loop runs.**  Another command on it inside the loop fails, as each driver reports it (no MARS on SQL Server).  A connection the method opened stays open until the loop ends.
- **Leaving the loop early** disposes the reader, which reads and discards the rest of the rows, and then closes a connection the method opened.  On a large result this takes as long as reading the rest.  A transaction is left as it was, unless the server raises an error for a row still to come: on PostgreSQL that error is thrown from the disposal, at the `break` or in place of the loop body's exception, which is lost, and aborts the transaction, as [TD-0087](../../tech-debt/TD-0087-drained-rows-error-replaces-the-loop-bodys-exception.md) records; on SQL Server it is not reported, and depending on the error and on `XACT_ABORT` it can roll the transaction back with nothing thrown, as [TD-0088](../../tech-debt/TD-0088-sql-server-unreported-error-rolls-back-the-transaction.md) records.
- **An error in a later row** throws from `MoveNextAsync`, after the rows before it were yielded: a null in a column that cannot be null, a converter's exception, a driver error.
- **On SQL Server, a query of several statements** yields the rows of its first result set, and its other statements run when the reader is disposed.  An error that a later statement raises is not reported: neither `MoveNextAsync` nor the disposal throws it, and a buffered method of the same SQL loses it too, as [TD-0085](../../tech-debt/TD-0085-sql-server-error-after-the-first-result-set-is-lost.md) records; depending on the error and on `XACT_ABORT` it can roll back the caller's transaction, unreported, as TD-0088 records.
- **Cancellation** through either token throws as each driver throws today; [PostgreSQL](../../manual/postgresql.md#method-results) and [SQL Server](../../manual/sqlserver.md#method-results) already document it.

### Settings

- `CollectionKind.IAsyncEnumerable = 10` and `GeneratorCollectionType.IAsyncEnumerable = 10`, member for member, and the value in both lists of `schemas/settings-v1.schema.json`.  The setting's readers need nothing else; the builder confirms that each level's tests reach the new value.
- `RowBuffer.IsUsedBy` stays false for the new value, since nothing is buffered.  `no-array-pool` has no effect on a streaming method, as it has none on the two immutable lists.
- `GetResultType` is not used for this method's return type; the signature is written without `async` and `Task`.

## Testing

- **Unit:** the new value at each level (settings file and its schema, attribute, both markers); `RowBufferTests` that a streaming method does not use the buffer and a file whose `many` methods all stream gets none; `QueryMethodWriterTests` for the streaming method, with parameters, with tokens (including a token named `await`), with a vector column, with a converter, and under each `MethodLocation`; the generated code compiles with no warnings.
- **End to end, on both engines**, beside the existing collection tests:
  - the rows equal those of the array method for the same query;
  - a null `connection` or `parameters`, and a blank token, throw at the call, before any enumeration;
  - a closed connection is open during the loop and closed after it, after a `break`, and after an exception in the loop body; an open connection stays open;
  - after a `break` partway through, the connection runs another query, and inside a transaction the transaction still commits;
  - the method's `cancellationToken` and `WithCancellation` each cancel;
  - a null in a column that cannot be null throws at that row, after the rows before it;
  - a converted column reads as it does in the array method;
  - on SQL Server, a query of several statements: one that yields the rows of its first result set alone, and one whose `THROW` after its rows is thrown by no `MoveNextAsync` and not by the disposal, though the batch ran as far as the `THROW`.
- **Allocations**, in both `EndToEnd/AllocationTests.cs`: the streaming method, enumerated to the end, against a hand-written `static` local async iterator that makes the same calls, within the existing margin, which is never raised.  *(Designer's call, after the builder check.)*
- **The floor driver projects**, `SqlSource.Tests.Postgres.Npgsql8` and `SqlSource.Tests.SqlServer.SqlClient61`: one streaming query each, if the builder finds behaviour on the floor drivers can differ.

## Documentation and examples

By rule 4 of the root `AGENTS.md`, in the same pull request:

- [`settings.md`](../../manual/settings.md#setting-collection-type): the row for `IAsyncEnumerable` (returns `IAsyncEnumerable<T>`, holding nothing: each row is read as the loop asks for it), the sentence that every collection is filled made true of the others only, and the behaviour above in short.  The "All settings" row's description if it lists values.  The `no-array-pool` row, which says every `many` method reads into a collection.  The settings file section, if it lists the values.
- [`generator.md`](../../manual/generator.md#query-methods): the return table's `many` row and the `many` bullet of what each shape returns, including its sentence on which types read through the array pool; the connection section, for a connection open over the loop.
- [`diagnostics.md`](../../manual/diagnostics.md): the two lists of the values `collectionType` and `-- collection-type:` take.
- `schemas/settings-v1.schema.json`: the value, in its `enum` and its pattern.
- [`recipes.md`](../../manual/recipes.md): a short recipe for processing rows as they arrive, if the builder judges the settings section is not enough.
- The manual's [What works today](../../manual/README.md#what-works-today) table: a row for streaming rows, Available.
- `README.md`, only where it lists the collection types or the method shapes.
- [`docs/architecture/generator.md`](../../architecture/generator.md): the method writer's description, for the split method.
- The [epic outline](2026-10-07-query-generation-epic-design.md), which is kept current until the epic closes: its two lines that put `IAsyncEnumerable<T>` out of scope say it was delivered by this design.
- `examples/PostgresExamples` and `examples/SqlServerExamples`: one `CodeGen/` query with `-- collection-type: IAsyncEnumerable` and a caller with `await foreach`, sidecars written by `tools/describe-end-to-end.sh`.

## Left to the builder

- Where SQL from tokens is built.
- The local function's name.
- Whether the floor driver projects need a case.
- Whether a recipe is worth adding.
- The exact manual wording, within the rules of the root `AGENTS.md`.
