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

The method of a query whose shape is `one` throws `UnexpectedRowCountException` when the query returns no rows or more than one, and that of a query whose shape is `one-optional` when it returns more than one.  Its message names the query and what it returned, "The query 'GetActiveUser' returned more than one row.", and its `QueryName` the query.  A [collection method](collection-methods.md#collection-methods) throws it for an item, and its message also names the item, "The query 'AddUser' returned no rows for item 1.", and its `ItemIndex` is the item's position, counted from 0; a query method's `ItemIndex` is null.

- **It derives from `InvalidOperationException`**, which LINQ's `Single` throws, so code that catches that catches it too.
- **It is in the namespace `SqlSource`**, generated into every project that uses SqlSource, unless the project uses the copy of the one project it references that has it: see [projects that share internals](#projects-that-share-internals).  It is `internal`, unless the project sets [`SqlSourcePublicExceptions`](#public-exceptions), so another assembly catches it by type only when it sees it, and as `InvalidOperationException` otherwise: see [the limit](#limit-caught-by-type).

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

A SQL Server method of the shape `one`, `one-optional` or `many` that takes a connection throws `TransactionRolledBackException` when it was given a transaction, threw an exception of its own or reached an error that the database raised for a row or for a statement after the rows, and an error that the rest of its query raised then rolled the transaction back.  The first exception is the `InnerException`: `UnexpectedRowCountException`, a null in a column whose property cannot be null, a converter's exception, or the `SqlException` of the database's error, such as a divide by zero at a row.  The later error, the one that rolled the transaction back, is not kept.  Its message names the query, "The transaction was rolled back by an error that the query 'GetActiveUser' raised after the inner exception was thrown.", and its `QueryName` the query.  Without a transaction, or with the transaction still open, the method throws the first exception as it is, and a database error that itself rolled the transaction back, such as a `THROW` under `SET XACT_ABORT ON`, is thrown as its `SqlException`.  A Microsoft.Data.SqlClient transaction that had completed before the call is refused before the query runs, with `InvalidOperationException`, so this exception says that the method's own query rolled the transaction back.  A transaction of a library that wraps Microsoft.Data.SqlClient's and keeps its `Connection` set after it completes is neither refused nor reported: see [what a method reads and throws](databases/sqlserver/query-methods.md#method-results).

- **A repository method never throws it**, since it takes no transaction.
- **A [collection method](collection-methods.md#collection-methods) given a transaction throws it by the same rule**, when it reads rows: its `InnerException` is the method's own exception, or the [`CollectionItemException`](#collection-item-exception) of the item whose error came first.  See [SQL Server](databases/sqlserver/query-methods.md#collection-methods).
- **It derives from `InvalidOperationException`**, so code that catches that catches it too.
- **It is in the namespace `SqlSource`, and as visible as `UnexpectedRowCountException`**, beside which it is generated, so another assembly catches it by type only when it sees it, and as `InvalidOperationException` otherwise: see [the limit](#limit-caught-by-type).

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

A [collection method](collection-methods.md#collection-methods) throws `CollectionItemException` when the database reports an error at one of its items.  Its `InnerException` is the driver's exception, unchanged, its `ItemIndex` the item's position, counted from 0, and its `QueryName` the query.  Its message names the query and the item, then gives the database's message: "The query 'AddUser' failed at item 2: ...".  Where your code would catch the driver's exception, catch this one and test its `InnerException`, as above.

- **It derives from `System.Data.Common.DbException`**, the base of `SqlException` and `PostgresException`, so code that catches `DbException` catches it too.  Its `SqlState`, `IsTransient`, `BatchCommand` and `ErrorCode` are those of its inner exception, so a retry policy that tests `IsTransient` works unchanged.  When it comes from [another project](#projects-that-share-internals), that project targets .NET 6 or later, or has such a target among its `TargetFrameworks`: one built for `netstandard2.0` or `netstandard2.1` alone cannot pass on the first three, and your project gets [SQLSRC038](../diagnostics.md#sqlsrc038).
- **It wraps only a database error at an item.**  An exception of the method's own, such as `UnexpectedRowCountException`, and an error that no item raised, are thrown as [what a collection method throws](collection-methods.md#collection-failures) says.
- **It is in the namespace `SqlSource`, and as visible as `UnexpectedRowCountException`**, beside which it is generated, so another assembly catches it by type only when it sees it, and as `DbException` otherwise: see [the limit](#limit-caught-by-type).

<a id="public-exceptions"></a>

## Public exceptions

```xml
<PropertyGroup>
    <SqlSourcePublicExceptions>true</SqlSourcePublicExceptions>
</PropertyGroup>
```

`SqlSourcePublicExceptions` makes the exceptions that the query methods throw, [`UnexpectedRowCountException`](#unexpected-row-count), [`TransactionRolledBackException`](#transaction-rolled-back) and [`CollectionItemException`](#collection-item-exception), `public` in the project that sets it, so that every project that references it catches them by type.  Without it they are `internal`, and only a project that the project grants `InternalsVisibleTo` sees them.  `true` is matched ignoring case; any other value, and none, leaves them `internal`.

- **It is a property of the project file**, and not a setting of a settings file: the exceptions are one set of types for the whole project.
- **A project that sees the exceptions of a project it references** uses those and gets none of its own, so the property changes nothing there: see [projects that share internals](#projects-that-share-internals).
- **Leave it off in a library you publish**, unless its callers must catch the exceptions by type.  Public exceptions are part of its public API, and an app that references two such libraries sees two copies: it gets [SQLSRC022](../diagnostics.md#sqlsrc022) if it uses SqlSource, and an error that the name is ambiguous where it names one if it does not.

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

- **The attribute, `SqlSourceConverterAttribute` and `SqlSourceConnectionAttribute`** are each project's own, and the two copies of each do not conflict.  The same holds for `SqlSource.NodaTimeConversions`, `SqlSource.NetTopologySuiteConversions` and `SqlSource.JsonConversions`, the classes that the query methods of a project call to read and send [NodaTime's types on SQL Server](databases/sqlserver/libraries.md#nodatime), [NetTopologySuite's](databases/sqlserver/libraries.md#nettopologysuite), and System.Text.Json's types on [SQL Server](databases/sqlserver/libraries.md#json) and on [Npgsql 8 and 9](databases/postgresql/libraries.md#json), when the query methods of both projects call one.
- **The exceptions, `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException`, are the other project's.**  A project that sees them in a project it references gets none of its own, and its query methods throw the other project's.  A project referenced only under an extern alias (`Aliases` on its reference, without `global`) does not count: the project gets its own.  So the test project catches what a method of either project throws by its type, and so does a project that sees the other's internals and does not use SqlSource.
- **They are `internal`**, unless the project that has them sets [`SqlSourcePublicExceptions`](#public-exceptions) to `true`, which makes them `public`, so that any project that references it catches them by type.  A project that uses another's exceptions gets none of its own, whatever it sets.
- **A project that sees them in two projects it references** gets [SQLSRC022](../diagnostics.md#sqlsrc022), as an app does that sees the internals of two data projects that both use SqlSource.  Put them in one shared project instead, which grants `InternalsVisibleTo` to every project that catches them or uses SqlSource and sees the others' internals, test projects among them: see [the recipe](../recipes.md#share-the-exceptions).  A project that sees them in a project that uses another version of SqlSource gets [SQLSRC023](../diagnostics.md#sqlsrc023), and a project on .NET 6 or later that sees them in a project that targets an older framework alone, such as `netstandard2.0`, gets [SQLSRC038](../diagnostics.md#sqlsrc038).

The compiler gives no warning (CS0436) about the types that are each project's own, in your code or in the generated code, also when warnings are errors, so nothing has to be added to `NoWarn`.  Each project sees only its own copies, and a project that sees the other's internals and does not use SqlSource does not see them at all.

<a id="limits"></a>

## Limits

<a id="limit-caught-by-type"></a>

### An exception of SqlSource is not caught by its type, or SQLSRC022

- **`UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException` are caught by their type only where they are seen.**  A project that sees another's exceptions, because the other grants it `InternalsVisibleTo` or sets [`SqlSourcePublicExceptions`](#public-exceptions), catches them by type.  A project that uses SqlSource and sees them in two projects it references gets [SQLSRC022](../diagnostics.md#sqlsrc022), which [one shared project](../recipes.md#share-the-exceptions) fixes; a project that does not use SqlSource and sees two copies gets the compiler's error that the name is ambiguous where it names one, which the same shared project fixes.  Anywhere else, as in an assembly that calls a `public` method of a project whose exceptions are `internal`, catch `InvalidOperationException`, from which the first two derive, and `DbException`, from which `CollectionItemException` derives.

<a id="limit-cref"></a>

### A compiler warning at a `cref` that names one of the exceptions

- **A `cref` to the exceptions can fail in a project that uses another project's.**  A project that uses the `UnexpectedRowCountException`, `TransactionRolledBackException` and `CollectionItemException` of a project it references, and also references another project or package that uses SqlSource and whose exceptions it cannot see, gets a compiler warning at a `cref` in its own documentation comments that names one of the exceptions or one of its members, when it builds a documentation file: CS0419 for the exception itself, such as `<exception cref="UnexpectedRowCountException"/>`, and CS1574 for a member, such as `<see cref="UnexpectedRowCountException.QueryName"/>`.  Under warnings as errors the build fails.  Write the reference as the documentation ID of what it names, which the compiler takes as it is: `T:` and the full name for an exception, as `cref="T:SqlSource.UnexpectedRowCountException"`, `cref="T:SqlSource.TransactionRolledBackException"` or `cref="T:SqlSource.CollectionItemException"`, and `P:` for a property or `M:` for a constructor, as `cref="P:SqlSource.UnexpectedRowCountException.QueryName"`.
