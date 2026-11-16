# Give a repository its connection

Give a repository its connection string, data source or connection factory, from dependency injection or a service of your own.

**Use this when** a repository must get its connection from a member that the build does not find by itself, such as a service of yours.  Use it too to make a SQL Server repository from a connection string.

<a id="connection-from-a-provider"></a>

## Give a repository its connection from a service of your own

```csharp
public interface IConnectionStrings
{
    string Get(string name);
}

[SqlSourceGenerate]
public partial class UserRepository(IConnectionStrings connectionStrings)
{
    [SqlSourceConnection]
    private string AppDatabase => connectionStrings.Get("app");
}
```

```csharp
public interface IConnectionFactory
{
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);

    DbConnection Open();
}

[SqlSourceGenerate]
public partial class OrderRepository(IConnectionFactory connections)
{
    [SqlSourceConnection]
    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        connections.OpenAsync(cancellationToken);

    // For the synchronous methods of method-type=sync or both
    [SqlSourceConnection]
    private DbConnection Open() => connections.Open();
}
```

**What you get.**  The property of `UserRepository` gives a connection string.  Each method of the repository makes a connection from it, and disposes it.  The methods of `OrderRepository` are factories.  Each repository method calls one: `OpenAsync` for an asynchronous method, and `Open` for a [synchronous one](../reference/repositories.md#synchronous-repository-methods).  It opens the connection that the factory returns when it is closed, and disposes it.

**The rules you need.**

- **Mark `[SqlSourceConnection]` on a member that the build does not find by its name or type.**  See [the connection](../reference/repository-connections.md#repository-connection) and [Mark the member yourself](#mark-the-member), below.
- **On PostgreSQL, a connection string gives a bare connection**, without the plugins or mappings of your data source.  See [the limit](../reference/databases/postgresql/query-methods.md#limit-connection-string-repository).
- **A type whose queries are all asynchronous needs only `OpenAsync`.**

<a id="sql-server-repository"></a>

## Make a SQL Server repository from a connection string

```jsonc
// sqlsource.json, beside your project file
{
    "root": true,
    "dialect": "mssql",
}
```

```csharp
[SqlSourceGenerate]
public partial class OrderRepository(string connectionString);
```

```csharp
// Program.cs
builder.Services.AddSingleton<IOrderRepository>(
    new OrderRepository(builder.Configuration.GetConnectionString("App")!));
```

**What you get.**  Each method of `OrderRepository` makes a `SqlConnection` of the connection string, opens it, and runs the query.  Then it disposes the connection, which returns it to the pool of Microsoft.Data.SqlClient.  The code that uses the repository takes the generated [interface](../reference/repositories.md#repository-interface), `IOrderRepository`.

**The rules you need.**

- **Microsoft.Data.SqlClient has no data source, so a SQL Server repository takes the connection string.**  The build finds it by its name, `connectionString`.  See [the connection](../reference/repository-connections.md#repository-connection) and [connecting to SQL Server](../reference/databases/sqlserver/index.md#connecting).

<a id="mark-the-member"></a>

## Mark the member yourself

If the build reports SQLSRC028, no connection, or SQLSRC029, more than one, mark the member that gives the connection `[SqlSourceConnection]`.

```csharp
// Without the attribute, the build reports SQLSRC028: it never finds a connection by its type.
[SqlSourceGenerate]
public partial class UserRepository([SqlSourceConnection] DbConnection connection);
```

**What you get.**  Each method of `UserRepository` borrows `connection`.  It opens the connection if it is closed, and closes it again before it returns.  It never disposes it.

**The rules you need.**

- The build finds a `string` named `connectionString`, `_connectionString` or `ConnectionString` by its name, and a data source by its type.  It never finds a connection by its type, since only you know whether the repository owns it.  See [The connection](../reference/repository-connections.md#repository-connection).
- You can mark a field, a property, a method or a primary-constructor parameter.  A method is a factory: the repository method disposes what it returns.  A field, a property or a parameter is held: a connection there is borrowed, and never disposed.  See [The connection](../reference/repository-connections.md#repository-connection).
- When one member is marked, only marked members count.  The build finds no other member by its name or type.  See [The connection](../reference/repository-connections.md#repository-connection).
- A database can have two marked members only when exactly one of them is a method that returns a task.  The asynchronous methods use that one, and the synchronous methods the other, as in [Give a repository its connection from a service of your own](#connection-from-a-provider).  See [The connection](../reference/repository-connections.md#repository-connection).
- If the repository queries of the type use more than one logical database, mark one member for each, with its name: `[SqlSourceConnection(Database = "reports")]`.  See [The connection](../reference/repository-connections.md#repository-connection).
- A repository method gives its command no transaction.  On SQL Server, it fails on a borrowed connection that has a pending transaction.  To run queries in a transaction, give them the output `methods`.  See [The connection](../reference/repository-connections.md#repository-connection).
- The build adds the attribute to your project beside `[SqlSourceGenerate]`.  Its one property is `Database`.  See [The three attributes](../reference/attributes.md#attributes).

## If it does not work

- **SQLSRC028.**  The type has no member that the build finds.  Mark the member that gives the connection, or add a `string` named `connectionString` or a data source.  See [SQLSRC028](../diagnostics.md#sqlsrc028).
- **SQLSRC029.**  The type has more than one member that can give the connection, and the message lists them.  Mark only the one to use.  See [SQLSRC029](../diagnostics.md#sqlsrc029).
- **SQLSRC030.**  A mark has no `Database`, and the queries use more than one logical database.  Set its `Database`.  See [SQLSRC030](../diagnostics.md#sqlsrc030).
- **SQLSRC032.**  The marked member cannot give a connection, such as a method that takes a parameter other than a `CancellationToken`.  Mark a member of a kind that the table of [The connection](../reference/repository-connections.md#repository-connection) lists.  See [SQLSRC032](../diagnostics.md#sqlsrc032).
- **`InvalidOperationException` on SQL Server, from a borrowed connection that has a pending transaction.**  A repository method gives its command no transaction, and Microsoft.Data.SqlClient refuses it.  Give the queries the output `methods`.  See [The connection](../reference/repository-connections.md#repository-connection).
