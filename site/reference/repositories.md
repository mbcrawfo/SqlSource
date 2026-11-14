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

A query whose [output](../settings.md#setting-output) is `repository`, the default, gets an instance method of the type that claims it, a repository.  Above, a `public` type `App.UserRepository` claims `UserRepository.sql`.  The method gets a connection from a member of the repository, here a new one from the data source `dataSource`, sends each parameter, runs the query, reads its row into the output model, and disposes the connection before it returns or throws.  So the code that calls it passes no connection, and needs none.  The repository also gets [an interface](#repository-interface) of its public repository methods, `IUserRepository`, for the code that uses it.  The method is asynchronous by default, and the generator parameter `method-type` gives the query a synchronous method instead or beside it, as [synchronous repository methods](#synchronous-repository-methods) say.

<a id="repository-signature"></a>

## The signature

```csharp
public async Task<ListFromDto[]> ListFrom(
    string table,
    string orderBy,
    CancellationToken cancellationToken = default)
```

The method has the name of the query, with `Async` after it under the [generator parameter](../settings.md#setting-generator-parameters) `async-method-suffix`, `ListFromAsync`, and always under `method-type=both`.  A [synchronous method](#synchronous-repository-methods) has the query's name alone.  Its arguments, in this order:

| Argument | What it is |
|----|----|
| `parameters` | The query's input model, when it has parameters.  It is not in the method of a query without parameters |
| A `string` for each [token](tokens.md#tokens) | The text that replaces the token, named as the token, in the order the tokens first appear, checked as [a method that takes a connection](connection-methods.md#method-signature) checks it |
| `cancellationToken` | Cancels getting and opening the connection, running the query and reading its rows.  A synchronous method has none, so one of a query without parameters or tokens takes no argument: `CountUsers()` |

It takes no connection, no transaction and no command timeout: see [transactions and timeouts](#repository-transactions).  It returns what [its shape](return-types.md#method-shapes) says, as any query method does, and throws as [a method that takes a connection](connection-methods.md#method-signature) throws, but for the checks of the connection and the transaction it does not take: `ArgumentNullException` for a null `parameters`, and for a list parameter `ArgumentException` and `InvalidOperationException`.

- **It is an instance method of the type, `public` by default**, whatever the accessibility of the type: a public method of an internal type is internal in effect.  On a type that is `protected` or `protected internal`, or inside one, a method that takes or returns a model cannot be `public`, since its models are internal, and is [SQLSRC123](../diagnostics.md#sqlsrc123) until you write `-- visibility: internal` on it.  [`visibility`](members.md#setting-visibility) gives it another, such as `protected` for the methods of an abstract base repository, which its derived types expose as they choose.  A method that is not `public` is left out of the interface.
- **It is always on the type.**  A [member location](members.md#setting-member-location) of `class` fits, and `extension-class` or `nested-class` is [SQLSRC122](../diagnostics.md#sqlsrc122).
- **Its name is not taken** by a member of the type, the type itself or one of its type parameters, a constant or token method that another query puts on the type, a nested class of the type's queries, or, for a public method, the name of the type's interface, nor is a [synchronous method](../settings.md#setting-generator-parameters) that takes no argument named `Finalize`, which would be taken for the type's finalizer ([SQLSRC274](../diagnostics.md#sqlsrc274)).  A token is not named `parameters` or `cancellationToken`, nor as any other argument a query method can have ([SQLSRC275](../diagnostics.md#sqlsrc275)).
- A type's repository methods are in the file of its methods, `App.UserRepository-Methods.g.cs`, with its other query methods and its interface.

<a id="repository-connection"></a>

## The connection

```csharp
[SqlSourceGenerate]
public partial class OrderRepository(string connectionString);

[SqlSourceGenerate]
public partial class AuditRepository(IConnectionFactory factory)
{
    [SqlSourceConnection]
    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        factory.OpenAsync("audit", cancellationToken);
}
```

A repository method gets its connection from one member of the type, its connection source, which the build finds by its name or type, or which you mark `[SqlSourceConnection]`.  `OrderRepository` above has a connection string, found by its name, and `AuditRepository` a method of its own that the attribute marks.  What the method does with the connection depends on what the member gives:

| The member | Found | The method's connection |
|----|----|----|
| A `string` field, property or primary-constructor parameter named `connectionString`, `_connectionString` or `ConnectionString`, or any `string` member or method that is marked | By its name and type | A new connection of the string, an `NpgsqlConnection` or a `Microsoft.Data.SqlClient.SqlConnection`, which the method opens and disposes |
| A field, property or primary-constructor parameter of `DbDataSource` or a type derived from it, such as `NpgsqlDataSource` | By its type | A new connection that the data source opens, `OpenConnectionAsync`, or `OpenConnection` for a synchronous method, which the method disposes |
| A method that returns a `DbConnection` or a type derived from it, or a `Task` or `ValueTask` of one, and that takes nothing or one `CancellationToken`, which gets the method's, or `default` from a synchronous method | When marked | The connection it returns, called for each call of the method, opened when it is closed, and disposed.  One that returns a `Task` or `ValueTask` serves asynchronous methods alone |
| A field, property or primary-constructor parameter of `DbConnection` or a type derived from it | When marked | Borrowed: opened when it is closed and closed again before the method returns, never disposed |

The rule to remember: **a method is a factory, and a field, a property or a parameter is held.**  What a method returns is the repository method's, which disposes it; a connection that the repository holds is borrowed, and left as the method found it.  A connection string and a data source make a new connection for each call, which the method disposes.  A [synchronous method](#synchronous-repository-methods) gets its connection in the same way, with the driver's synchronous calls, from any member but a method that returns a task, which it cannot call: give it one of the others, or a pair, below.

- **Found by its name or type.**  With no member marked, the type's one source is its one `string` named as above, or its one member of a data source type, among its own fields and properties, those of its base types that it can reach, static ones too, and its primary constructor's parameters.  A connection is never found by its type, since only you know whether the repository owns it: mark it.  None is [SQLSRC028](../diagnostics.md#sqlsrc028), and two or more [SQLSRC029](../diagnostics.md#sqlsrc029), which lists them.
- **Fields and properties before parameters.**  A primary-constructor parameter is found only when no field or property is.  So `partial class UserRepository(NpgsqlDataSource dataSource) { private readonly NpgsqlDataSource _dataSource = dataSource; }` uses `_dataSource`, a record's positional parameter yields to its property, and `UserRepository(string connectionString) : RepositoryBase(connectionString)` uses the base type's member when it can reach one.  A parameter that the type passes to its base type, or uses to initialize a field or property, is found only when you mark it, since the methods would keep a second copy of it, which the compiler warns of (CS9107, CS9124).
- **`[SqlSourceConnection]` marks the source**, on a field, a property, a method or a primary-constructor parameter, of any of the four kinds above.  Once one member is marked, only marked members count, and none is found by its name or type.  An override of a marked member is marked as the member it overrides.  A marked member that cannot give a connection, such as a method with other parameters, is [SQLSRC032](../diagnostics.md#sqlsrc032).
- **A pair, for asynchronous and synchronous methods.**  A database can have two marked members when exactly one of them is a method that returns a `Task` or `ValueTask` of a connection: its asynchronous methods get their connection from that one, and its synchronous methods from the other, which can be any of the four kinds.  Two that both return a task, two that return none, or three or more are [SQLSRC029](../diagnostics.md#sqlsrc029).  Each of the two is checked as one source is, and a problem of either costs the database its methods.  A pair whose other member no synchronous method uses, as when every query is asynchronous, is not an error.

  ```csharp
  [SqlSourceGenerate]
  public partial class AuditRepository(IConnectionFactory factory)
  {
      [SqlSourceConnection]
      private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
          factory.OpenAsync("audit", cancellationToken);

      [SqlSourceConnection]
      private DbConnection Open() => factory.Open("audit");
  }
  ```

- **Several databases.**  A repository whose queries use more than one [logical database](../settings.md#setting-database) marks one member for each, or a pair, with the database's name, which is compared ignoring case.  Nothing is found by its name or type then, since nothing says which database a member is for.

  ```csharp
  [SqlSourceGenerate(Source = ".")]
  public partial class Reports(
      [SqlSourceConnection(Database = "main")] NpgsqlDataSource main,
      [SqlSourceConnection(Database = "reports")] NpgsqlDataSource reports);
  ```

  A mark without `Database` on such a type is [SQLSRC030](../diagnostics.md#sqlsrc030), and one for a database that no repository query of the type uses is the warning [SQLSRC031](../diagnostics.md#sqlsrc031).  A query whose output is not `repository` needs no source.
- **The source is of its database's engine**: an Npgsql type for a SQL Server database, or a Microsoft.Data.SqlClient type for a PostgreSQL one, is [SQLSRC033](../diagnostics.md#sqlsrc033).  Microsoft.Data.SqlClient has no data source, so a SQL Server repository takes a connection string, a factory method, or a connection it borrows.
- **A primary-constructor parameter named `cancellationToken`** is hidden by every asynchronous method's argument of that name, and **one named `parameters`** by the input model of a method whose query has parameters, when a repository query of its database has some, [SQLSRC036](../diagnostics.md#sqlsrc036): rename it.  One named `cancellationToken` is refused under any `method-type`, though a synchronous method takes no such argument.
- **A null source throws.**  A connection string, a data source or a connection that is null when the method is called, or a factory that returns null, throws `InvalidOperationException`, "The connection source 'dataSource' of 'App.UserRepository' is null.", before anything is sent.
- **A method that [streams its rows](return-types.md#streaming-rows)** gets its connection when the loop asks for the first row, and disposes it, or closes a borrowed one that it opened, when the loop ends, so the connection is held for the whole loop.
- **A borrowed connection runs the command in no transaction.**  A repository method gives its command no transaction, so on SQL Server, Microsoft.Data.SqlClient refuses it on a connection that has a pending transaction, with `InvalidOperationException`, from a synchronous method as from an asynchronous one; Npgsql runs it in the connection's transaction.  To run queries in a transaction, give them the output `methods`: see [transactions and timeouts](#repository-transactions).
- **A PostgreSQL connection string makes a bare connection**, which knows none of the plugins and mappings of your data source: see [limits](databases/postgresql/query-methods.md#limit-connection-string-repository).  Give a PostgreSQL repository a data source.

The attribute is `SqlSource.SqlSourceConnectionAttribute`, generated into your project beside `[SqlSourceGenerate]`.  Its one property, `Database`, names the [logical database](../settings.md#setting-database) as the `database` setting does.

<a id="repository-interface"></a>

## The interface

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

A type that claims a query whose output is `repository` gets a `partial interface`, named `I` and the type's name, `IUserRepository` for `UserRepository`, which the type implements.  Code that takes the interface can be given a fake of it in a test, with no database.

- **Its members are the type's public repository methods**, with their signatures and documentation: under `method-type=both`, a query's synchronous method and its asynchronous one.  A method of another output, a method that is not `public`, and a method with an error are not in it.  It is generated while the type claims a repository query, even when every repository method has an error, so that code that names it reports only those errors.
- **It is beside the type**, in its namespace, or inside the type it is nested in, with the type's type parameters.  It is `public` when the type and every type around it are, and `internal` otherwise.
- **It is `partial`**, so a `partial interface` of your own of the same name adds members, which your own part of the repository implements.
- **Its name** is [`InterfaceName`](#repository-interfaces) on the type's `[SqlSourceGenerate]` when it is given.  A name that something beside the type already has is [SQLSRC035](../diagnostics.md#sqlsrc035), and one that is not a C# identifier [SQLSRC037](../diagnostics.md#sqlsrc037).  Both leave the interface out, and the methods in.
- **Turning it off**: `GenerateInterface = false` on the type's `[SqlSourceGenerate]`, or `SqlSourceRepositoryInterfaces` for the project, as [repository interfaces](#repository-interfaces) says.

<a id="repository-interfaces"></a>

### Repository interfaces

```xml
<PropertyGroup>
    <SqlSourceRepositoryInterfaces>false</SqlSourceRepositoryInterfaces>
</PropertyGroup>
```

```csharp
[SqlSourceGenerate(GenerateInterface = true, InterfaceName = "IUserStore")]
public partial class UserRepository(NpgsqlDataSource dataSource);
```

A type that claims a query whose output is `repository` gets an [interface](#repository-interface) of its public repository methods, `IUserRepository` for `UserRepository`, unless one of these turns it off:

| Written as | Sets | Default |
|----|----|----|
| `SqlSourceRepositoryInterfaces`, a property of the project | Whether the project's repositories get their interfaces.  `false`, matched ignoring case, turns them off; any other value, and none, leaves them on | On |
| `GenerateInterface` on `[SqlSourceGenerate]` | Whether this type gets its interface, over the project's property | The project's property |
| `InterfaceName` on `[SqlSourceGenerate]` | The interface's name: a C# identifier, without a namespace or type parameters | `I` and the type's name |

Above, the project turns the interfaces off, and `UserRepository` turns its own back on, named `IUserStore`.  `GenerateInterface = false` turns one type's off in a project that leaves them on.

- **It is a property of the project file**, and not a setting of a settings file: the settings of a `.sql` file reach its queries, and one type can claim the files of several folders.  The two properties of the attribute cannot be written in a settings file or a marker: they belong to the type.
- **A name that is taken** by something beside the type is [SQLSRC035](../diagnostics.md#sqlsrc035), and a name that is not a C# identifier is [SQLSRC037](../diagnostics.md#sqlsrc037): the type gets no interface, and keeps its methods.
- **A type with no repository query** gets no interface, whatever these say.

<a id="repository-transactions"></a>

## Transactions and timeouts

A repository method takes no transaction and no command timeout.  It runs on the connection that its source gives, in no transaction of yours, under the driver's default timeout.  A query that must run in a transaction with others, or that needs its own timeout, takes the output `methods` instead, in its own file or with `-- output: methods` on the query, and your own code in the repository opens a connection, begins the transaction and calls its [method that takes a connection](connection-methods.md#methods-that-take-a-connection).  See [the recipe](../recipes.md#queries-in-a-transaction).

A repository's [collection method](collection-methods.md#collection-methods) begins a transaction of its own, by the generator parameter `transaction`, and commits it before it returns, so that its items are added all together or not at all.  It begins none when an ambient transaction, such as a `TransactionScope`'s, is open, and runs in that one instead, or, on a connection that is not in it, such as a borrowed connection opened before the scope, in no transaction at all, as under `transaction=off`: see [transactions](collection-methods.md#collection-transactions).

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

A [repository method](#repository-methods) follows `method-type` as a method that takes a connection does: under `sync` the query gets a synchronous method in place of its asynchronous one, under `both` the two, as `GetUser` above does, and under `async`, the default, the asynchronous one alone.  Its [collection method](collection-methods.md#collection-methods) follows it in the same way.

- **Its name is the query's**, and under `both` the asynchronous method's always has `Async` after it, as above.
- **Its arguments are the input model and a `string` for each token**, as [the signature](#repository-signature) says, without `cancellationToken`.  A query without parameters or tokens gets a method that takes nothing, `CountUsers()`.
- **It returns what the asynchronous method's task holds**, and nothing, `void`, for `none`, and throws the same exceptions.
- **The interface has a member for each public method**, so under `both` it has both, and under `sync` the synchronous one alone.
- **It gets its connection from the repository**, as [the connection](#repository-connection) says, with the driver's synchronous calls: a new connection of a connection string, opened; one that a data source opens, `OpenConnection`; the connection that a factory returns, opened when it is closed and disposed, with `default` for a factory that takes a `CancellationToken`; or a borrowed connection, opened when it is closed and closed again.  A connection string, a data source, a connection and a factory that returns a `DbConnection` serve both kinds of method.
- **A factory that returns a `Task` or `ValueTask` serves asynchronous methods alone.**  When it is the only member that gives the query's database its connection, a query that would get a synchronous method is [SQLSRC299](../diagnostics.md#sqlsrc299), an error at the query's name.  Under `both` the query keeps its asynchronous method, its member of the interface and its asynchronous collection method, and loses only the synchronous ones, and under `sync` it gets no method.  Mark a second member that returns no task beside it, which makes the two a [pair](#repository-connection) whose synchronous methods use that one, or give the query `method-type=async`.
- **A stream has no synchronous method**, as for [a query method](query-methods.md#synchronous-methods): under `sync` it is [SQLSRC298](../diagnostics.md#sqlsrc298), and under `both` it keeps its asynchronous method alone, with both collection methods when it has one.
- **A token cannot be named `cancellationToken`** ([SQLSRC275](../diagnostics.md#sqlsrc275)), and a primary-constructor parameter that gives the connection cannot either ([SQLSRC036](../diagnostics.md#sqlsrc036)), under any `method-type`.
- **On PostgreSQL**, a data source with `Multiplexing=true` refuses a synchronous repository method, with `NotSupportedException`: see [what a method reads and throws](databases/postgresql/query-methods.md#method-results).  On SQL Server, a borrowed connection with a pending transaction is refused with `InvalidOperationException`, as the asynchronous method refuses it.
