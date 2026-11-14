# What a method returns (arrays, lists, streaming)

<a id="method-shapes"></a>

## What each shape returns

The [result shape](sql-files.md#result-shapes) after the query's name says what the method does with the rows.  Without one, a query that returns rows is `many`, and one that returns none is `rowcount`.

What a query method returns, at either output, depends on the query's result shape.  A [synchronous method](query-methods.md#synchronous-methods) returns what the task holds, with no `Task`, and nothing, `void`, for `none`:

| Shape | Returns |
|----|----|
| `many` | `Task<C>`, where `C` is the [collection type](#setting-collection-type) of the output model: an array by default, `Task<ListFromDto[]>`.  For the collection type `IAsyncEnumerable`, `IAsyncEnumerable<TDto>`, with no `Task` |
| `one` | `Task<TDto>`, the output model |
| `one-optional` | `Task<TDto?>` |
| `none` | `Task` |
| `rowcount` | `Task<int>` |

- **`many`** reads every row into the [collection type](#setting-collection-type), an array by default, and complete when the task completes, or, for the collection type `IAsyncEnumerable`, gives each row to the caller's loop as it is read: see [streaming rows](#streaming-rows).  Unless the query has [`array-pool=off`](generator-parameters.md#setting-generator-parameters), the rows of every collection type but `ImmutableList<T>`, `IImmutableList<T>` and `IAsyncEnumerable<T>`, which holds no rows, are read into buffers borrowed from .NET's shared array pool, so that, once the pool holds buffers of the sizes it needs, the method allocates no collection for up to about 1,000 rows but the one it returns.
- **`one`** returns the one row.  It throws [`UnexpectedRowCountException`](exceptions.md#unexpected-row-count) when the query returns no rows, or more than one: it reads a second row to see whether there is one.
- **`one-optional`** returns the row, or null when there is none, and throws the same exception for more than one.
- **`none`** runs the statement and returns nothing.
- **`rowcount`** runs the statement and returns the count the driver reports: the rows inserted, updated or deleted, those of each statement added up when the query has several, and `-1` for a `SELECT`.  The rows of a `RETURNING` or `OUTPUT` clause are counted.  On PostgreSQL a `WITH` whose last statement is a `SELECT` counts as a `SELECT`, so it is `-1` though its `WITH` updates rows.  On SQL Server, `SET NOCOUNT ON` makes it `-1`, and when a query without parameters sets it, every later count on the connection too: see [a limit](databases/sqlserver/query-methods.md#limit-rowcount-after-nocount).

`none` and `rowcount` read no rows, so they suit an `INSERT ... RETURNING` whose rows you do not want.  `one`, `one-optional` and `many` on a query that returns no rows are [SQLSRC272](../diagnostics.md#sqlsrc272).  A SQL Server query of several statements returns the rows of its first result set, as its [model](databases/sqlserver/index.md#what-can-be-described) has them, and its other statements still run: an error that one of them raises is thrown by every method, which reads the rest of the batch before it returns, and may have rolled back the transaction the method was given, depending on the error and on `XACT_ABORT`.  When the method throws an exception of its own first, such as `UnexpectedRowCountException`, or reaches an error that the database raises for a row or for a statement after the rows and that leaves the transaction open, such as a divide by zero at a row, and a later error then rolls the transaction back, it throws [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back): see [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).

<a id="setting-collection-type"></a>

## Collection type

```sql
-- name: ListUsers
-- collection-type: IReadOnlyList
SELECT id, name FROM users;
```

The type that the [method](query-methods.md#query-methods) of a query whose result shape is `many` returns its rows in.  The default is `Array`.  Above, `ListUsers` returns `Task<IReadOnlyList<ListUsersDto>>`.

| Value | Returns | Holding |
|----|----|----|
| `Array` (the default) | `T[]` | An array |
| `IEnumerable` | `IEnumerable<T>` | An array |
| `IReadOnlyCollection` | `IReadOnlyCollection<T>` | An array |
| `IReadOnlyList` | `IReadOnlyList<T>` | An array |
| `ICollection` | `ICollection<T>` | A `List<T>` |
| `IList` | `IList<T>` | A `List<T>` |
| `List` | `List<T>` | A `List<T>` |
| `ImmutableArray` | `ImmutableArray<T>` | An `ImmutableArray<T>` |
| `ImmutableList` | `ImmutableList<T>` | An `ImmutableList<T>` |
| `IImmutableList` | `IImmutableList<T>` | An `ImmutableList<T>` |
| `IAsyncEnumerable` | `IAsyncEnumerable<T>` | Nothing: each row is read as the loop asks for it |

`T` is the query's output model.  Every collection but `IAsyncEnumerable` is filled before the method's task completes, `IEnumerable` included, so reading it runs no query.

- It stands in the file header and inside a query.  It can also be `collectionType` in a settings file.
- It changes nothing for a query whose shape is not `many`, and nothing under the output `sql` or `models`.

<a id="streaming-rows"></a>

## Streaming rows

```sql postgres
-- name: StreamUsers
-- collection-type: IAsyncEnumerable
SELECT id, name FROM users ORDER BY id;
```

```csharp
// Generated, shown without global::
public IAsyncEnumerable<StreamUsersDto> StreamUsers(
    CancellationToken cancellationToken = default)

// Your code
await foreach (var user in users.StreamUsers(cancellationToken))
{
    await writer.WriteLineAsync(user.Name);
}
```

The method of a `many` query whose collection type is `IAsyncEnumerable` returns `IAsyncEnumerable<T>`, and gives your loop each row as it is read.  So the loop starts on the first row while the rest are still to come, and the rows are never all held at once.  The method takes the arguments that every [query method](connection-methods.md#method-signature) takes, and its name ends with `Async` under `async-method-suffix`, as every method's does, and under `method-type=both`.  A stream has no synchronous method: under [`method-type=sync`](query-methods.md#synchronous-methods) such a query is [SQLSRC298](../diagnostics.md#sqlsrc298), and under `method-type=both` it gets its stream alone, and a synchronous [collection method](collection-methods.md#collection-methods), which returns an array, when it has one.

- **The call checks the arguments and runs nothing.**  A null `connection` or `parameters` throws when the method is called, and so does a token's text that is blank, unless [`token-validation=off`](tokens.md#token-validation) is in effect.  A repository method gets its connection from its source, and the connection is opened, the values of the `parameters` model read and the query sent when the loop asks for the first row, not at the call, and each loop over the result runs the query again with the values the model holds then.  An enumerator that is disposed before it asks for a row opens nothing.  On SQL Server, a transaction that has completed is refused when the loop asks for the first row, not at the call, and the connection is not opened: see [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).
- **The connection stays open until the loop ends.**  A closed connection is opened for the loop and closed again when the loop ends: when it finishes, at a `break`, at an exception in its body, or when the enumerator is disposed.  An open connection stays open.  A repository method holds the connection it got for the whole loop, and disposes it when the loop ends, unless it borrowed it.  While the loop runs, the connection is busy with the query, so another command on it in the loop's body is refused by the driver: Npgsql throws `NpgsqlOperationInProgressException`, and Microsoft.Data.SqlClient, on a connection without `MultipleActiveResultSets`, `InvalidOperationException`.  Run such a command on another connection, or read the rows into a collection first.
- **Leaving the loop early reads the rest of the rows.**  At a `break`, or an exception in the loop's body, the rows still to come, and on SQL Server the statements after them, are read and discarded before the loop ends, so on a large result that takes as long as reading them.  The query is not cancelled, and a transaction is left as it was, unless the database raises an error for one of those rows, such as a division by zero, or SQL Server for one of those statements.  That error is thrown when the loop ends, whatever ended it: at the `break`, and in place of an exception that the loop's body threw, so that an error in the query's rows, which may have changed your transaction, always reaches your code.  On PostgreSQL it also aborts a transaction that the query ran in, as any error does, so the transaction's next command is refused until it is rolled back; on SQL Server it may have rolled the transaction back, depending on the error and on `XACT_ABORT`, and when it leaves the transaction open and a later error of the batch rolls it back, the loop throws `TransactionRolledBackException` with that error as its `InnerException`: see [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).  To keep the body's exception, catch it in the loop's body, keep it in a variable and `break`.  Around the loop, catch the database's exception, `PostgresException` or `SqlException`, and on SQL Server `TransactionRolledBackException` too, and throw the exception you kept in its place if there is one, or else throw the exception you caught again.  After the loop, throw the exception you kept if there is one, since a `break` with no later row failing ends the loop with no exception at all.  Or have the query return only the rows the loop needs, with a `WHERE` or a `LIMIT` or `TOP`, so that no row is left to fail.
- **Each row is read as every `many` method reads it**, with the same checks and [converters](converters.md#converters).  An error at a row that the loop reads, such as a null in a column whose property cannot be null, or an error that the database raises for that row, is thrown by the loop when it reaches that row, after the rows before it: see [how a row is read](query-methods.md#reading-rows).  A row that the loop leaves unread is not checked or converted: of its errors, only one that the database raises can reach your code, as the item above says.  On SQL Server, when such a check or converter throws for a row in a transaction, or the database raises an error for that row that leaves the transaction open, and an error that the rest of the query raises then rolls the transaction back, the loop throws `TransactionRolledBackException` with that exception or error as its `InnerException`: see [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).
- **Either token cancels.**  The method's `cancellationToken`, and a token given to `WithCancellation`, each cancel the loop, which throws what the driver throws when a method is cancelled: see [PostgreSQL](databases/postgresql/query-methods.md#method-results) and [SQL Server](databases/sqlserver/query-methods.md#method-results).
- **On SQL Server, a query of several statements** gives the rows of its first result set, and its other statements run before the loop ends.  An error that one of them raises is thrown after the last row, or at a `break`, as the item above says, and depending on the error and on `XACT_ABORT` it may have rolled back the transaction the query ran in: see [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).
