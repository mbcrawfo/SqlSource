# SQL Server

SqlSource reads SQL Server and Azure SQL files by the `mssql` [dialect](dialects.md), and the `sqlsource` tool can describe their queries: it asks your database what each query's parameters and columns are, and writes the answer to the query's [sidecar](tool.md#sidecar-files).  This page is about describing, about the C# types that a described query's columns and parameters get, and about the models built from them.  How SqlSource reads a T-SQL file, its comments, strings and `[bracketed]` names, is on [the dialects page](dialects.md).

```xml
<PropertyGroup>
    <SqlSourceDialect>mssql</SqlSourceDialect>
</PropertyGroup>
```

```console
$ dotnet sqlsource describe --connection mssql="Server=localhost,1433;Database=app;User ID=app;Password=secret;TrustServerCertificate=True"
mssql (mssql): 12 described, 0 skipped, 0 failed
```

The connection is given for the query's [logical database](tool.md#connections), which is `mssql` when you name none, so its variable is `SQLSOURCE_CONNECTION_MSSQL`.  The summary names that database first, and its dialect in parentheses.

Under `mssql`, a name that a query declares with `DECLARE @name` is a local variable and not a [query parameter](concepts.md#query-parameter): see [variables declared with `DECLARE`](parameters-and-tokens.md#declared-variables).  The rest of what the dialect changes is on [the dialects page](dialects.md#what-a-dialect-changes).

<a id="connecting"></a>

## Connecting

- **The connection string is Microsoft.Data.SqlClient's**: `Server=localhost,1433;Database=app;User ID=app;Password=...`, with the keywords of [SqlClient's documentation](https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring).  A string that SqlClient cannot read, or a database that cannot be opened, is [SQLSRC224](diagnostics.md#sqlsrc224), whose section lists what each of its messages means.
- **A server whose certificate this machine does not trust needs `TrustServerCertificate=True`.**  SqlClient encrypts the connection and checks the server's certificate unless the connection string says otherwise, and SQL Server in a local container, or any server that made its own certificate, has one that no authority signed: without the keyword, opening it is SQLSRC224 with `the server's certificate is not trusted, or the TLS handshake failed`.  Add the keyword only for a server of your own; for any other, install the authority of its certificate.
- **SQL Server 2017 and later are supported**, and SqlSource is tested on 2017 and 2025.  An older server is [SQLSRC235](diagnostics.md#sqlsrc235).  Azure SQL Database and Azure SQL Managed Instance are accepted whatever version they report, and are not tested.
- **Microsoft Entra ID cannot be used.**  The tool cannot sign in with an `Authentication` of `Active Directory ...`: give a SQL Server login and its password, or, on Windows, use Windows authentication with `Integrated Security=True`, which SqlSource is not tested with.  A LocalDB instance, `(localdb)\MSSQLLocalDB`, which is usually opened with Windows authentication, can be used on Windows alone; elsewhere, describe against a SQL Server that listens on a port, such as one in a container.
- **The tool needs your schema, not your data.**  A copy of the schema with no rows, such as a local database that your migrations created, is enough.

<a id="what-describing-does"></a>

## What describing does to your database

**The tool never runs your queries.**  For each query it asks SQL Server to describe the query from its text: the server reads and checks the query to work out the types of its parameters and of its columns, and where each column comes from, and runs none of it.  A `DROP`, an `UPDATE` or a `WAITFOR` in a query does nothing while it is described.  The tool opens one session for each database, reads the server's version and the database's settings, and changes no setting of the session.

<a id="parameters"></a>

## Parameters

```sql mssql
-- name: FindCustomers
SELECT id, name FROM dbo.customers
WHERE (@name IS NULL OR name = @name);
```

SQL Server gives each [query parameter](concepts.md#query-parameter) a type from the way the query uses it: in `WHERE id = @id`, `@id` takes the type of `id`.  A [`-- param:` marker](parameters-and-tokens.md#param-marker) gives a parameter a type of your own instead, in SQL Server's words, `nvarchar(100)` or an alias type such as `dbo.Email`, which SQL Server checks against every use.

- **A parameter used more than once** is typed at each use, and the uses must agree.  A use that says nothing of the type, such as `@name IS NULL`, is left out, so the optional filter above needs no marker: `@name` takes the type of `name`.  Two uses that give two types, `WHERE id = @p OR name = @p`, are [SQLSRC262](diagnostics.md#sqlsrc262): declare the parameter's type, or use two parameters.
- **A parameter used once, alone where nothing gives it a type**, as in `WHERE @p IS NULL`, is an `int`, since SQL Server gives it that type.  Declare its type when you mean another: `-- param: @p nvarchar(100)`.
- **A use that fixes no type**, `SELECT @p AS x`, `COALESCE(@p, name)` or `@a + @b` with nothing else, is [SQLSRC226](diagnostics.md#sqlsrc226).  Write `CAST(@p AS int)` where the parameter is used, or declare its type.
- **A declared type the database does not have** is [SQLSRC228](diagnostics.md#sqlsrc228).

**SQL Server gives some uses a wider type than you may mean**, and the C# type follows it:

| The use | The type SQL Server gives | C# type |
|----|----|----|
| `TOP (@n)`, `OFFSET @skip ROWS`, `FETCH NEXT @take ROWS ONLY` | `bigint` | `long` |
| `name LIKE @p`, `name = @p + N'x'` | `nvarchar(4000)` | `string` |
| `total > @min`, where `total` is a `decimal(18,2)` | `decimal(38,19)` | `decimal` |
| `DATEADD(day, @days, placed_at)` | `int` on SQL Server 2017, `bigint` on 2025 | `int` or `long` |

Declare the type you mean, `-- param: @n int` or `-- param: @min decimal(18,2)`, and SQL Server checks every use against it.  For `DATEADD`, the marker also keeps the parameter's type the same whichever version the tool describes against.

<a id="what-can-be-described"></a>

## What can be described

```sql mssql
-- name: ShipOrder
SET NOCOUNT ON;
UPDATE dbo.orders SET shipped_at = SYSUTCDATETIME() WHERE id = @id;
SELECT id, shipped_at FROM dbo.orders WHERE id = @id;
```

The tool describes a query by its first result set: the rows of its first `SELECT`, or of the first `OUTPUT` of an `INSERT`, `UPDATE`, `DELETE` or `MERGE`.  A query can hold several statements, as above, where the `UPDATE` returns no result set and the `SELECT` gives the query its columns.  A query that returns no result set at all, an `UPDATE` with no `OUTPUT` or an `OUTPUT ... INTO` a table, gets no output model; a `SELECT` that matches no row still has its columns, and gets one.

**Only the first result set is described, and read into the query's model.**  A query that returns two result sets, `SELECT ...; SELECT ...`, gets an output model of the first alone, and nothing is reported of the second: give each `SELECT` a `-- name:` of its own.

| The query | Reported as | What to do |
|----|----|----|
| Paths that return different columns, such as an `IF` whose two `SELECT`s differ | [SQLSRC263](diagnostics.md#sqlsrc263) | Return the same columns on every path, or write a query for each |
| SQL built in a string and run, `EXEC(@sql)` | [SQLSRC264](diagnostics.md#sqlsrc264) | Write the SQL in the query, with a parameter where the string took a value, or a [token](parameters-and-tokens.md#tokens) for a part that changes |
| A temporary table that the query creates and uses in a later statement | [SQLSRC265](diagnostics.md#sqlsrc265) | Make the query one statement, or use a table variable |
| A column with no name, such as `COUNT(*)` without an alias | [SQLSRC266](diagnostics.md#sqlsrc266) | Give the column an alias |
| `CREATE PROCEDURE`, and another statement whose result SQL Server cannot work out | [SQLSRC225](diagnostics.md#sqlsrc225), `Msg 11527` | Give the query `-- output: sql` |
| An `OUTPUT` without `INTO`, on a table that has an enabled trigger | [SQLSRC225](diagnostics.md#sqlsrc225), `Msg 334` | Output the rows `INTO` a table variable and select them from it |
| A `GO` inside the query | [SQLSRC225](diagnostics.md#sqlsrc225), `Msg 102` | Give each batch a `-- name:` of its own |
| A parameter that SqlSource takes for a declared variable, or the other way round | [SQLSRC231](diagnostics.md#sqlsrc231) | Do what the error's `help:` line says |

<a id="nullability"></a>

## Which columns can be null

```sql mssql
-- name: ListOrders
SELECT o.id, o.placed_at, c.name
FROM dbo.orders AS o
LEFT JOIN dbo.customers AS c ON c.id = o.customer_id;
```

For each column the tool records whether it can be null, as SQL Server says.  If `orders.id`, `orders.placed_at` and `customers.name` are all `NOT NULL`, `id` and `placed_at` above are never null, and `name` can be: the `LEFT JOIN` gives a null name to an order with no customer.

SQL Server treats a column as possibly null unless it can show that it never is, and SqlSource keeps its answer as it is.  A column it treats as possibly null that never is costs a null check in your code.  SQL Server says a column can be null wherever a null can come from: a column of a table that can hold null, a column of a view that the view's query can fill with null, the optional side of a `LEFT`, `RIGHT` or `FULL` join or of an `OUTER APPLY`, an aggregate, a subquery's value, a window function, `ROLLUP`, `CUBE` and `GROUPING SETS`, and the `inserted` and `deleted` columns of a `MERGE`'s `OUTPUT` when one of its actions leaves them empty: a `MERGE` that can delete leaves `inserted` empty for the rows it deletes, and one that can insert leaves `deleted` empty for the rows it inserts.

It says that a column is never null when no outer join of the query can fill it with null and it is a `NOT NULL` column of a table, a column of a view that the view's query never fills with null, a constant other than `NULL`, `ISNULL` whose second argument is never null, `CONCAT`, or a `CASE` or an `IIF` whose every result is never null.  It treats these as possibly null, though some of them never are:

- **Aggregates**: `COUNT(*)` and `COUNT_BIG(*)`, which are never null, and `SUM`, `MAX` and the others of values that are never null, in a query with a `GROUP BY`, whose every group has a row.
- **A window function**: `ROW_NUMBER()`, `RANK()`, `SUM(...) OVER (...)`, `LAG(...)` with a default.
- **A cast or a conversion**, `CAST(id AS varchar(10))`, and arithmetic, `id + 1`.
- **`COALESCE`** with an argument that is never null.  `ISNULL(x, 0)` is never null where `COALESCE(x, 0)` is possibly null, so write `ISNULL` where the two mean the same to you.
- **Some functions**, `NEWID()`, `EOMONTH`, `AT TIME ZONE`, and on SQL Server 2022 and later `GREATEST`, `LEAST`, `JSON_OBJECT` and `JSON_ARRAY`.
- **A computed column**, even one declared `PERSISTED NOT NULL`, unless SQL Server can show from its expression that it is never null, as for `ISNULL(quantity, 0)`.
- **A column of a recursive `WITH` query.**
- **A column that a filter keeps from being null**, such as a column that can hold null under `WHERE x IS NOT NULL`, or the optional side of a `LEFT JOIN` that the `WHERE` makes inner.

A column that can be null gets a nullable property in the query's [output model](#models), `int?` or `string?`, and a column that is never null a property that cannot be null, `int` or `string`.  Where you know better, you decide instead: a `-- column:` marker says that a column is never null, or can be, and leaves its name alone, and so does an alias that ends in `!` or `?`.

```sql mssql
-- name: CountOrders
-- column: order_count not null
SELECT customer_id, COUNT(*) AS order_count
FROM dbo.orders
GROUP BY customer_id;
```

A count is never null, though SQL Server treats `COUNT(*)` as possibly null: the marker gives the property `OrderCount` the type `int` and not `int?`.  Nothing checks the marker.  An alias `AS [order_count!]` does the same without the marker, and the mark is left out of the property's name; it stays in the column's name, which Dapper matches to the property's by name, so with Dapper write the marker.  See [which properties can be null](generator.md#model-nullability) and [the `-- column:` marker](generator.md#column-marker).

<a id="types"></a>

## Types

```sql mssql
-- name: GetOrder
SELECT id, placed_at, total, notes, row_version
FROM dbo.orders
WHERE id = @id;
```

Each column and each parameter of a described SQL Server query gets a C# type: the type that Microsoft.Data.SqlClient reads a value of its SQL Server type as.  If `id` is a `bigint`, `placed_at` a `datetime2(7)`, `total` a `decimal(12,2)`, `notes` an `nvarchar(max)` and `row_version` a `rowversion`, they are `long`, `System.DateTime`, `decimal`, `string` and `byte[]`; `@id` is a `long`.  A column that can be null gets its type with `?`: a `bigint` column that can be null is a `long?`, and an `nvarchar` one a `string?`.

The build gives these types to each query whose output is `models` or `codegen` and whose entry in its sidecar is current, and reports an error for a column or parameter that has none, such as [SQLSRC246](diagnostics.md#sqlsrc246) for `geography`.  They are the types of the properties of the query's [models](#models), and a query with such an error has no models.  To give a type another C# type, use the [type override](settings.md#setting-type-overrides): `date=System.DateOnly`.

<a id="which-sqlclient"></a>

### Which version of Microsoft.Data.SqlClient

Microsoft.Data.SqlClient 6.1 and 7 read every type as the same C# type, so the types are those of the table below whatever version of 6.1 or later the project references, directly or through another package:

- **Microsoft.Data.SqlClient 6.1 or 7** gets the table's types.
- **Microsoft.Data.SqlClient older than 6.1** is [SQLSRC267](diagnostics.md#sqlsrc267), an error.  Update it to 6.1 or later.  EF Core 8 and 9 bring an older one: see [known issues](#known-issues).
- **Microsoft.Data.SqlClient 8 or later** gets the types of Microsoft.Data.SqlClient 7, with the information [SQLSRC268](diagnostics.md#sqlsrc268), since this version of SqlSource does not know a newer one.
- **No Microsoft.Data.SqlClient**, as in a library that holds the queries and leaves the driver to the application, gets the table's types too, but for `vector`, whose C# type is Microsoft.Data.SqlClient's own: it is [SQLSRC269](diagnostics.md#sqlsrc269) until the project references Microsoft.Data.SqlClient.  The older `System.Data.SqlClient` package is not Microsoft.Data.SqlClient, and a project that references it alone has no Microsoft.Data.SqlClient.

<a id="type-table"></a>

### The types

The names are SQL Server's, which are the [type override's](settings.md#setting-type-overrides) keys, with, in parentheses, the other one-word names that are also keys.  A type's length, precision or scale does not change its C# type: `nvarchar(50)` and `nvarchar(max)` are both `string`, and `decimal(12,2)` is a `decimal`.

| SQL Server type | C# type |
|----|----|
| `bigint` | `long` |
| `int` (`integer`) | `int` |
| `smallint` | `short` |
| `tinyint` | `byte` |
| `bit` | `bool` |
| `decimal` (`dec`), `numeric`, `money`, `smallmoney` | `decimal` |
| `float` | `double` |
| `real` | `float` |
| `date`, `datetime`, `datetime2`, `smalldatetime` | `System.DateTime` |
| `time` | `System.TimeSpan` |
| `datetimeoffset` | `System.DateTimeOffset` |
| `char` (`character`), `varchar`, `text`, `nchar`, `nvarchar`, `ntext`, `xml`, `json` | `string` |
| `binary`, `varbinary`, `image`, `timestamp` (`rowversion`) | `byte[]` |
| `uniqueidentifier` | `System.Guid` |
| `sql_variant` | `object` |
| `vector` | `Microsoft.Data.SqlTypes.SqlVector<float>`.  Without Microsoft.Data.SqlClient, SQLSRC269 |
| `hierarchyid`, `geography`, `geometry` | None: SQLSRC246.  Reference the package `Microsoft.SqlServer.Types` and map the type with the type override: `hierarchyid=Microsoft.SqlServer.Types.SqlHierarchyId`, `geography=Microsoft.SqlServer.Types.SqlGeography`, `geometry=Microsoft.SqlServer.Types.SqlGeometry` |

A type that the table does not name gets its C# type by what it is:

| Kind | C# type |
|----|----|
| An alias type, `CREATE TYPE dbo.Email FROM nvarchar(320)` | Its base type's C# type: `dbo.Email` is a `string`.  To give it another, map it with the type override by its schema and name, `dbo.Email=MyApp.Email`.  `sysname` is a `string` |
| A CLR type of your own assembly, or a type of a newer SQL Server | None: SQLSRC246.  Map it with the type override to the type the application reads it as; the message names the key to write |

<a id="type-notes"></a>

### What to know about some types

- **`timestamp`** is SQL Server's row version, which it also calls `rowversion`, and not a time, so it is a `byte[]`.  PostgreSQL's `timestamp` is a time: see [type overrides](settings.md#setting-type-overrides) for an entry in a project with both.
- **`date` and `time`** are a `System.DateTime` and a `System.TimeSpan`, and never a `System.DateOnly` or a `System.TimeOnly`, on Microsoft.Data.SqlClient 6.1 and 7.
- **`date`, `datetime`, `datetime2` and `smalldatetime`** are read as a `System.DateTime` whose `Kind` is `Unspecified`, and `datetimeoffset` keeps its offset.  Dapper sends a `DateTime` parameter as a `datetime`, to 1/300 of a second, so a `datetime2` loses digits unless it is sent as a `datetime2`: see [the recipe](recipes.md#dapper-with-sql-server).
- **`float(1)` to `float(24)`** are a `real` to SQL Server, so they are a `float` in C#.  `float(25)` to `float(53)`, and `float` without a length, are a `double`.
- **`sql_variant`** is an `object`, whose value is the C# type of the value the row holds: an `int` for an `int`, a `string` for an `nvarchar`.
- **`json`**, SQL Server 2025's type, is a `string`.
- **`vector`** is a `Microsoft.Data.SqlTypes.SqlVector<float>`, a type of Microsoft.Data.SqlClient, which the project references to name it.

<a id="models"></a>

## Models

```sql mssql
-- name: FindOrders
-- param: @status null
SELECT o.id, o.placed_at, c.name AS customer_name
FROM dbo.orders AS o
LEFT JOIN dbo.customers AS c ON c.id = o.customer_id
WHERE o.placed_at >= @since AND (@status IS NULL OR o.status = @status);
```

```csharp
public sealed record FindOrdersParams(global::System.DateTime Since, string? Status);

public sealed record FindOrdersDto(long Id, global::System.DateTime PlacedAt, string? CustomerName);
```

A described SQL Server query gets the [models](generator.md#models) that every described query gets, by the rules of the generator page; their namespace and documentation are left out above.  What SQL Server decides in them:

- **Each property's type** is its column's or parameter's C# type, from [the type table](#type-table) under the version of Microsoft.Data.SqlClient that the project references, or from the [type override](settings.md#setting-type-overrides).  Above, `id` is a `bigint`, `placed_at` a `datetime2` and `status` an `nvarchar(20)`, all `NOT NULL`: `@since` takes the type of `placed_at`, and `@status` that of `status`.
- **A column's property can be null** by [which columns can be null](#nullability): `customer_name` comes from the optional side of the `LEFT JOIN`, so its property is a `string?`.  A [`-- column:` marker](generator.md#column-marker) can say otherwise, and so can a column's alias with `!` or `?`.
- **A parameter's property can be null** only when its `-- param:` marker says `null`, as `@status` does, since SQL Server does not say whether a parameter can be null.  A marker with `null` and no type leaves the type to SQL Server.
- **A column with no name**, as an expression or a literal without an alias, is [SQLSRC266](diagnostics.md#sqlsrc266) when the tool describes the query.  Give it an alias.
- **A query whose rows are a whole table** gets a model named after the table, with the table's schema before its name unless the schema is `dbo`: `dbo.customers` gives `CustomersDto`, and `billing.invoices` gives `BillingInvoicesDto`.  See [table models](generator.md#table-models).
- **A query that returns several result sets** gets an output model of the first alone: see [what can be described](#what-can-be-described).

<a id="query-methods"></a>

## Query methods

> **Coming in a later release.**  The generator will write a method that runs each described SQL Server query through Microsoft.Data.SqlClient and returns its rows.  See [what works today](README.md#what-works-today).

<a id="known-issues"></a>

## Known issues

- **A project that uses EF Core 8 or 9 references Microsoft.Data.SqlClient 5.1**, which EF Core's SQL Server provider brings, and gets [SQLSRC267](diagnostics.md#sqlsrc267).  Reference Microsoft.Data.SqlClient 6.1 or later from the project itself, beside the EF Core packages:

  ```xml
  <ItemGroup>
      <PackageReference Include="Microsoft.Data.SqlClient" Version="6.1.7" />
  </ItemGroup>
  ```

- **A `vector` column in a project that references a Microsoft.Data.SqlClient older than 6.1** gets, beside [SQLSRC267](diagnostics.md#sqlsrc267), a compiler error in the generated model, because the `SqlVector<float>` it names is not in that version.  The error goes away with the package update that clears SQLSRC267.

- **Some columns that are never null get a property that can be null.**  SQL Server treats a column as possibly null when it cannot show that it never is, and SqlSource keeps its answer, so counts, sums and maxima of a group, window functions, casts, `COALESCE` with an argument that is never null, computed columns and the columns of a recursive `WITH` query, among others, get a property such as `int?` where `int` would do.  No value is wrong, but your code checks for a null that never comes.  Give such a column a `-- column: name not null` marker, or end its alias with `!`, as [which columns can be null](#nullability) shows, or write `ISNULL` in the place of `COALESCE`.

- **A query that returns several result sets gets a model of the first alone**, and nothing is reported of the others.  Give each `SELECT` a `-- name:` of its own, or read the result sets after the first with your own code.
