# Give a repository its connection

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

A member that the build does not find by its name or type is marked `[SqlSourceConnection]`.  `UserRepository`'s property gives a connection string, from which each method makes a connection and disposes it; on PostgreSQL that is a bare connection, without your data source's plugins or mappings, as [the limit](../reference/databases/postgresql/query-methods.md#limit-connection-string-repository) says.  `OrderRepository`'s methods are factories: each repository method calls one, opens the connection it returns when it is closed, and disposes it, an asynchronous method `OpenAsync` and a [synchronous one](../reference/repositories.md#synchronous-repository-methods) `Open`.  A type whose queries are all asynchronous needs only `OpenAsync`.  A property or field of a `DbConnection` is borrowed instead, and never disposed.

Rules: [the connection](../reference/repositories.md#repository-connection).

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

Microsoft.Data.SqlClient has no data source, so a SQL Server repository takes the connection string, found by its name, `connectionString`.  Each method makes a `SqlConnection` of it, opens it, runs the query and disposes the connection, which returns it to Microsoft.Data.SqlClient's pool.  The code that uses the repository takes the generated `IOrderRepository`.

Rules: [the connection](../reference/repositories.md#repository-connection), [the interface](../reference/repositories.md#repository-interface), [connecting to SQL Server](../reference/databases/sqlserver/index.md#connecting).
