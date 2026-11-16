# Synchronous methods

This page says when a query gets a synchronous [query method](query-methods.md), and how that method runs the query.

<a id="synchronous-methods"></a>

## Synchronous methods

```sql postgres
-- output: methods

-- name: GetUser -> one-optional
-- generator: method-type=both
SELECT id, name
FROM users
WHERE id = @id;
```

```csharp
// Generated, shown without global::
internal static GetUserDto? GetUser(
    this DbConnection connection,
    GetUserParams parameters,
    DbTransaction? transaction = null,
    int? commandTimeout = null)

internal static async Task<GetUserDto?> GetUserAsync(
    this DbConnection connection,
    GetUserParams parameters,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)

// Your code, where it does not await
GetUserDto? user = connection.GetUser(new GetUserParams(42));
```

The [generator parameter](generator-parameters.md#setting-generator-parameters) `method-type` gives a query a synchronous method, when the output of the query is `repository` or `methods`.  A synchronous method is for code that does not await, such as a console tool or a library whose API is synchronous.  It runs the query on the calling thread, with the synchronous calls of the driver, and never waits on a task.  `method-type` takes one of these values:

- `async`, the default, gives the asynchronous method alone.
- `sync` gives a synchronous method in place of the asynchronous one.
- `both` gives the two, as `GetUser` in the sample has.

The names follow these rules:

- **A synchronous method has the name of the query, never with `Async` after it**, whatever `async-method-suffix` says.
- **Under `both`, the name of the asynchronous method always has `Async` after it**, with or without `async-method-suffix`.  Two methods that differ only by `cancellationToken`, an argument with a default, would make every call ambiguous.
- **Otherwise, the name of the asynchronous method has `Async` after it only under [`async-method-suffix`](generator-parameters.md#gp-async-method-suffix)**, which is off by default.
- **Each name of a query is checked as [where the methods go](connection-methods.md#method-location) says.**  If something else takes a name, the query is [SQLSRC274](../diagnostics.md#sqlsrc274), and gets none of its methods until you fix it.  For a clash with a method of another type in an extension class, the query gets none of the methods of that name.
- **A synchronous repository method with no argument cannot be named `Finalize`** ([SQLSRC274](../diagnostics.md#sqlsrc274)).  The compiler would take it for the finalizer of the type: see [the signature](repositories.md#repository-signature).
- **A synchronous method takes the arguments of the asynchronous method, without `cancellationToken`.**  A method that takes a connection takes the connection, the input model, a `string` for each [token](tokens.md#tokens), `transaction` and `commandTimeout`, as [the signature](connection-methods.md#method-signature) says.  A [repository method](repositories.md#synchronous-repository-methods) takes the input model and the tokens alone.
- **Nothing cancels a synchronous method**, as the synchronous calls of ADO.NET take no token.  `commandTimeout` still limits the command.
- **A token still cannot be named `cancellationToken`**, under any `method-type` ([SQLSRC275](../diagnostics.md#sqlsrc275)).
- **A synchronous method returns what the task of the asynchronous method holds**, as [what each shape returns](return-types.md#method-shapes) says.  It returns the collection for `many`, the row for `one`, the row or null for `one-optional`, the count for `rowcount`, and nothing, `void`, for `none`.
- **A synchronous method does what the asynchronous method does.**  It does each of these in the same way:
  - It opens a closed connection, and closes it again before it returns or throws, as [the connection](connection-methods.md#method-connection) says.  A repository method gets its connection from the repository instead.
  - It runs in the `transaction` that it is given.
  - It sends each parameter, and reads each row.
  - It throws the same exceptions: [`UnexpectedRowCountException`](exceptions.md#unexpected-row-count), the exceptions of the driver and, on SQL Server, [`TransactionRolledBackException`](exceptions.md#transaction-rolled-back).
- **The documentation of a synchronous method in IntelliSense is that of the asynchronous method**, without `cancellationToken`.  For `none`, it has no line for what the method returns.
- **A synchronous repository method needs a member that gives the connection and returns no task** ([SQLSRC299](../diagnostics.md#sqlsrc299)).  See [synchronous repository methods](repositories.md#synchronous-repository-methods).
- **On PostgreSQL, a data source with `Multiplexing=true` refuses a synchronous method on a connection that has no transaction open.**  An example is the connection that a repository method gets from its data source.  See [what a method reads and throws](databases/postgresql/query-methods.md#method-results).
- **A stream has no synchronous method.**  A `many` query whose [collection type](return-types.md#setting-collection-type) is `IAsyncEnumerable` gives each row to the loop of the caller as it is read.  Only an asynchronous method does that.
- **Under `both`, a stream gets its asynchronous method alone, named with `Async`.**  If it has a [collection method](collection-methods.md#collection-methods), it gets both collection methods, as a collection method reads its rows into an array and never streams them.  For a query `ListUsers`, they are `ListUsers(IEnumerable<ListUsersParams> parameters, ...)` and `ListUsersAsync(IEnumerable<ListUsersParams> parameters, ...)`.
- **Under `sync`, a stream is [SQLSRC298](../diagnostics.md#sqlsrc298)**, and gets no method and no collection method.  For a synchronous method, give the query a collection type that is read whole, such as `List`.

The [collection method](collection-methods.md#collection-methods) of a query follows `method-type` as the query method does.  Under `sync`, the collection method is synchronous, and has the name of the query.  It is an overload of the synchronous method, by its `IEnumerable` argument.  Under `both`, the query gets the two collection methods.  The synchronous one has the name of the query, and the asynchronous one has `Async` after it:

```csharp
// -- name: GetUser -> one-optional, with -- generator: collection-method method-type=both,
// generated beside GetUser and GetUserAsync, shown without global::
internal static GetUserDto[] GetUser(
    this DbConnection connection,
    IEnumerable<GetUserParams> parameters,
    DbTransaction? transaction = null,
    int? commandTimeout = null)

internal static async Task<GetUserDto[]> GetUserAsync(
    this DbConnection connection,
    IEnumerable<GetUserParams> parameters,
    DbTransaction? transaction = null,
    int? commandTimeout = null,
    CancellationToken cancellationToken = default)
```

- **A synchronous collection method takes the arguments of the asynchronous one, without `cancellationToken`.**
- **A synchronous collection method returns what the task of the asynchronous one holds**: the collection, the count, or nothing, `void`, for `none`, as [what it returns](collection-methods.md#collection-methods) says.
- **A synchronous collection method does what the asynchronous one does.**  It does each of these in the same way:
  - It reads the collection once, and runs the items 1,000 at a time.
  - It begins a transaction of its own, unless it is given one or one is ambient, by the generator parameter [`transaction`](collection-methods.md#collection-transactions).
  - It throws the same exceptions, as [what it throws](collection-methods.md#collection-failures) says:
    - [`CollectionItemException`](exceptions.md#collection-item-exception), with the `ItemIndex` of the item;
    - `UnexpectedRowCountException`;
    - on SQL Server, `TransactionRolledBackException`, and the `InvalidOperationException` of a query that returns more than one result set.
- **The name of a synchronous collection method, the name of the query, is checked as the name of a method is**, by [where the methods go](connection-methods.md#method-location).  If something else takes it, the query is [SQLSRC274](../diagnostics.md#sqlsrc274).  This holds for the collection method of a stream too, which has no synchronous method of that name.
