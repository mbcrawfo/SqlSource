# Exceptions

<a id="unexpected-row-count"></a>

## `UnexpectedRowCountException`

```csharp
// -- name: GetActiveUser -> one
try
{
    var user = await users.GetActiveUser(new GetActiveUserParams(email));
}
catch (UnexpectedRowCountException exception)
{
    // exception.QueryName is "GetActiveUser"
}
```

`UnexpectedRowCountException` says that a query returned the wrong number of rows.  The method of a query throws it in one of these cases:

- The shape of the query is `one`, and the query returns no rows or more than one.
- The shape of the query is `one-optional`, and the query returns more than one row.

The rules:

- **Its message names the query and what it returned**: "The query 'GetActiveUser' returned more than one row."  Its `QueryName` is the name of the query.
- **A [collection method](collection-methods.md#collection-methods) throws it for an item.**  Its message also names the item: "The query 'AddUser' returned no rows for item 1."  Its `ItemIndex` is the position of the item, counted from 0.
- **If a query method throws it, its `ItemIndex` is null.**
- **It derives from `InvalidOperationException`**, which LINQ's `Single` throws.  So code that catches `InvalidOperationException` catches it too.
- **It is in the namespace `SqlSource`.**  SqlSource generates it into every project that uses SqlSource.  A project gets no copy of its own when it uses the copy of the one project it references that has it: see [projects that share internals](#projects-that-share-internals).
- **It is `internal`, unless the project sets [`SqlSourcePublicExceptions`](#public-exceptions).**  So another assembly catches it by its type only when it sees it.  Otherwise, that assembly catches it as `InvalidOperationException`: see [the limit](#limit-caught-by-type).

<a id="transaction-rolled-back"></a>

## `TransactionRolledBackException`

```csharp
// -- name: GetActiveUser -> one, at the output methods, on SQL Server
try
{
    var user = await connection.GetActiveUser(new GetActiveUserParams(email), transaction: transaction);
}
catch (TransactionRolledBackException exception)
{
    // exception.QueryName is "GetActiveUser", and exception.InnerException is the UnexpectedRowCountException.
    // The transaction has completed: begin another to go on.
}
```

`TransactionRolledBackException` says that a query rolled back the transaction that you gave its method.  It is SQL Server's alone.  A SQL Server method throws it when all of these are true:

- The method takes a connection, and the shape of its query is `one`, `one-optional` or `many`.
- The method was given a transaction.
- The method threw an exception of its own, or it reached a database error for a row or for a statement after the rows.
- An error that the rest of the query raised then rolled the transaction back.

The rules:

- **On PostgreSQL, no method throws it.**  An error that PostgreSQL raises aborts the transaction, and the method throws the error as Npgsql's `PostgresException`.  PostgreSQL then refuses each later command in the transaction until you roll it back.  See [PostgreSQL](databases/postgresql/query-methods.md#method-results).
- **Its `InnerException` is the first exception**, one of these:
  - `UnexpectedRowCountException`;
  - the exception for a null in a column whose property cannot be null;
  - the exception of a converter;
  - the `SqlException` of the database's error, such as a divide by zero at a row.
- **The later error is not kept.**  That is the error that rolled the transaction back.
- **Its message names the query**: "The transaction was rolled back by an error that the query 'GetActiveUser' raised after the inner exception was thrown."  Its `QueryName` is the name of the query.
- **Without a transaction, or with the transaction still open, the method throws the first exception as it is.**
- **A database error that itself rolled the transaction back is thrown as its `SqlException`.**  An example is a `THROW` under `SET XACT_ABORT ON`.
- **A Microsoft.Data.SqlClient transaction that had completed before the call is refused before the query runs.**  The method throws `InvalidOperationException`.  So `TransactionRolledBackException` says that the method's own query rolled the transaction back.
- **A library can wrap the transaction of Microsoft.Data.SqlClient.**  If the wrapper keeps its `Connection` set after it completes, the method neither refuses nor reports it.  See [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).
- **A repository method never throws it**, since it takes no transaction.
- **A [collection method](collection-methods.md#collection-methods) given a transaction throws it by the same rule**, when it reads rows.  Its `InnerException` is the method's own exception, or the [`CollectionItemException`](#collection-item-exception) of the item whose error came first.  See [SQL Server](databases/sqlserver/query-methods.md#collection-methods).
- **It derives from `InvalidOperationException`.**  So code that catches `InvalidOperationException` catches it too.
- **It is in the namespace `SqlSource`, and as visible as `UnexpectedRowCountException`**, beside which SqlSource generates it.  So another assembly catches it by its type only when it sees it.  Otherwise, that assembly catches it as `InvalidOperationException`: see [the limit](#limit-caught-by-type).

<a id="collection-item-exception"></a>

## `CollectionItemException`

```csharp
// -- name: AddUser -> one, with -- generator: collection-method, on SQL Server
try
{
    await users.AddUser(newUsers);
}
catch (CollectionItemException exception) when (exception.InnerException is SqlException { Number: 2627 })
{
    // exception.ItemIndex is the position of the user whose key is taken, counted from 0.
    // The method's own transaction was rolled back, so no user was added.
}
```

`CollectionItemException` says that the database reported an error at one item of a [collection method](collection-methods.md#collection-methods).  The collection method throws it.

- **Its `InnerException` is the exception of the driver, unchanged.**
- **Its `ItemIndex` is the position of the item, counted from 0.**  Its `QueryName` is the name of the query.
- **Its message names the query and the item, then gives the message of the database**: "The query 'AddUser' failed at item 2: ...".
- **Catch it where your code would catch the exception of the driver.**  Then test its `InnerException`, as the sample does.
- **It derives from `System.Data.Common.DbException`**, the base of `SqlException` and `PostgresException`.  So code that catches `DbException` catches it too.
- **Its `SqlState`, `IsTransient`, `BatchCommand` and `ErrorCode` are those of its inner exception.**  So a retry policy that tests `IsTransient` works unchanged.
- **If it comes from [another project](#projects-that-share-internals), that project must target .NET 6 or later.**  It can also have such a target among its `TargetFrameworks`.  A project built for `netstandard2.0` or `netstandard2.1` alone cannot pass on the first three.  Your project then gets [SQLSRC038](../diagnostics.md#sqlsrc038).
- **It wraps only a database error at an item.**  An exception of the method's own, such as `UnexpectedRowCountException`, is thrown as [what a collection method throws](collection-methods.md#collection-failures) says.  So is an error that no item raised.
- **It is in the namespace `SqlSource`, and as visible as `UnexpectedRowCountException`**, beside which SqlSource generates it.  So another assembly catches it by its type only when it sees it.  Otherwise, that assembly catches it as `DbException`: see [the limit](#limit-caught-by-type).

<a id="public-exceptions"></a>

## Public exceptions

```xml
<PropertyGroup>
    <SqlSourcePublicExceptions>true</SqlSourcePublicExceptions>
</PropertyGroup>
```

`SqlSourcePublicExceptions` makes the exceptions that the query methods throw `public` in the project that sets it.  Then every project that references the project catches them by their type.  The exceptions are [`UnexpectedRowCountException`](#unexpected-row-count), [`TransactionRolledBackException`](#transaction-rolled-back) and [`CollectionItemException`](#collection-item-exception).

- **`true` makes them `public`.**  SqlSource ignores case when it matches `true`.  Any other value leaves them `internal`.
- **Without the property, they are `internal`.**  Only a project that the project grants `InternalsVisibleTo` sees them.
- **It is a property of the project file**, and not a setting of a settings file.  The exceptions are one set of types for the whole project.
- **A project that sees the exceptions of a project it references uses those**, and gets none of its own.  So the property changes nothing there: see [projects that share internals](#projects-that-share-internals).
- **Leave it off in a library you publish**, unless its callers must catch the exceptions by type.  Public exceptions are part of the public API of the library.
- **An app that references two such libraries sees two copies.**  If the app uses SqlSource, it gets [SQLSRC022](../diagnostics.md#sqlsrc022).  If it does not, it gets an error that the name is ambiguous, where it names one.

<a id="projects-that-share-internals"></a>

## Projects that share internals

```xml
<!-- App.csproj, which uses SqlSource -->
<ItemGroup>
    <InternalsVisibleTo Include="App.Tests" />
</ItemGroup>
```

```csharp
// App.Tests, which may use SqlSource too
try
{
    await users.GetActiveUser(new GetActiveUserParams(email));
}
catch (UnexpectedRowCountException exception)
{
    // the exception that App's method threw
}
```

A test project usually sees the internals of the project it tests, through `InternalsVisibleTo`.  Both can use SqlSource.

- **The attribute, `SqlSourceConverterAttribute` and `SqlSourceConnectionAttribute` are each project's own.**  The two copies of each do not conflict.
- **`SqlSource.NodaTimeConversions`, `SqlSource.NetTopologySuiteConversions` and `SqlSource.JsonConversions` are each project's own too.**  When the query methods of both projects call one of these classes, the two copies do not conflict.  The query methods of a project call these classes to read and send these types:
  - [NodaTime's types on SQL Server](databases/sqlserver/libraries.md#nodatime);
  - [NetTopologySuite's](databases/sqlserver/libraries.md#nettopologysuite) types on SQL Server;
  - System.Text.Json's types on [SQL Server](databases/sqlserver/libraries.md#json) and on [Npgsql 8 and 9](databases/postgresql/libraries.md#json).
- **The exceptions, `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException`, are the other project's.**  If a project sees them in a project it references, it gets none of its own.  Its query methods then throw the exceptions of the other project.
- **A project referenced only under an extern alias does not count.**  That is a reference that has `Aliases` without `global`.  The project then gets its own exceptions.
- **So the test project catches what a method of either project throws, by its type.**  So does a project that sees the internals of the other and does not use SqlSource.
- **They are `internal`, unless the project that has them sets [`SqlSourcePublicExceptions`](#public-exceptions) to `true`.**  Then they are `public`, and any project that references it catches them by type.
- **A project that uses the exceptions of another project gets none of its own, whatever it sets.**
- **A project that sees them in two projects it references gets [SQLSRC022](../diagnostics.md#sqlsrc022).**  An example is an app that sees the internals of two data projects that both use SqlSource.  Put the exceptions in one shared project instead: see [the recipe](../guides/several-projects.md#share-the-exceptions).  The shared project grants `InternalsVisibleTo` to every project that catches them, or that uses SqlSource and sees the internals of the others.  Test projects are among them.
- **A project that sees them in a project that uses another version of SqlSource gets [SQLSRC023](../diagnostics.md#sqlsrc023).**
- **A project on .NET 6 or later gets [SQLSRC038](../diagnostics.md#sqlsrc038) when it sees them in a project that targets an older framework alone**, such as `netstandard2.0`.

The compiler gives no warning CS0436 about the types that are each project's own.  This holds in your code and in the generated code, also when warnings are errors.  So you add nothing to `NoWarn`.  Each project sees only its own copies.  A project that sees the internals of the other and does not use SqlSource does not see them at all.

<a id="limits"></a>

## Limits

<a id="limit-caught-by-type"></a>

### An exception of SqlSource is not caught by its type, or SQLSRC022

**What you see.**  Your code cannot catch `UnexpectedRowCountException`, `TransactionRolledBackException` or `CollectionItemException` by its type.  Or a project sees two copies of them in projects that it references:

- If the project uses SqlSource, it gets [SQLSRC022](../diagnostics.md#sqlsrc022).
- If the project does not use SqlSource, it gets the compiler's error that the name is ambiguous, where it names one.

**Why.**  These exceptions are caught by their type only where they are seen.  A project sees the exceptions of another project when the other grants it `InternalsVisibleTo`, or sets [`SqlSourcePublicExceptions`](#public-exceptions).

**What to do.**

- **For two copies, put the exceptions in [one shared project](../guides/several-projects.md#share-the-exceptions).**  It fixes SQLSRC022, and the compiler's error too.
- **Anywhere else, catch the type that the exception derives from.**  An example is an assembly that calls a `public` method of a project whose exceptions are `internal`.  Catch `InvalidOperationException`, from which the first two derive.  Catch `DbException`, from which `CollectionItemException` derives.

<a id="limit-cref"></a>

### A compiler warning at a `cref` that names one of the exceptions

**What you see.**  A `cref` to the exceptions can fail in a project that uses the exceptions of another project.  The compiler gives a warning at the `cref` when all of these are true:

- The project uses the `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException` of a project it references.
- The project also references another project or package that uses SqlSource, and whose exceptions it cannot see.
- The `cref` is in the project's own documentation comments, and it names one of the exceptions or one of its members.
- The project builds a documentation file.

The warning depends on what the `cref` names:

- **CS0419 for the exception itself**, such as `<exception cref="UnexpectedRowCountException"/>`.
- **CS1574 for a member**, such as `<see cref="UnexpectedRowCountException.QueryName"/>`.

Under warnings as errors, the build fails.

**Why.**  At a `cref`, the compiler finds the exceptions that the project cannot see too.  So it finds two copies of the name.

**What to do.**  Write the reference as the documentation ID of what it names.  The compiler takes a documentation ID as it is.

- **For an exception, write `T:` and the full name**: `cref="T:SqlSource.UnexpectedRowCountException"`, `cref="T:SqlSource.TransactionRolledBackException"` or `cref="T:SqlSource.CollectionItemException"`.
- **For a property, write `P:`**, as `cref="P:SqlSource.UnexpectedRowCountException.QueryName"`.
- **For a constructor, write `M:`.**
