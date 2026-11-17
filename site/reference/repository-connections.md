# Where a repository gets its connection

This page says where the methods of a [repository](repositories.md) get their connection, and what they do with it.

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

A repository method gets its connection from one member of the type, its *connection source*.  The build finds the member by its name or its type, or you mark it `[SqlSourceConnection]`.  In the sample, `OrderRepository` has a connection string, which the build finds by its name.  `AuditRepository` has a method of its own, which the attribute marks.  What the repository method does with the connection depends on what the member gives:

| The member | Found | The method's connection |
|----|----|----|
| A `string` field, property or primary-constructor parameter named `connectionString`, `_connectionString` or `ConnectionString`, or any `string` member or method that is marked | By its name and type | A new connection of the string, an `NpgsqlConnection` or a `Microsoft.Data.SqlClient.SqlConnection`, which the method opens and disposes |
| A field, property or primary-constructor parameter of `DbDataSource` or a type derived from it, such as `NpgsqlDataSource` | By its type | A new connection that the data source opens, `OpenConnectionAsync`, or `OpenConnection` for a synchronous method, which the method disposes |
| A method that returns a `DbConnection` or a type derived from it, or a `Task` or `ValueTask` of one, and that takes nothing or one `CancellationToken`, which gets the method's, or `default` from a synchronous method | When marked | The connection it returns, called for each call of the method, opened when it is closed, and disposed.  One that returns a `Task` or `ValueTask` serves asynchronous methods alone |
| A field, property or primary-constructor parameter of `DbConnection` or a type derived from it | When marked | Borrowed: opened when it is closed and closed again before the method returns, never disposed |

The rule to remember: **a method is a factory, and a field, a property or a parameter is held.**  The repository method disposes the connection that a method returns.  A connection that the repository holds is borrowed, and the method leaves it as it found it.  A connection string and a data source make a new connection for each call, which the method disposes.

