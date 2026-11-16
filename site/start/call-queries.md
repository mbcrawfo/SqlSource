# Call your queries

At the end of this page, your application gets the repository by dependency injection, calls its methods with a cancellation token, and reads their models (DTOs).

<a id="repository"></a>

## The repository and its connection

You pass no connection to a method of the repository.  Each method opens a connection, runs its query and disposes the connection.  It gets the connection from the member of the class:

# [PostgreSQL](#tab/postgresql)

```csharp
[SqlSourceGenerate]
public sealed partial class UserRepository(NpgsqlDataSource dataSource);
```

Each call opens a connection from the data source.

# [SQL Server](#tab/sqlserver)

```csharp
[SqlSourceGenerate]
public sealed partial class UserRepository(string connectionString);
```

Each call makes a `SqlConnection` from the connection string, and opens it.

---

<a id="register"></a>

## Register it

Register the repository as its interface, `IUserRepository`, which the build generated.  In `Program.cs` of an ASP.NET Core application:

# [PostgreSQL](#tab/postgresql)

```csharp
builder.Services.AddSingleton(NpgsqlDataSource.Create(builder.Configuration.GetConnectionString("App")!));
builder.Services.AddSingleton<IUserRepository, UserRepository>();
```

# [SQL Server](#tab/sqlserver)

```csharp
builder.Services.AddSingleton<IUserRepository>(
    new UserRepository(builder.Configuration.GetConnectionString("App")!));
```

---

A class of your application then takes `IUserRepository` in its constructor.  A test can give that class a fake, as [Test code that uses a repository](testing.md) shows.

<a id="models"></a>

## The models

For each query, the build generates a record for its query parameters and a record for its rows.  For `GetUser`, they are:

```csharp
public sealed record GetUserParams(int UserId);
public sealed record GetUserDto(int Id, string Name, string Email);
```

- The parameters model has one property for each query parameter.  A query with no parameters, such as `ListUserNames`, has none.
- The row model has one property for each column.  A property is nullable when its column can be null.
- A query that reads every column of a table, in the table's order, gets a row model named after the table, such as `UsersDto`.

<a id="what-a-method-returns"></a>

## What each method returns

The result shape of each query decides what its method returns:

```csharp
GetUserDto? user = await users.GetUser(new GetUserParams(1));
ListUserNamesDto[] names = await users.ListUserNames();
int renamed = await users.RenameUser(new RenameUserParams("Ada Lovelace", 1));
```

- `GetUser`, `-> one-optional`: the row, or null.
- `ListUserNames`, with no shape: every row, in an array.
- `RenameUser`, `-> rowcount`: the number of rows changed.

<a id="cancellation"></a>

## Cancel a call

Each method takes a `CancellationToken` as its last argument.  It cancels opening the connection, running the query and reading the rows.  Pass on the token that your code gets:

```csharp
app.MapGet("/users/{id}", async (int id, IUserRepository users, CancellationToken cancellationToken) =>
    await users.GetUser(new GetUserParams(id), cancellationToken));
```

<a id="missing-row"></a>

## When a row is missing

`GetUser` is `one-optional`, so it returns null when no user has the id.  Test for null:

```csharp
GetUserDto? user = await users.GetUser(new GetUserParams(42));
if (user is null)
{
    // No user has the id 42.
}
```

A query whose shape is `one` throws `UnexpectedRowCountException` when it returns no row.  Both `one` and `one-optional` throw it when the query returns more than one row.

## If it does not work

- `GetUserDto` is not found: your table has only the query's columns, in its order, so the model is `UsersDto`.
- A method is missing, and the build reports [SQLSRC241](../diagnostics.md#sqlsrc241) or [SQLSRC244](../diagnostics.md#sqlsrc244): describe the query, as [Your first query](first-query.md#describe) shows.

## What you have now

An application that gets `IUserRepository` from dependency injection, calls its methods, and reads their models.

Rules: [repository methods](../reference/repositories.md#repository-methods), [the connection](../reference/repository-connections.md#repository-connection), [the interface](../reference/repositories.md#repository-interface), [model names](../reference/models.md#model-names), [table models](../reference/shared-models.md#table-models), [what each shape returns](../reference/return-types.md#method-shapes), [the signature](../reference/repositories.md#repository-signature), [`UnexpectedRowCountException`](../reference/exceptions.md#unexpected-row-count).  Guides: [names with `Async`](../guides/naming-and-placement.md), [methods for code that does not await](../guides/sync-and-streaming.md), [another way to give a repository its connection](../guides/repository-connection.md).

Next: [Run queries in a transaction](transactions.md)
