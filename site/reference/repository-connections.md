# Where a repository gets its connection

This page says where the methods of a [repository](repositories.md) get their connection, and what they do with it.

<a id="repository-connection"></a>

## The connection

```csharp
[SqlSourceGenerate]
public partial class OrderRepository(string connectionString);

[SqlSourceGenerate]
public partial class AuditRepository(IConnectionProvider provider)
{
    [SqlSourceConnection]
    private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
        provider.OpenAsync("audit", cancellationToken);
}
```

A repository method gets its connection from one member, its *connection source*.  The source is a member of the repository, a member of a factory that the repository holds, or a static member of the project, as [where the build looks](#connection-order) says.  This section is about a member of the repository.  The build finds the member by its name or its type, or you mark it `[SqlSourceConnection]`.  In the sample, `OrderRepository` has a connection string, which the build finds by its name.  `AuditRepository` has a method of its own, which the attribute marks.  What the repository method does with the connection depends on what the member gives:

| The member | Found | The method's connection |
|----|----|----|
| A `string` field, property or primary-constructor parameter named `connectionString`, `_connectionString` or `ConnectionString`, or any `string` member or method that is marked | By its name and type | A new connection of the string, an `NpgsqlConnection` or a `Microsoft.Data.SqlClient.SqlConnection`, which the method opens and disposes |
| A field, property or primary-constructor parameter of `DbDataSource` or a type derived from it, such as `NpgsqlDataSource` | By its type | A new connection that the data source opens, `OpenConnectionAsync`, or `OpenConnection` for a synchronous method, which the method disposes |
| A method that returns a `DbConnection` or a type derived from it, or a `Task` or `ValueTask` of one, and that takes nothing or one `CancellationToken`, which gets the method's, or `default` from a synchronous method | When marked | The connection it returns, called for each call of the method, opened when it is closed, and disposed.  One that returns a `Task` or `ValueTask` serves asynchronous methods alone |
| A field, property or primary-constructor parameter of `DbConnection` or a type derived from it | When marked | Borrowed: opened when it is closed and closed again before the method returns, never disposed |

The rule to remember: **a connection that a method returns is disposed, and a connection in a field, a property or a parameter is borrowed.**  The repository method disposes the connection that a method returns.  A connection that a field, a property or a parameter keeps is borrowed, and the method leaves it as it found it.  A connection string and a data source make a new connection for each call, which the method disposes.

A [synchronous method](repositories.md#synchronous-repository-methods) gets its connection in the same way, with the synchronous calls of the driver.  It can use any member except a method that returns a task, which it cannot call.  For a synchronous method, give the database one of the other members, or a pair of members.

- **If no member is marked, the build finds the source by its name or its type.**  The source is the one `string` of the type named as in the table, or its one member of a data source type.  The build looks among the fields and properties of the type, and those of its base types that it can reach, `static` ones too.  It also looks among the parameters of the primary constructor.
- **The build never finds a connection by its type: mark it.**  Only you know whether the repository owns the connection.
- **If the repository has no source, the build looks in a [factory that the repository holds](#held-factory), then among the [static members of the project](#static-connection).**  See [where the build looks](#connection-order).
- **If the build finds no source in any of these places, it reports [SQLSRC028](../diagnostics.md#sqlsrc028).**  If it finds two or more, it reports [SQLSRC029](../diagnostics.md#sqlsrc029), which lists them.
- **The build finds a primary-constructor parameter only when it finds no field or property.**  So `partial class UserRepository(NpgsqlDataSource dataSource) { private readonly NpgsqlDataSource _dataSource = dataSource; }` uses `_dataSource`.  The positional parameter of a record yields to its property.  `UserRepository(string connectionString) : RepositoryBase(connectionString)` uses the member of the base type, when the type can reach one.
- **The build finds a parameter that the type passes to its base type, or uses to initialize a field or property, only when you mark it.**  The methods would keep a second copy of it, and the compiler warns of that (CS9107, CS9124).
- **`[SqlSourceConnection]` marks the source.**  You can mark a field, a property, a method or a primary-constructor parameter, of any of the four kinds in the table.  You can also mark a member of a [factory type](#held-factory), or a [static member](#static-connection) of any type of your project.
- **Once one member is marked, only marked members count.**  The build then finds no member of the repository by its name or type.  A factory or a static member of the project still gives the connection of a database that no marked member is for.
- **The build uses a member of a base type when the generated code reaches that member by its name, as in C#.**  A member of the repository, or of a base type between them, can hide it.  A member that the repository cannot reach, such as a `private` member of a base type between them, hides nothing.
  - A method hides a base method of the same parameters.  It also hides one whose call it accepts.  So `Open(int retries)` leaves a marked `Open()` in use, and `Open(int retries = 3)` hides it.
  - An `override` counts as the method it overrides, in the type that declares that method.  So an override of a virtual `Open(int retries = 3)` of the same base type leaves a marked `Open()` in use.
  - A field, property or event hides a base method only when you can invoke it, as with a delegate type.
  - Any member but a generic nested type hides a base field or property of its name.
- **An override of a marked member is marked as the member it overrides.**
- **A marked member that cannot give a connection is [SQLSRC032](../diagnostics.md#sqlsrc032).**  An example is a method with other parameters.  The build then does not look for the database's source in a factory or among the static members of the project.
- **A database can have a pair of marked members when exactly one of them is a method that returns a `Task` or `ValueTask` of a connection.**  The asynchronous methods get their connection from that one.  The synchronous methods get it from the other, which can be any of the four kinds.

  ```csharp
  [SqlSourceGenerate]
  public partial class AuditRepository(IConnectionProvider provider)
  {
      [SqlSourceConnection]
      private Task<DbConnection> OpenAsync(CancellationToken cancellationToken) =>
          provider.OpenAsync("audit", cancellationToken);

      [SqlSourceConnection]
      private DbConnection Open() => provider.Open("audit");
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
- **A SQL Server repository takes a connection string, a method that returns a connection, or a connection that it borrows.**  Microsoft.Data.SqlClient has no data source.
- **A primary-constructor parameter that gives the connection cannot be named `cancellationToken`** ([SQLSRC036](../diagnostics.md#sqlsrc036)).  Every asynchronous method has an argument of that name, which hides the parameter.  The build refuses the name under any `method-type`, though a synchronous method takes no such argument.  Rename the parameter.
- **A primary-constructor parameter that gives the connection cannot be named `parameters` when a repository query of its database has parameters** ([SQLSRC036](../diagnostics.md#sqlsrc036)).  A method whose query has parameters takes its input model as `parameters`, which hides the parameter.  Rename the parameter.
- **A null source throws.**  The method throws `InvalidOperationException` if a connection string, a data source or a connection is null when the method is called.  It also throws if a marked method returns null.  The message is "The connection source 'dataSource' of 'App.UserRepository' is null.", and the method throws before it sends anything.
- **A method that [streams its rows](return-types.md#streaming-rows) gets its connection when the loop asks for the first row.**  When the loop ends, the method disposes the connection, or closes a borrowed connection that it opened.  So the method holds the connection for the whole loop.
- **On a borrowed connection, a repository method gives its command no transaction.**  To run queries in a transaction, give them the output `methods`: see [transactions and timeouts](repositories.md#repository-transactions).
- **On SQL Server, Microsoft.Data.SqlClient refuses the command on a borrowed connection that has a pending transaction.**  It throws `InvalidOperationException`, from a synchronous method as from an asynchronous one.
- **On PostgreSQL, Npgsql runs the command in the transaction of the borrowed connection.**
- **An open connection that a marked method returns, with a transaction that your code began on it, is the same case.**  Microsoft.Data.SqlClient refuses the command, and Npgsql runs it in that transaction.  The method then disposes the connection, which ends that transaction.
- **On PostgreSQL, a connection string makes a bare connection.**  The connection knows none of the plugins and mappings of your data source: see [limits](databases/postgresql/query-methods.md#limit-connection-string-repository).  Give a PostgreSQL repository a data source.

The attribute is `SqlSource.SqlSourceConnectionAttribute`.  SqlSource generates it into your project beside `[SqlSourceGenerate]`.  Its one property, `Database`, names the [logical database](settings.md#setting-database) as the `database` setting does.

<a id="connection-order"></a>

## Where the build looks for the source

```csharp
public interface IConnectionFactory
{
    [SqlSourceConnection]
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);
}

public static class Db
{
    [SqlSourceConnection]
    public static NpgsqlDataSource? Main { get; set; }
}

[SqlSourceGenerate]
public partial class UserRepository(NpgsqlDataSource dataSource);      // dataSource

[SqlSourceGenerate]
public partial class OrderRepository(IConnectionFactory connections);  // connections.OpenAsync

[SqlSourceGenerate]
public partial class ReportRepository;                                 // Db.Main
```

The build looks for the connection source of each [logical database](settings.md#setting-database) of a repository in three places, in this order:

1. The members of the repository, as [the connection](#repository-connection) says.
2. The marked members of a factory that the repository holds: see [a factory that a repository holds](#held-factory).
3. The marked static members of the project: see [a static member of the project](#static-connection).

In the sample, each repository gets its connection from a different place.  `UserRepository` has a data source of its own.  `OrderRepository` holds a factory.  `ReportRepository` has neither, and uses `Db.Main`.

- **The first place that has a source for the database gives it.**  So a member of the repository wins over a factory and over a static member, and a factory wins over a static member.  A static member that you add to the project never changes the source of a repository that has one.
- **A place that has a problem with the database decides it too.**  The build reports the problem, and does not look in the next place.  For example, a marked member of the repository that cannot give a connection is [SQLSRC032](../diagnostics.md#sqlsrc032), though the project has a static member that could.
- **So does a primary-constructor parameter that the build would find by its name or type, but that the type passes to its base type or uses in an initializer.**  The build reports [SQLSRC028](../diagnostics.md#sqlsrc028), which names the parameter, and does not look in a factory or among the static members.  A repository of more than one database has no source found by its name or type, so there such a parameter decides nothing, and the build looks in the next place.
- **The build looks for the source of each database on its own.**  One database of a repository can take a member of the repository, and another a static member of the project.
- **A pair is made in one place.**  Two members of a [pair](#repository-connection), of which exactly one returns a task, must both be members of the repository, of its factories, or static members of the project.
- **If no place has a source for the database, the build reports [SQLSRC028](../diagnostics.md#sqlsrc028).**

<a id="held-factory"></a>

## A factory that a repository holds

```csharp
public interface IConnectionFactory
{
    [SqlSourceConnection]
    Task<DbConnection> OpenAsync(CancellationToken cancellationToken);
}

[SqlSourceGenerate]
public partial class OrderRepository(IConnectionFactory connections);
```

A repository can get its connection from a marked member of an object that it holds, a *factory*.  You mark the member once, on the factory's type, and every repository that holds such a factory uses it.  In the sample, each method of `OrderRepository` calls `connections.OpenAsync(cancellationToken)`, and disposes the connection that it returns.

In this section, a *factory* is the object that the repository holds, not a marked method.  The connection that it gives follows the rule of the kind of its marked member, as in [the connection](#repository-connection): the repository method disposes a connection that a marked method returns, and borrows a connection that a marked field or property keeps.

- **A holder is a field, a property or a primary-constructor parameter of the repository**, or a field or property of a base type that the repository can reach.  It can be `static`, and it needs no mark.
- **The type of a holder has a marked member that the repository can reach.**  So a field of another type, such as an `ILogger`, holds no factory.  The type can be a class, an interface, a struct, a nullable struct, or a type parameter whose constraints have such a member.
- **A marked field, property or parameter is a connection source of the repository itself, not a holder.**
- **A field, property or parameter of a `string`, a `DbDataSource`, a `DbConnection` or a type derived from them is never a holder.**
- **A primary-constructor parameter is a holder only when no field or property of the type or of a base type is one.**  So `OrderRepository(IConnectionFactory connections) { private readonly IConnectionFactory _connections = connections; }` uses `_connections`.
- **A parameter that the type passes to its base type, or uses to initialize a field or property, is not a holder.**  So `OrderRepository(IConnectionFactory connections) : RepositoryBase(connections)` uses the holder of the base type, when the type can reach one.
- **The marked members of the factory are its instance fields, properties and methods**, and those of its base types and of the interfaces it implements, that the repository can reach.  Each is one of the four kinds of [the connection](#repository-connection), with the same rules.
- **A member of the factory's type hides a member of its base type as a repository's member does**, by the rules of [the connection](#repository-connection).  If the holder's type is a type parameter, a member of its class constraint hides a member of its interface constraint by the same rules.
- **A member of the factory that implements a marked member of an interface is marked as that member is, with its `Database`.**  So a holder of a class `ConnectionFactory` that implements `IConnectionFactory` finds the mark on `IConnectionFactory.OpenAsync`.  A repository that implements such an interface gets no mark from it.
- **A marked interface member that the factory's class or struct implements explicitly, or leaves to the interface's default, is reached through the interface.**  So with `UserRepository(ConnectionFactory connections)`, where `ConnectionFactory` implements `IConnectionFactory.OpenAsync` explicitly, each method calls `((IConnectionFactory)connections).OpenAsync(cancellationToken)`.  A public member of the same name that does not implement the marked member is not marked by it.
- **So is one whose implementation a `new` member of the factory's class hides.**  So when `ConnectionFactory : FactoryBase` declares `public new Task<DbConnection> OpenAsync(CancellationToken cancellationToken)`, over the `OpenAsync` of `FactoryBase` that implements `IConnectionFactory`, each method calls `((IConnectionFactory)connections).OpenAsync(cancellationToken)`, which runs the member of `FactoryBase`.
- **A `ref struct` factory cannot be cast to its interface, so a marked member that it implements explicitly cannot give the connection**, and is [SQLSRC032](../diagnostics.md#sqlsrc032) at the holder.  Implement the member implicitly, as a `public` member of the `ref struct`.
- **An override of a marked member is marked as the member it overrides**, as on a repository.
- **The `static` members of a factory are [static members of the project](#static-connection).**  The build does not reach them through the holder.
- **Two holders of factories with marked members give two sources.**  For one database, they are [SQLSRC029](../diagnostics.md#sqlsrc029), as `'_main.OpenAsync', '_audit.OpenAsync'`, unless their `Database`s differ or they are a pair.  Two marked members of one factory are two sources too, and the message names one that is reached through its interface with the interface: `'connections.OpenAsync', 'connections.IAuditFactory.OpenAsync'`.
- **The build looks one level deep.**  It does not look in a factory that a factory holds.
- **The factory's type can be in a referenced project or package**, when that project marks its members with SqlSource's attribute.
- **The `Database` of a factory's mark** works as [which database a shared mark is for](#shared-marks) says.
- **A problem of a factory's member is reported at the holder**, in each repository that would use the member: [SQLSRC032](../diagnostics.md#sqlsrc032) and [SQLSRC033](../diagnostics.md#sqlsrc033).  The message names the member and the holder, as `'IConnectionFactory.OpenAsync' reached through 'connections'`: by the holder's type, or by the interface for a member reached through it.
- **A primary-constructor parameter that holds a factory cannot be named `cancellationToken`, or `parameters` when a repository query of its database has parameters** ([SQLSRC036](../diagnostics.md#sqlsrc036)).  The method's argument of that name would hide it.  Rename the parameter.
- **The method reaches the member through the holder, and the holder first.**  If the holder is null when the method is called, it throws `InvalidOperationException`: "The connection source 'connections' of 'App.OrderRepository' is null."
- **If the member's value is null, or the marked method returns null, the message names the member through its holder**: "The connection source 'connections.OpenAsync' of 'App.OrderRepository' is null."  The method throws before it sends anything.

<a id="static-connection"></a>

## A static member of the project

```csharp
public static class Db
{
    [SqlSourceConnection]
    public static string? ConnectionString { get; set; }
}

[SqlSourceGenerate]
public partial class ReportRepository;
```

A marked `static` member of a type of your project gives the connection to each repository that has no source of its own and holds no factory with one.  Your application sets the member once, at startup.  In the sample, each method of `ReportRepository` makes a connection of `Db.ConnectionString`, and disposes it.

- **A static field, constant, property or method can be the source**, of any of the four kinds of [the connection](#repository-connection).  Its type can be a static class or any other type of the project, but a type marked `[SqlSourceGenerate]`.
- **The member is in your project's own code.**  A marked member of a referenced project or package is not a source of your project.
- **A repository uses the member only when the repository can reach it.**  The build reports nothing for one that it cannot reach, such as a `private` member of another class.
- **A `static` member of a type marked `[SqlSourceGenerate]` is that type's own, and never the source of another repository.**  A static class nested in such a type is a type of its own, and its members are static members of the project.
- **A type marked `[SqlSourceGenerate]` whose queries have no output `repository` needs no connection.**  So the build neither uses nor reports a `static` member or a primary-constructor parameter that such a type marks.
- **A `static` member of a repository's base type is one of the repository's own members**, as [the connection](#repository-connection) says, and never a static member of the project for that repository.  So it is not used when a member of the repository hides it, as [the connection](#repository-connection) says.  Other repositories can use it as a static member of the project.
- **The `Database` of the mark** works as [which database a shared mark is for](#shared-marks) says.
- **A mark that can never give a connection is [SQLSRC032](../diagnostics.md#sqlsrc032), reported once at the mark**, whether a repository needs it or not.  It is not a source.  A `static` member of a base type of a type marked `[SqlSourceGenerate]` is the exception: it is that type's own member, and each repository that derives from the base type and needs it reports it.  These are such marks:
  - a member that cannot give a connection, as on a repository;
  - a member of a generic type, or of a type nested in one;
  - a member of a `file` type, or of a type nested in one;
  - a `static abstract` or `static virtual` member of an interface;
  - a mark on a parameter of a method, or of the primary constructor of a type that is not marked `[SqlSourceGenerate]`;
  - a mark on a lambda, a local function or an enum member.
- **A mark on an instance member of another type marks a member of a factory.**  The build uses it only through a [holder](#held-factory), and reports nothing for it otherwise.
- **The method reaches the member through its type, by its full name.**  So no argument or local of the method hides it.
- **If the member's value is null, or the method returns null, the message names the type and the member**: "The connection source 'Db.ConnectionString' of 'App.ReportRepository' is null."  The method throws `InvalidOperationException` before it sends anything.

<a id="shared-marks"></a>

## Which database a shared mark is for

```csharp
public static class Db
{
    [SqlSourceConnection]
    public static string? Main { get; set; }

    [SqlSourceConnection(Database = "reports")]
    public static string? Reports { get; set; }
}
```

The marks of a factory and of the static members of the project are shared: one mark serves many repositories.  So their `Database` follows other rules than the `Database` of a repository's own member.  In the sample, the queries of `reports` use `Db.Reports`, and the queries of every other database use `Db.Main`.

- **A shared mark without `Database` is a default.**  It serves each database that no mark of the same place names, on a repository of one database or of several.
- **A shared mark that names the database wins over a default of the same place.**  The two are never a pair.
- **A default of an Npgsql type does not serve a SQL Server database.**  Nor does a default of a Microsoft.Data.SqlClient type serve a PostgreSQL database.  So one factory or class can serve repositories of both engines, with no `Database` on each mark.
- **A default of a `string`, a `DbDataSource` or a `DbConnection` serves both engines.**
- **A shared mark that names the database must be of its engine** ([SQLSRC033](../diagnostics.md#sqlsrc033)).  The error is at the holder of a factory, and at the repository's `[SqlSourceGenerate]` for a static member.
- **A shared mark for a database that the repository does not use is ignored**, with no warning.  [SQLSRC031](../diagnostics.md#sqlsrc031) is for the marks of the repository's own members alone.
- **Two shared marks of one place for one database are [SQLSRC029](../diagnostics.md#sqlsrc029)**, unless exactly one of them returns a task.  They are then a pair, as on a repository.
- **On the repository's own members, a mark without `Database` is for the repository's one database.**  On a repository of several databases it is [SQLSRC030](../diagnostics.md#sqlsrc030), as [the connection](#repository-connection) says.
