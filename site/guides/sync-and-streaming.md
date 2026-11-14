# Call a query without await, or stream rows

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

`-- generator: method-type=both` gives `GetUserName` a synchronous method, `GetUserName`, which returns the row or null and takes no cancellation token, beside its asynchronous method, `GetUserNameAsync`.  `method-type=sync` gives it the synchronous method alone.  A query with [`collection-method`](../reference/collection-methods.md#collection-methods) gets its collection method the same way, synchronous, asynchronous or both.  Write it in the file header, or in `generatorParameters` of the folder's `sqlsource.json`, for every query it reaches.  On PostgreSQL, the data source it runs on has no `Multiplexing=true`.

```csharp
// The same query at the default output, repository, without -- output: methods
public int Run(int id)
{
    GetUserNameDto? user = users.GetUserName(new GetUserNameParams(id));
    Console.WriteLine(user?.Name ?? "no such user");
    return user is null ? 1 : 0;
}
```

A repository gets the same methods, and its interface a member for each, so `users` can be the interface.  Each gets its connection from the repository: a repository whose only connection is a method that returns a task marks a second one that returns none, as [the recipe](repository-connection.md#connection-from-a-provider) does.

Rules: [synchronous methods](../reference/query-methods.md#synchronous-methods), [synchronous repository methods](../reference/repositories.md#synchronous-repository-methods), [generator parameters](../reference/generator-parameters.md#setting-generator-parameters).
