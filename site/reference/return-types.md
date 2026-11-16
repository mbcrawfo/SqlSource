# What a method returns (arrays, lists, streaming)

<a id="method-shapes"></a>

## What each shape returns

A query's [result shape](sql-files.md#result-shapes) says what its method does with the rows.  That section lists the five shapes, the shape of a query that has none, and their errors.

Under the output `repository` or `methods`, what a query method returns depends on the result shape of the query.  A [synchronous method](synchronous-methods.md#synchronous-methods) returns what the task holds, with no `Task`.  For `none`, it returns nothing, `void`.

| Shape | Returns |
|----|----|
| `many` | `Task<C>`, where `C` is the [collection type](#setting-collection-type) of the output model: an array by default, `Task<ListFromDto[]>`.  For the collection type `IAsyncEnumerable`, `IAsyncEnumerable<TDto>`, with no `Task` |
| `one` | `Task<TDto>`, the output model |
| `one-optional` | `Task<TDto?>` |
| `none` | `Task` |
| `rowcount` | `Task<int>` |

- **`many` reads every row into the [collection type](#setting-collection-type)**, an array by default.  The collection is complete when the task completes.
- **For the collection type `IAsyncEnumerable`, `many` gives each row to the caller's loop as it is read.**  See [streaming rows](#streaming-rows).
- **`many` reads the rows into buffers that it borrows from .NET's shared array pool**, unless the query has [`array-pool=off`](generator-parameters.md#setting-generator-parameters).  This holds for every collection type but `ImmutableList<T>`, `IImmutableList<T>` and `IAsyncEnumerable<T>`, which holds no rows.  Once the pool holds buffers of the needed sizes, the method allocates no collection but the one it returns, for up to about 1,000 rows.
- **`one` returns the one row.**  It throws [`UnexpectedRowCountException`](exceptions.md#unexpected-row-count) when the query returns no rows, or more than one.  It reads a second row to see whether there is one.
- **`one-optional` returns the row, or null when there is none.**  It throws `UnexpectedRowCountException` when the query returns more than one row.
- **`none` runs the statement and returns nothing.**
- **`rowcount` runs the statement and returns the count that the driver reports.**  The count is the rows inserted, updated or deleted.  When the query has several statements, the count adds up the rows of each.  For a `SELECT`, the count is `-1`.  The rows of a `RETURNING` or `OUTPUT` clause are counted.
- **On PostgreSQL, a `WITH` whose last statement is a `SELECT` counts as a `SELECT`.**  So its count is `-1`, though its `WITH` updates rows.
- **On SQL Server, `SET NOCOUNT ON` makes the count `-1`.**  When a query without parameters sets it, every later count on the connection is `-1` too: see [a limit](databases/sqlserver/query-methods.md#limit-rowcount-after-nocount).

`none` and `rowcount` read no rows.  So they suit an `INSERT ... RETURNING` whose rows you do not want.

On SQL Server, a query of several statements returns the rows of its first result set, as its [model](databases/sqlserver/index.md#what-can-be-described) has them.  Its other statements still run, and every method reads the rest of the batch before it returns.

- **An error that one of the other statements raises is thrown by every method.**  Depending on the error and on `XACT_ABORT`, the error may have rolled back the transaction that the method was given.
- **A later rollback is thrown as [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back).**  The method throws it when a later error rolls the transaction back after one of these:
  - The method throws an exception of its own first, such as `UnexpectedRowCountException`.
  - The method reaches an error that the database raises for a row, or for a statement after the rows, and that leaves the transaction open.  An example is a divide by zero at a row.

  See [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).

<a id="setting-collection-type"></a>

## Collection type

```sql
-- name: ListUsers
-- collection-type: IReadOnlyList
SELECT id, name FROM users;
```

The collection type is the type in which the [method](query-methods.md#query-methods) of a query whose result shape is `many` returns its rows.  Above, `ListUsers` returns `Task<IReadOnlyList<ListUsersDto>>`.

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

`T` is the query's output model.  The default is `Array`.

You can write it in the file header and inside a query.  In a settings file, it is `collectionType`.

- **Every collection but `IAsyncEnumerable` is filled before the method's task completes**, `IEnumerable` included.  So reading it runs no query.
- **It changes nothing for a query whose shape is not `many`**, and nothing under the output `models` or `sql`.

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

The method of a `many` query whose collection type is `IAsyncEnumerable` returns `IAsyncEnumerable<T>`.  It gives your loop each row as it is read.  So the loop starts on the first row while the rest are still to come, and the method never holds all the rows at once.

The method takes the arguments that every [query method](connection-methods.md#method-signature) takes.  Under `async-method-suffix`, its name ends with `Async`, as the name of every method does.  Under `method-type=both`, its name ends with `Async` too.  A stream has no synchronous method:

- Under [`method-type=sync`](synchronous-methods.md#synchronous-methods), such a query is [SQLSRC298](../diagnostics.md#sqlsrc298).
- Under `method-type=both`, the query gets its stream alone.  When the query has a [collection method](collection-methods.md#collection-methods), it also gets a synchronous collection method, which returns an array.

The rules of the call:

- **The call checks the arguments and runs nothing.**  The method throws at the call when `connection` or `parameters` is null.  It also throws at the call when the text of a token is blank, unless [`token-validation=off`](tokens.md#token-validation) is in effect.
- **The query runs when the loop asks for the first row, not at the call.**  Then the method opens the connection, reads the values of the `parameters` model and sends the query.  A repository method also gets its connection from its source then.
- **Each loop over the result runs the query again**, with the values that the `parameters` model holds then.
- **An enumerator that is disposed before it asks for a row opens nothing.**
- **On SQL Server, a transaction that has completed is refused when the loop asks for the first row**, not at the call.  The connection is not opened.  See [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).

The rules of the connection:

- **The connection stays open until the loop ends.**  A closed connection is opened for the loop, and closed again when the loop ends.  An open connection stays open.
- **The loop ends in one of these ways**: it finishes, it reaches a `break`, its body throws an exception, or the enumerator is disposed.
- **A repository method holds the connection it got for the whole loop.**  It disposes the connection when the loop ends, unless it borrowed it.
- **While the loop runs, the connection is busy with the query.**  So the driver refuses another command on it in the loop's body:
  - Npgsql throws `NpgsqlOperationInProgressException`.
  - Microsoft.Data.SqlClient throws `InvalidOperationException`, on a connection without `MultipleActiveResultSets`.

  Run such a command on another connection, or read the rows into a collection first.

The rules of leaving the loop early:

- **Leaving the loop early reads the rest of the rows.**  At a `break`, or at an exception in the loop's body, the rows still to come are read and discarded before the loop ends.  On SQL Server, so are the statements after them.  So on a large result, leaving early takes as long as reading those rows.
- **Leaving early does not cancel the query.**  A transaction is left as it was, unless one of these happens:
  - The database raises an error for one of those rows, such as a division by zero.
  - On SQL Server, the database raises an error for one of those statements.
- **The database's error for the rows still to come is thrown when the loop ends, whatever ended it.**  On SQL Server, so is its error for the statements after them.  The error is thrown at the `break`, and in place of an exception that the loop's body threw.  So an error in the query's rows, which may have changed your transaction, always reaches your code.
- **On PostgreSQL, the error also aborts a transaction that the query ran in**, as any error does.  So the next command of the transaction is refused until the transaction is rolled back.
- **On SQL Server, the error may have rolled the transaction back**, depending on the error and on `XACT_ABORT`.
- **On SQL Server, a later rollback is thrown as `TransactionRolledBackException`.**  When the error leaves the transaction open and a later error of the batch rolls it back, the loop throws `TransactionRolledBackException`.  Its `InnerException` is the error for the row or the statement.  See [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).
- **To keep the body's exception, catch it in the loop's body.**  Then:
  1. In the loop's body, keep the exception in a variable and `break`.
  2. Around the loop, catch the database's exception, `PostgresException` or `SqlException`.  On SQL Server, catch `TransactionRolledBackException` too.
  3. In that catch, throw the exception you kept if there is one.  Else, throw the exception you caught again.
  4. After the loop, throw the exception you kept if there is one.  A `break` with no later row failing ends the loop with no exception at all.
- **Or leave no row to fail.**  Have the query return only the rows the loop needs, with a `WHERE` or a `LIMIT` or `TOP`.

The rules of each row:

- **Each row is read as every `many` method reads it**, with the same checks and [converters](converters.md#converters).
- **An error at a row that the loop reads is thrown when the loop reaches that row**, after the rows before it.  Examples are a null in a column whose property cannot be null, and an exception that a converter throws.  An error that the database raises for that row is another.  See [how a row is read](query-methods.md#reading-rows).
- **A row that the loop leaves unread is not checked or converted.**  Of its errors, only one that the database raises can reach your code, as the rules of leaving the loop early say.
- **On SQL Server, a rollback after an error at a row is thrown as `TransactionRolledBackException`.**  The loop throws it when an error that the rest of the query raises rolls the transaction back after one of these:
  - A check or a converter throws for a row in a transaction.
  - The database raises an error for a row, and the error leaves the transaction open.

  Its `InnerException` is that exception or error.  See [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).

The rules of cancelling and of several statements:

- **Either token cancels.**  The method's `cancellationToken` cancels the loop, and so does a token given to `WithCancellation`.  The loop throws what the driver throws when a method is cancelled: see [PostgreSQL](databases/postgresql/query-methods.md#method-results) and [SQL Server](databases/sqlserver/query-methods.md#method-results).
- **On SQL Server, a query of several statements gives the rows of its first result set.**  Its other statements run before the loop ends.
- **On SQL Server, an error that one of the other statements raises is thrown after the last row, or at a `break`.**  Depending on the error and on `XACT_ABORT`, it may have rolled back the transaction that the query ran in.  See [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).
