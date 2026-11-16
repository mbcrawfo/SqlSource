# Call a query without await, or stream rows

Call a query from synchronous code with a sync method, or read a large result one row at a time as an `IAsyncEnumerable`.

**Use this when** code that cannot await, such as a command of a console tool, must run a query.  Use it too when a result is too large to hold in an array.

<a id="run-without-awaiting"></a>

## Run a query from code that does not await

```sql postgres
-- output: methods

-- name: GetUserName -> one-optional
-- generator: method-type=both
SELECT name
FROM users
WHERE id = @id;
```

```csharp
// A command of a console tool, whose API is synchronous
public int Run(int id)
{
    using var connection = dataSource.OpenConnection();
    GetUserNameDto? user = connection.GetUserName(new GetUserNameParams(id));
    Console.WriteLine(user?.Name ?? "no such user");
    return user is null ? 1 : 0;
}

// Elsewhere, code that awaits
GetUserNameDto? user = await connection.GetUserNameAsync(new GetUserNameParams(id), cancellationToken: cancellationToken);
```

**What you get.**  `-- generator: method-type=both` gives the query two methods.  The synchronous method, `GetUserName`, returns the row or null, and takes no cancellation token.  Beside it, `GetUserNameAsync` is the asynchronous method.

```csharp
// The same query at the default output, repository, without -- output: methods
public int Run(int id)
{
    GetUserNameDto? user = users.GetUserName(new GetUserNameParams(id));
    Console.WriteLine(user?.Name ?? "no such user");
    return user is null ? 1 : 0;
}
```

A repository gets the same methods, and its interface a member for each, so `users` can be the interface.

**The rules you need.**

- **`method-type=sync` gives the query the synchronous method alone.**  See [synchronous methods](../reference/query-methods.md#synchronous-methods).
- **A query with [`collection-method`](../reference/collection-methods.md#collection-methods) gets its collection method the same way**: synchronous, asynchronous or both.
- **The line can reach more than one query.**  See [generator parameters](../reference/generator-parameters.md#setting-generator-parameters) for where to write it.
- **On PostgreSQL, run a synchronous method on a data source without `Multiplexing=true`.**  See [what a method reads and throws](../reference/databases/postgresql/query-methods.md#method-results).
- **A repository method gets its connection from the repository.**  See [synchronous repository methods](../reference/repositories.md#synchronous-repository-methods).
- **If the only connection of a repository is a method that returns a task, mark a second one that returns none.**  See [a connection from a service of your own](repository-connection.md#connection-from-a-provider).

<a id="stream-rows"></a>

## Read rows one at a time (IAsyncEnumerable)

If a result is too large to hold in an array, stream its rows: your loop gets each row as it is read.

```sql postgres
-- name: StreamUsersOldestFirst
-- collection-type: IAsyncEnumerable
SELECT id, name, email
FROM users
ORDER BY created_at, id;
```

```jsonc
// Methods/sqlsource.json
{
  "output": "methods",
}
```

```csharp
[SqlSourceGenerate]
public sealed partial class UserQueries(DbConnection connection)
{
    public async Task WriteUserNamesAsync(TextWriter writer, CancellationToken cancellationToken = default)
    {
        await foreach (
            var user in connection.StreamUsersOldestFirst(cancellationToken: cancellationToken).ConfigureAwait(false)
        )
        {
            await writer.WriteLineAsync(user.Name).ConfigureAwait(false);
        }
    }
}
```

**What you get.**  At the output `methods`, which the folder's `sqlsource.json` sets, `StreamUsersOldestFirst` is a method of the connection that returns an `IAsyncEnumerable<StreamUsersOldestFirstDto>`.  A repository gets the same method, `users.StreamUsersOldestFirst(cancellationToken)`.

**The rules you need.**

- `-- collection-type: IAsyncEnumerable` on a query whose shape is `many` makes its method stream them.  The loop starts on the first row while the rest are still to come, and the rows are never all held at once.  See [Streaming rows](../reference/return-types.md#streaming-rows) and [Collection type](../reference/return-types.md#setting-collection-type).
- The call runs nothing.  The query runs when the loop asks for the first row, and each loop over the result runs it again.  See [Streaming rows](../reference/return-types.md#streaming-rows).
- The connection stays open until the loop ends, and is busy with the query.  Run no other command on it in the body of the loop.  Use another connection, or read the rows into a collection first.  See [Streaming rows](../reference/return-types.md#streaming-rows).
- If the loop ends early, at a `break` or an exception, the method reads the rest of the rows and discards them.  On a large result, that takes as long as reading them.  To stop early, have the query return only the rows that the loop needs, with a `WHERE`, `LIMIT` or `TOP`.  See [Streaming rows](../reference/return-types.md#streaming-rows).
- An error at a row, such as a null in a column whose property cannot be null, is thrown when the loop reaches that row.  Your loop has then read the rows before it.  See [Streaming rows](../reference/return-types.md#streaming-rows).
- The `cancellationToken` of the method cancels the loop, and so does a token that you give to `WithCancellation`.  See [Streaming rows](../reference/return-types.md#streaming-rows).
- A stream has no synchronous method.  Under `method-type=both`, the query keeps its stream alone.  See [Which queries get a method](../reference/query-methods.md#which-methods).

## If it does not work

- **`NotSupportedException` from a synchronous method, on PostgreSQL.**  The data source has `Multiplexing=true`.  Give the code that calls the method a data source without it, or call the asynchronous method.  See [what a method reads and throws](../reference/databases/postgresql/query-methods.md#method-results).
- **`NpgsqlOperationInProgressException`, or `InvalidOperationException` on SQL Server, in the body of the loop.**  A command ran on the connection that the loop holds.  Run it on another connection, or read the rows into a collection first.  See [Streaming rows](../reference/return-types.md#streaming-rows).
- **The loop throws an error of the database in place of your own exception.**  An unread row raised the error as the loop ended.  Catch your exception in the body of the loop, keep it in a variable, and `break`.  Then throw the kept exception, after the loop or in place of the database's exception.  See [Streaming rows](../reference/return-types.md#streaming-rows).
- **SQLSRC298.**  The query streams its rows and has `method-type=sync`.  Write `method-type=both`, or a collection type that is read whole, such as `List`.  See [SQLSRC298](../diagnostics.md#sqlsrc298).
