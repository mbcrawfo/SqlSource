# Repositories

<a id="repository-methods"></a>

## Repository methods

```sql postgres
-- name: GetUser -> one-optional
-- summary: Loads one user by id.
SELECT id, name
FROM users
WHERE id = @id;
```

```csharp
// Your code
[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource);

// Generated, shown without global::
namespace App
{
    partial class UserRepository : IUserRepository
    {
        public async Task<GetUserDto?> GetUser(
            GetUserParams parameters,
            CancellationToken cancellationToken = default)
    }

    public partial interface IUserRepository
    {
        Task<GetUserDto?> GetUser(
            GetUserParams parameters,
            CancellationToken cancellationToken = default);
    }
}

// Your code
var users = new UserRepository(dataSource);
GetUserDto? user = await users.GetUser(new GetUserParams(42));
```

A repository is a type marked `[SqlSourceGenerate]` whose queries have the [output](settings.md#setting-output) `repository`, the default output.  Each of these queries gets a repository method: an instance method of the type that claims the query.  A repository method gets its own connection from a member of the type, its [connection source](repository-connections.md#repository-connection), such as a connection string or a data source.  So the code that calls the method passes no connection, and needs none.

In the sample, a `public` type `App.UserRepository` claims `UserRepository.sql`.  The method `GetUser` gets a new connection from the data source `dataSource`.  It sends each parameter, runs the query, and reads the row into the output model.  It disposes the connection before it returns or throws.

- **A repository also gets [an interface](#repository-interface) of its public repository methods**, here `IUserRepository`.  The code that uses the repository can take the interface, and a test can give it a fake.
- **By default, a repository method is asynchronous.**  Under the generator parameter `method-type`, the query gets a synchronous method in place of the asynchronous one, or beside it: see [synchronous repository methods](#synchronous-repository-methods).

<a id="repository-signature"></a>

## The signature

```csharp
public async Task<ListFromDto[]> ListFrom(
    string table,
    string orderBy,
    CancellationToken cancellationToken = default)
```

A repository method has the name of the query.  Under the [generator parameter](generator-parameters.md#setting-generator-parameters) `async-method-suffix`, the name has `Async` after it: `ListFromAsync`.  Under `method-type=both`, the name of the asynchronous method always has `Async` after it.  A [synchronous method](#synchronous-repository-methods) has the name of the query alone.  The method takes these arguments, in this order:

| Argument | What it is |
|----|----|
| `parameters` | The query's input model, when it has parameters.  It is not in the method of a query without parameters |
| A `string` for each [token](tokens.md#tokens) | The text that replaces the token, named as the token, in the order the tokens first appear, checked as [a method that takes a connection](connection-methods.md#method-signature) checks it |
| `cancellationToken` | Cancels getting and opening the connection, running the query and reading its rows.  A synchronous method has none, so one of a query without parameters or tokens takes no argument: `CountUsers()` |

- **It takes no connection, no transaction and no command timeout.**  See [transactions and timeouts](#repository-transactions).
- **It returns what [its shape](return-types.md#method-shapes) says**, as any query method does.
- **It throws as [a method that takes a connection](connection-methods.md#method-signature) throws**, except for the checks of the connection and the transaction, which it does not take.  It throws `ArgumentNullException` for a null `parameters`.  For a list parameter, it throws `ArgumentException` and `InvalidOperationException`.
- **It is an instance method of the type, `public` by default, whatever the accessibility of the type.**  A public method of an internal type is internal in effect.
- **[`visibility`](member-placement.md#setting-visibility) gives the method another accessibility.**  An example is `protected`, for the methods of an abstract base repository.  Its derived types then expose the methods as they choose.
- **A method that is not `public` is left out of the interface.**
- **On a type that is `protected` or `protected internal`, or inside one, a method that takes or returns a model cannot be `public`.**  Its models are internal.  Such a method is [SQLSRC123](../diagnostics.md#sqlsrc123) until you write `-- visibility: internal` on it.
- **It is always on the type.**  The [member location](member-placement.md#setting-member-location) `class` fits.  `extension-class` or `nested-class` is [SQLSRC122](../diagnostics.md#sqlsrc122).
- **Its name cannot be taken by something else of the type** ([SQLSRC274](../diagnostics.md#sqlsrc274)).  One of these takes the name:
  - a member of the type, the type itself, or one of its type parameters;
  - a constant or token method that another query puts on the type;
  - a nested class of the queries of the type;
  - the member of a base type, or the primary-constructor parameter, that gives the method its connection;
  - for a public method, the name of the interface of the type.
- **A [synchronous method](generator-parameters.md#setting-generator-parameters) that takes no argument cannot be named `Finalize`** ([SQLSRC274](../diagnostics.md#sqlsrc274)).  The compiler would take it for the finalizer of the type.
- **A token cannot be named `parameters` or `cancellationToken`, or as any other argument that a query method can have** ([SQLSRC275](../diagnostics.md#sqlsrc275)).  A token also cannot have the name of the primary-constructor parameter that gives the connection.  Nor can it have the name of the repository, when the repository is not generic and a `static` member gives the connection.
- **The repository methods of a type are in the file of its methods**, `App.UserRepository-Methods.g.cs`, with its other query methods and its interface.

[Where a repository gets its connection](repository-connections.md) is a page of its own.

<a id="repository-interface"></a>

<a id="repository-interfaces"></a>

## The interface

```xml
<PropertyGroup>
    <SqlSourceRepositoryInterfaces>false</SqlSourceRepositoryInterfaces>
</PropertyGroup>
```

```csharp
[SqlSourceGenerate(GenerateInterface = true, InterfaceName = "IUserStore")]
public partial class UserRepository(NpgsqlDataSource dataSource);
```

```csharp
// Your code
public sealed class Greeter(IUserRepository users)
{
    public async Task<string> Greet(int id) =>
        (await users.GetUser(new GetUserParams(id)))?.Name ?? "stranger";
}

// Your code, adding a member of your own to the generated interface
public partial interface IUserRepository
{
    Task<int> CountActive(CancellationToken cancellationToken = default);
}
```

A type that claims a query whose output is `repository` gets a `partial interface` of its public repository methods, which the type implements.  Code that takes the interface can be given a fake of it in a test, with no database.  In the first two samples, the project turns the interfaces off, and `UserRepository` turns its own back on, named `IUserStore`.  The last sample is for a repository with the default name: `Greeter` takes the interface `IUserRepository`, and your own `partial interface` adds a member to it.

- **Its members are the public repository methods of the type**, with their signatures and documentation.  Under `method-type=both`, it has the synchronous method of a query and its asynchronous one.
- **Only public repository methods are in it.**  A method of another output, a method that is not `public`, such as an `internal` or a `protected` one, and a method with an error are not in it.
- **It is generated while the type claims a repository query, even when every repository method has an error.**  So code that names the interface reports only those errors.
- **It is beside the type**, in its namespace, or inside the type that the type is nested in.  It has the type parameters of the type.
- **It is `public` when the type and every type around it are `public`.**  Otherwise it is `internal`.
- **It is `partial`.**  A `partial interface` of your own with the same name adds members, which your own part of the repository implements.

Three settings turn the interface on or off, and name it.  Each has one place:

| Written as | Sets | Default |
|----|----|----|
| `SqlSourceRepositoryInterfaces`, a property of the project | Whether the project's repositories get their interfaces.  `false`, matched ignoring case, turns them off; any other value, and none, leaves them on | On |
| `GenerateInterface` on `[SqlSourceGenerate]` | Whether this type gets its interface, over the project's property | The project's property |
| `InterfaceName` on `[SqlSourceGenerate]` | The interface's name: a C# identifier, without a namespace or type parameters | `I` and the type's name |

By default, every repository gets its interface, named `I` and the name of the type: `IUserRepository` for `UserRepository`.

- **`SqlSourceRepositoryInterfaces` turns the interfaces of the whole project on or off.**  It is a property of the project file, and not a setting of a settings file.  Why: the settings of a `.sql` file reach its queries, and one type can claim the files of several folders.
- **`GenerateInterface` and `InterfaceName` are set on the `[SqlSourceGenerate]` of the type.**  The two properties of the attribute cannot be written in a settings file or a marker: they belong to the type.
- **`GenerateInterface` wins over the property of the project.**  `GenerateInterface = false` turns the interface of one type off in a project that leaves them on.
- **A type with no repository query gets no interface**, whatever these settings say.
- **A name that something beside the type already has is [SQLSRC035](../diagnostics.md#sqlsrc035).**
- **A name that is not a C# identifier is [SQLSRC037](../diagnostics.md#sqlsrc037).**
- **Each of these two errors leaves the interface out, and keeps the methods.**

<a id="repository-transactions"></a>

## Transactions and timeouts

A repository method takes no transaction and no command timeout.  It runs on the connection that its source gives, under the default timeout of the driver, and gives its command no transaction.  It can still run in a transaction of your code's in two cases:

- **On PostgreSQL, on a connection on which your code has begun a transaction, Npgsql runs the command in that transaction.**  This holds for a borrowed connection.  It holds for an open connection that a factory returns too, but the method then disposes that connection, which rolls your transaction back.  See [the connection](repository-connections.md#repository-connection).
- **Inside a `TransactionScope`, a connection opened inside the scope takes part in the ambient transaction**, unless its connection string turns enlisting off.  This holds whether the method, a factory or your code opened the connection.  A connection opened before the scope does not, unless your code enlists it with `EnlistTransaction`.  On PostgreSQL, a data source with `Multiplexing=true` cannot take part: Npgsql throws when the connection is opened.

If a query needs to run in a transaction with other queries, or needs its own timeout, give it the output `methods` instead.  Put the query in a file of its own with that output, or write `-- output: methods` on the query.  Your own code in the repository then opens a connection, begins the transaction, and calls the [method that takes a connection](connection-methods.md#methods-that-take-a-connection) of the query.  If your code disposes the transaction without committing it, the driver rolls it back.  See [the recipe](../start/transactions.md#queries-in-a-transaction).

A [collection method](collection-methods.md#collection-methods) of a repository begins a transaction of its own, by the generator parameter `transaction`.  It commits the transaction before it returns.  So its items are added all together, or not at all.

If an ambient transaction is open, such as the transaction of a `TransactionScope`, the collection method begins no transaction.  It runs in the ambient transaction instead.  On a connection that is not in the ambient transaction, such as a borrowed connection opened before the scope, it runs in no transaction at all, as under `transaction=off`.  See [transactions](collection-methods.md#collection-transactions).

<a id="synchronous-repository-methods"></a>

## Synchronous repository methods

```sql postgres
-- name: GetUser -> one-optional
-- generator: method-type=both
SELECT id, name
FROM users
WHERE id = @id;
```

```csharp
// Your code
[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource);

// Generated, shown without global::
partial class UserRepository : IUserRepository
{
    public GetUserDto? GetUser(
        GetUserParams parameters)

    public async Task<GetUserDto?> GetUserAsync(
        GetUserParams parameters,
        CancellationToken cancellationToken = default)
}

public partial interface IUserRepository
{
    GetUserDto? GetUser(
        GetUserParams parameters);

    Task<GetUserDto?> GetUserAsync(
        GetUserParams parameters,
        CancellationToken cancellationToken = default);
}

// Your code, where it does not await
GetUserDto? user = users.GetUser(new GetUserParams(42));
```

A [repository method](#repository-methods) follows the generator parameter `method-type` as a method that takes a connection does.  The query gets these methods:

- under `async`, the default, the asynchronous method alone;
- under `sync`, a synchronous method in place of the asynchronous one;
- under `both`, the two, as `GetUser` in the sample has.

The [collection method](collection-methods.md#collection-methods) of the query follows `method-type` in the same way.

- **Its name is the name of the query.**  Under `both`, the name of the asynchronous method always has `Async` after it, as in the sample.
- **Its arguments are the input model and a `string` for each token**, as [the signature](#repository-signature) says, without `cancellationToken`.  A query without parameters or tokens gets a method that takes nothing, `CountUsers()`.
- **It returns what the task of the asynchronous method holds.**  For `none`, it returns nothing, `void`.  It throws the same exceptions.
- **The interface has a member for each public method.**  So under `both` it has both methods, and under `sync` the synchronous one alone.
- **It gets its connection from the repository**, as [the connection](repository-connections.md#repository-connection) says, with the synchronous calls of the driver.  By the kind of member, the connection is one of these:
  - a new connection of a connection string, opened;
  - a connection that a data source opens, `OpenConnection`;
  - the connection that a factory returns, opened when it is closed, and disposed, with `default` for a factory that takes a `CancellationToken`;
  - a borrowed connection, opened when it is closed, and closed again.
- **A connection string, a data source, a connection and a factory that returns a `DbConnection` serve both kinds of method.**
- **A factory that returns a `Task` or `ValueTask` serves asynchronous methods alone.**
- **If such a factory is the only member that gives the database of the query its connection, a query that would get a synchronous method is [SQLSRC299](../diagnostics.md#sqlsrc299).**  It is an error at the name of the query.
- **Under `both`, SQLSRC299 costs the query only its synchronous methods.**  The query keeps its asynchronous method, its member of the interface and its asynchronous collection method.  Under `sync`, the query gets no method.
- **To fix SQLSRC299, mark a second member that returns no task beside the factory.**  The two are then a [pair](repository-connections.md#repository-connection), and the synchronous methods use the second member.  Or give the query `method-type=async`.
- **A stream has no synchronous method**, as for [a query method](synchronous-methods.md#synchronous-methods).  Under `sync`, a stream is [SQLSRC298](../diagnostics.md#sqlsrc298).  Under `both`, it keeps its asynchronous method alone, with both collection methods when it has one.
- **Under any `method-type`, a token cannot be named `cancellationToken`** ([SQLSRC275](../diagnostics.md#sqlsrc275)).  A primary-constructor parameter that gives the connection cannot have that name either, under any `method-type` ([SQLSRC036](../diagnostics.md#sqlsrc036)).
- **On PostgreSQL, a data source with `Multiplexing=true` refuses a synchronous repository method**, with `NotSupportedException`: see [what a method reads and throws](databases/postgresql/query-methods.md#method-results).
- **On SQL Server, Microsoft.Data.SqlClient refuses the command of a synchronous method on a borrowed connection with a pending transaction**, with `InvalidOperationException`, as it refuses that of the asynchronous method.