A [synchronous method](repositories.md#synchronous-repository-methods) gets its connection in the same way, with the synchronous calls of the driver.  It can use any member except a method that returns a task, which it cannot call.  For a synchronous method, give the database one of the other members, or a pair of members.

- **If no member is marked, the build finds the source by its name or its type.**  The source is the one `string` of the type named as in the table, or its one member of a data source type.  The build looks among the fields and properties of the type, and those of its base types that it can reach, `static` ones too.  It also looks among the parameters of the primary constructor.
- **The build never finds a connection by its type: mark it.**  Only you know whether the repository owns the connection.
- **If the build finds no source, it reports [SQLSRC028](../diagnostics.md#sqlsrc028).**  If it finds two or more, it reports [SQLSRC029](../diagnostics.md#sqlsrc029), which lists them.
- **The build finds a primary-constructor parameter only when it finds no field or property.**  So `partial class UserRepository(NpgsqlDataSource dataSource) { private readonly NpgsqlDataSource _dataSource = dataSource; }` uses `_dataSource`.  The positional parameter of a record yields to its property.  `UserRepository(string connectionString) : RepositoryBase(connectionString)` uses the member of the base type, when the type can reach one.
- **The build finds a parameter that the type passes to its base type, or uses to initialize a field or property, only when you mark it.**  The methods would keep a second copy of it, and the compiler warns of that (CS9107, CS9124).
- **`[SqlSourceConnection]` marks the source.**  You can mark a field, a property, a method or a primary-constructor parameter, of any of the four kinds in the table.
- **Once one member is marked, only marked members count.**  The build then finds no member by its name or type.
- **An override of a marked member is marked as the member it overrides.**
- **A marked member that cannot give a connection is [SQLSRC032](../diagnostics.md#sqlsrc032).**  An example is a method with other parameters.
- **A database can have a pair of marked members when exactly one of them is a method that returns a `Task` or `ValueTask` of a connection.**  The asynchronous methods get their connection from that one.  The synchronous methods get it from the other, which can be any of the four kinds.

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

- **Two marked members that both return a task are [SQLSRC029](../diagnostics.md#sqlsrc029).**  So are two that return none, and three or more.
- **The build checks each member of a pair as it checks one source.**  A problem of either member costs the database its methods.
- **A pair is not an error when no synchronous method uses its other member**, as when every query is asynchronous.
- **A repository whose queries use more than one logical database marks one member for each, or a pair.**  A [logical database](settings.md#setting-database) is a name for one database that queries run against.  A query belongs to the logical database that its `database` setting names, or else to the one named after its dialect.  The mark names the database with `Database`.  The build compares the names ignoring case.

  ```csharp
  [SqlSourceGenerate(Source = ".")]
  public partial class Reports(
      [SqlSourceConnection(Database = "main")] NpgsqlDataSource main,
      [SqlSourceConnection(Database = "reports")] NpgsqlDataSource reports);
  ```

- **On a repository of several databases, the build finds no member by its name or type.**  Nothing says which database a member is for.
- **On such a type, a mark without `Database` is [SQLSRC030](../diagnostics.md#sqlsrc030).**
- **A mark for a database that no repository query of the type uses is the warning [SQLSRC031](../diagnostics.md#sqlsrc031).**
- **A query whose output is not `repository` needs no source.**
- **The source must be of the engine of its database.**  An Npgsql type for a SQL Server database is [SQLSRC033](../diagnostics.md#sqlsrc033).  So is a Microsoft.Data.SqlClient type for a PostgreSQL database.
- **A SQL Server repository takes a connection string, a factory method, or a connection that it borrows.**  Microsoft.Data.SqlClient has no data source.
- **A primary-constructor parameter that gives the connection cannot be named `cancellationToken`** ([SQLSRC036](../diagnostics.md#sqlsrc036)).  Every asynchronous method has an argument of that name, which hides the parameter.  The build refuses the name under any `method-type`, though a synchronous method takes no such argument.  Rename the parameter.
- **A primary-constructor parameter that gives the connection cannot be named `parameters` when a repository query of its database has parameters** ([SQLSRC036](../diagnostics.md#sqlsrc036)).  A method whose query has parameters takes its input model as `parameters`, which hides the parameter.  Rename the parameter.
- **A null source throws.**  The method throws `InvalidOperationException` if a connection string, a data source or a connection is null when the method is called.  It also throws if a factory returns null.  The message is "The connection source 'dataSource' of 'App.UserRepository' is null.", and the method throws before it sends anything.
- **A method that [streams its rows](return-types.md#streaming-rows) gets its connection when the loop asks for the first row.**  When the loop ends, the method disposes the connection, or closes a borrowed connection that it opened.  So the method holds the connection for the whole loop.
- **On a borrowed connection, a repository method gives its command no transaction.**  To run queries in a transaction, give them the output `methods`: see [transactions and timeouts](repositories.md#repository-transactions).
- **On SQL Server, Microsoft.Data.SqlClient refuses the command on a borrowed connection that has a pending transaction.**  It throws `InvalidOperationException`, from a synchronous method as from an asynchronous one.
- **On PostgreSQL, Npgsql runs the command in the transaction of the borrowed connection.**
- **An open connection that a factory returns, with a transaction that your code began on it, is the same case.**  Microsoft.Data.SqlClient refuses the command, and Npgsql runs it in that transaction.  The method then disposes the connection, which ends that transaction.
- **On PostgreSQL, a connection string makes a bare connection.**  The connection knows none of the plugins and mappings of your data source: see [limits](databases/postgresql/query-methods.md#limit-connection-string-repository).  Give a PostgreSQL repository a data source.

The attribute is `SqlSource.SqlSourceConnectionAttribute`.  SqlSource generates it into your project beside `[SqlSourceGenerate]`.  Its one property, `Database`, names the [logical database](settings.md#setting-database) as the `database` setting does.

<a id="limits"></a>

## Limits

<a id="limit-member-of-the-same-name"></a>

### A marked member of a base type is not found

**What you see.**  A repository does not use a member of its base type that you marked `[SqlSourceConnection]`.  The build reports [SQLSRC028](../diagnostics.md#sqlsrc028), or the repository's methods use another member as their source.

**Why.**  The build skips a member of a base type when the repository, or a base type between them, has a member of the same name.  It skips it even when that member is a method with other parameters, or one that the repository cannot reach, such as a `private` member of a base type between them.  So a method `Open(int retries)` of the repository hides a marked `Open()` of its base type.

**What to do.**  Give the member of the repository, or of the base type between them, another name.

<a id="limit-static-member-of-the-repositorys-name"></a>

### A static member of a base type with the repository's name

**What you see.**  The build fails in the repository's generated methods with CS1061, such as "'string' does not contain a definition for 'Sample'".

**Why.**  The methods reach a `static` connection source, or a `static` member that holds your factory, through the repository's name: `Sample.Sample` for a member `Sample` of the base type of `Sample`.  Inside the repository, `Sample` names the member it inherits, not the type.

**What to do.**  Give the member of the base type another name.
