# SQL Server

SqlSource reads SQL Server and Azure SQL files by the `mssql` [dialect](dialects.md), and the `sqlsource` tool can describe their queries: it asks your database what each query's parameters and columns are, and writes the answer to the query's [sidecar](tool.md#sidecar-files).  This page is about describing, about the C# types that a described query's columns and parameters get, about the models built from them, and about the query methods that run them.  How SqlSource reads a T-SQL file, its comments, strings and `[bracketed]` names, is on [the dialects page](dialects.md).

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "mssql",
}
```

```console
$ dotnet sqlsource describe --connection mssql="Server=localhost,1433;Database=app;User ID=app;Password=secret;TrustServerCertificate=True"
mssql (mssql): 12 described, 0 skipped, 0 failed
```

The connection is given for the query's [logical database](tool.md#connections), which is `mssql` when you name none, so its variable is `SQLSOURCE_CONNECTION_MSSQL`, or `SQLSOURCE_CONNECTION` when it is the run's only database.  The summary names that database first, and its dialect in parentheses.

Under `mssql`, a name that a query declares with `DECLARE @name` is a local variable and not a [query parameter](concepts.md#query-parameter): see [variables declared with `DECLARE`](parameters-and-tokens.md#declared-variables).  The rest of what the dialect changes is on [the dialects page](dialects.md#what-a-dialect-changes).

<a id="connecting"></a>

## Connecting

- **The connection string is Microsoft.Data.SqlClient's**: `Server=localhost,1433;Database=app;User ID=app;Password=...`, with the keywords of [SqlClient's documentation](https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring).  A string that SqlClient cannot read, or a database that cannot be opened, is [SQLSRC224](diagnostics.md#sqlsrc224), whose section lists what each of its messages means.
- **A server whose certificate this machine does not trust needs `TrustServerCertificate=True`.**  SqlClient encrypts the connection and checks the server's certificate unless the connection string says otherwise, and SQL Server in a local container, or any server that made its own certificate, has one that no authority signed: without the keyword, opening it is SQLSRC224 with `the server's certificate is not trusted, or the TLS handshake failed`.  Add the keyword only for a server of your own; for any other, install the authority of its certificate.
- **SQL Server 2017 and later are supported**, and SqlSource is tested on 2017, 2019, 2022 and 2025.  An older server is [SQLSRC235](diagnostics.md#sqlsrc235).  Azure SQL Database and Azure SQL Managed Instance are accepted whatever version they report, and are not tested.
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

- **A parameter used more than once** is typed at each use, and the uses must agree.  When they disagree, a use that says nothing of the type, such as `@name IS NULL`, is left out, so the optional filter above needs no marker: `@name` takes the type of `name`.  Two uses that give two types, `WHERE id = @p OR name = @p`, are [SQLSRC262](diagnostics.md#sqlsrc262): declare the parameter's type, or use two parameters.
- **A parameter whose every use is alone where nothing gives it a type**, as in `WHERE @p IS NULL` or `@p IS NULL OR @p IS NOT NULL`, is an `int`, since SQL Server gives it that type.  Declare its type when you mean another: `-- param: @p nvarchar(100)`.
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

For each column the tool records whether it can be null, starting from what SQL Server says.  If `orders.id`, `orders.placed_at` and `customers.name` are all `NOT NULL`, `id` and `placed_at` above are never null, and `name` can be: the `LEFT JOIN` gives a null name to an order with no customer.

SQL Server treats a column as possibly null unless it can show that it never is, and SqlSource keeps its answer unless it can show from your query that the column never is.  A column it treats as possibly null that never is costs a null check in your code.  SQL Server says a column can be null wherever a null can come from: a column of a table that can hold null, a column of a view that the view's query can fill with null, the optional side of a `LEFT`, `RIGHT` or `FULL` join or of an `OUTER APPLY`, an aggregate, a subquery's value, a window function, `ROLLUP`, `CUBE` and `GROUPING SETS`, and the `inserted` and `deleted` columns of a `MERGE`'s `OUTPUT` when one of its actions leaves them empty: a `MERGE` that can delete leaves `inserted` empty for the rows it deletes, and one that can insert leaves `deleted` empty for the rows it inserts.  A `MERGE` that only updates leaves neither empty, so its `inserted` and `deleted` columns are possibly null only where their table's columns can hold null.

SQL Server says that a column is never null when no outer join of the query can fill it with null and it is a `NOT NULL` column of a table, a column of a view that the view's query never fills with null, a constant other than `NULL`, `ISNULL` whose second argument is never null, `CONCAT`, or a `CASE` or an `IIF` whose every result is never null.  In a query that is one `SELECT`, with or without a `WITH` clause, SqlSource also treats these as never null, where SQL Server does not.  A query with `GROUP BY ALL`, which returns a group with no row, gets none of these, and nor does a select list that mixes `*` with other items, such as `SELECT *, COUNT(*) OVER () AS total`:

- **A count**: `COUNT` and `COUNT_BIG`, with or without `OVER (...)`, and a subquery that is a count alone and returns one row, `(SELECT COUNT(*) FROM dbo.orders AS o WHERE o.customer_id = c.id)`, with no `GROUP BY`, `HAVING`, `TOP` or `OFFSET`.  A count is `0` for no rows, never null.
- **A rank**: `ROW_NUMBER()`, `RANK()` and `DENSE_RANK()`, and `NTILE` of a whole number, `NTILE(4)`, with `OVER (...)`.
- **`SUM`, `MAX` and `MIN` of a value that is never null**, in a query with a `GROUP BY`, `ROLLUP`, `CUBE` and `GROUPING SETS` included, whose every group has a row; or with `OVER (...)` whose frame holds the current row, as the frame does when the window gives none.
- **`COALESCE`, `GREATEST` and `LEAST`** with an argument that is never null, and none of a spatial, `hierarchyid` or other .NET type.
- **`CAST` and `CONVERT` of a value that is never null to a built-in type**, such as `int`, `decimal(10, 2)`, `nvarchar(max)` or `datetime2`, written as SQL Server names it, `CONVERT`'s style, when it has one, a whole number written in the query.  `TRY_CAST` and `TRY_CONVERT` are possibly null.
- **Arithmetic of values that are never null**: `+`, `-`, `*`, `/` and `%`, as in `SUM(quantity * unit_price)`.
- **`JSON_OBJECT` and `JSON_ARRAY`**, on SQL Server 2022 and later.
- **A computed column declared `PERSISTED NOT NULL`**, read from its table alone: the query's `FROM` names that one table and nothing else, the column is an item of its own or the query is `SELECT *` from that table, and there is no `ROLLUP`, `CUBE` or `GROUPING SETS`.

A value is never null here when it is one of the above, or when SQL Server says it is never null where the query reads it, such as a `NOT NULL` column that no outer join reaches; an expression that holds an aggregate, `OVER` or a function of your own counts only when it is one of the above.  Under `ROLLUP`, `CUBE` and `GROUPING SETS`, a column of the `GROUP BY` is null in the rows of a total, so outside an aggregate SqlSource takes no column of such a query as a value that is never null, whatever its table says.  A call that names a window of the `WINDOW` clause, `OVER w`, and a function of your own, called with its schema, `dbo.f(x)`, are never one of the above.  These answers assume that the connection that runs the query leaves `ANSI_WARNINGS` on, as Microsoft.Data.SqlClient does: see [known issues](#known-issues).

SqlSource treats these as possibly null, though some of them never are:

- **`LAG(...)` and `LEAD(...)` with a default.**
- **Other functions of values that are never null**, such as `EOMONTH`, `DATEADD`, `AT TIME ZONE` and `NEWID()`.
- **A computed column** that is not persisted, or declared `PERSISTED NOT NULL` and read through a join, a view, a subquery or a `WITH` query, unless SQL Server can show from its expression that it is never null, as for `ISNULL(quantity, 0)`.
- **A column of a recursive `WITH` query.**
- **`COALESCE` over a column of each side of a `FULL JOIN`**, as in `COALESCE(i.id, p.invoice_id)`.
- **A `CASE` whose `ELSE` is the column it tests for null**, `CASE WHEN x IS NULL THEN 0 ELSE x END`.  Write `ISNULL(x, 0)`, which is never null.
- **A column that a filter keeps from being null**, such as a column that can hold null under `WHERE x IS NOT NULL`, or the optional side of a `LEFT JOIN` that the `WHERE` makes inner.
- **A column of a query that is not one `SELECT`**: the `OUTPUT` of an `INSERT`, `UPDATE`, `DELETE` or `MERGE`, a `UNION`, `INTERSECT` or `EXCEPT`, `FOR XML` or `FOR JSON`, and several statements in one query keep SQL Server's answer, so a count or a sum there is possibly null.
- **A conversion to `geometry`, `geography`, `hierarchyid` or another .NET type, or `COALESCE`, `GREATEST` or `LEAST` over a value of one**, since SQL Server can convert the text `'NULL'` to such a type as null: `CAST(N'NULL' AS geometry)` and `COALESCE(shape, 'NULL')` are null.
- **A column of a query that uses `window` as a name**, an alias or a column without brackets, as in `FROM dbo.panes window`, keeps SQL Server's answer.  Write `[window]`.

A column that can be null gets a nullable property in the query's [output model](#models), `int?` or `string?`, and a column that is never null a property that cannot be null, `int` or `string`.  Where you know better, you decide instead: a `-- column:` marker says that a column is never null, or can be, and leaves its name alone, and so does an alias that ends in `!` or `?`.

```sql mssql
-- name: ListOrderMonths
-- column: month_end not null
SELECT id, EOMONTH(placed_at) AS month_end
FROM dbo.orders;
```

If `placed_at` is `NOT NULL`, the last day of its month is never null, though SQL Server treats `EOMONTH` as possibly null: the marker gives the property `MonthEnd` the type `System.DateTime` and not `System.DateTime?`.  The build does not check the marker: a [query method](generator.md#reading-rows) throws when such a column is null after all.  An alias `AS [month_end!]` does the same without the marker, and the mark is left out of the property's name; it stays in the column's name, which Dapper matches to the property's by name, so with Dapper write the marker.  See [which properties can be null](generator.md#model-nullability) and [the `-- column:` marker](generator.md#column-marker).

<a id="types"></a>

## Types

```sql mssql
-- name: GetOrder
SELECT id, placed_at, total, notes, row_version
FROM dbo.orders
WHERE id = @id;
```

Each column and each parameter of a described SQL Server query gets a C# type: the type that Microsoft.Data.SqlClient reads a value of its SQL Server type as.  If `id` is a `bigint`, `placed_at` a `datetime2(7)`, `total` a `decimal(12,2)`, `notes` an `nvarchar(max)` and `row_version` a `rowversion`, they are `long`, `System.DateTime`, `decimal`, `string` and `byte[]`; `@id` is a `long`.  A column that can be null gets its type with `?`: a `bigint` column that can be null is a `long?`, and an `nvarchar` one a `string?`.

The build gives these types to each query whose output is `models` or `codegen` and whose entry in its sidecar is current, and reports an error for a column or parameter that has none, such as [SQLSRC246](diagnostics.md#sqlsrc246) for `geography`.  They are the types of the properties of the query's [models](#models), and a query with such an error has no models.  To give a type another C# type, use the [type override](settings.md#setting-type-overrides), `"date": "System.DateOnly"` in a settings file's `typeOverrides`.  To give one column another, use a [column type](settings.md#setting-column-types), `"users.id": "MyApp.UserId"` in its `columnTypes`, and one parameter, its [`-- param:` marker](parameters-and-tokens.md#param-marker), `-- param: @id -> MyApp.UserId`.  A query method reads and sends that C# type as it is, so a type of yours that Microsoft.Data.SqlClient does not read, such as an id or an `Email` over an alias type, needs [converters](settings.md#converters) to and from the type of the table below.  Rows read through Dapper with a type of your choice need a type handler: see [the recipe](recipes.md#dapper-with-sql-server).

<a id="which-sqlclient"></a>

### Which version of Microsoft.Data.SqlClient

Microsoft.Data.SqlClient 6.1 and 7 read every type as the same C# type, so the types are those of the table below whatever version of 6.1 or later the project references, directly or through another package:

- **Microsoft.Data.SqlClient 6.1 or 7** gets the table's types.
- **Microsoft.Data.SqlClient older than 6.1** is [SQLSRC267](diagnostics.md#sqlsrc267), an error.  Update it to 6.1 or later.  EF Core 8 and 9 bring an older one: see [known issues](#known-issues).  Until it is updated, every type but `vector` gets the table's type, and no query gets its method; a `vector` column or parameter, whose C# type is first in Microsoft.Data.SqlClient 6.1, is [SQLSRC270](diagnostics.md#sqlsrc270) at its query.
- **Microsoft.Data.SqlClient 8 or later** gets the types of Microsoft.Data.SqlClient 7, with the information [SQLSRC268](diagnostics.md#sqlsrc268), since this version of SqlSource does not know a newer one.
- **No Microsoft.Data.SqlClient**, as in a library that holds the queries and leaves the driver to the application, gets the table's types too, but for `vector`, whose C# type is Microsoft.Data.SqlClient's own: it is [SQLSRC269](diagnostics.md#sqlsrc269) until the project references Microsoft.Data.SqlClient.  Such a project gets models alone: give its queries the output `models`, since a query method needs the driver ([SQLSRC273](diagnostics.md#sqlsrc273)).  The older `System.Data.SqlClient` package is not Microsoft.Data.SqlClient, and a project that references it alone has no Microsoft.Data.SqlClient.

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
| `vector` | `Microsoft.Data.SqlTypes.SqlVector<float>`.  Without Microsoft.Data.SqlClient, SQLSRC269; with one older than 6.1, SQLSRC270 |
| `hierarchyid`, `geography`, `geometry` | None: SQLSRC246.  Reference the package `Microsoft.SqlServer.Types` and map the type with the type override: `hierarchyid=Microsoft.SqlServer.Types.SqlHierarchyId`, `geography=Microsoft.SqlServer.Types.SqlGeography`, `geometry=Microsoft.SqlServer.Types.SqlGeometry`.  A query method then reads the column as that type, and sends the parameter as its SQL Server type |

A type that the table does not name gets its C# type by what it is:

| Kind | C# type |
|----|----|
| An alias type, `CREATE TYPE dbo.Email FROM nvarchar(320)` | Its base type's C# type: `dbo.Email` is a `string`.  To give it another, map it with the type override by its schema and name, `dbo.Email=MyApp.Email`.  `sysname` is a `string` |
| A CLR type of your own assembly, or a type of a newer SQL Server | None: SQLSRC246.  Map it with the type override to the type the application reads it as; the message names the key to write |

<a id="type-notes"></a>

### What to know about some types

- **`timestamp`** is SQL Server's row version, which it also calls `rowversion`, and not a time, so it is a `byte[]`.  PostgreSQL's `timestamp` is a time: see [type overrides](settings.md#setting-type-overrides) for an entry in a project with both.
- **`date` and `time`** are a `System.DateTime` and a `System.TimeSpan`, and never a `System.DateOnly` or a `System.TimeOnly`, on Microsoft.Data.SqlClient 6.1 and 7.
- **`date`, `datetime`, `datetime2` and `smalldatetime`** are read as a `System.DateTime` whose `Kind` is `Unspecified`, and `datetimeoffset` keeps its offset.  A query method sends a `datetime2` parameter as a `datetime2`, with all seven digits: see [how a parameter is sent](#method-parameters).  Dapper sends a `DateTime` parameter as a `datetime`, to 1/300 of a second, so a `datetime2` loses digits unless it is sent as a `datetime2`: see [the recipe](recipes.md#dapper-with-sql-server).
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

- **Each property's type** is its column's or parameter's C# type, from [the type table](#type-table) under the version of Microsoft.Data.SqlClient that the project references, or the one you chose, by the [type override](settings.md#setting-type-overrides), a [column type](settings.md#setting-column-types) or a marker.  Above, `id` is a `bigint`, `placed_at` a `datetime2` and `status` an `nvarchar(20)`, all `NOT NULL`: `@since` takes the type of `placed_at`, and `@status` that of `status`.
- **A column's property can be null** by [which columns can be null](#nullability): `customer_name` comes from the optional side of the `LEFT JOIN`, so its property is a `string?`.  A [`-- column:` marker](generator.md#column-marker) can say otherwise, and so can a column's alias with `!` or `?`.
- **A parameter's property can be null** only when its `-- param:` marker says `null`, as `@status` does, since SQL Server does not say whether a parameter can be null.  A marker with `null` and no type leaves the type to SQL Server.
- **A column with no name**, as an expression or a literal without an alias, is [SQLSRC266](diagnostics.md#sqlsrc266) when the tool describes the query.  Give it an alias.
- **A query whose rows are a whole table** gets a model named after the table, with the table's schema before its name unless the schema is `dbo`: `dbo.customers` gives `CustomersDto`, and `billing.invoices` gives `BillingInvoicesDto`.  See [table models](generator.md#table-models).
- **A query that returns several result sets** gets an output model of the first alone: see [what can be described](#what-can-be-described).

<a id="query-methods"></a>

## Query methods

```sql mssql
-- name: AddOrder -> one
INSERT INTO dbo.orders (customer_id, placed_at, total, status)
OUTPUT INSERTED.id
VALUES (@customerId, @placedAt, @total, @status);
```

```csharp
AddOrderDto order = await connection.AddOrder(
    new AddOrderParams(customerId, placedAt, 1234.5m, "placed"),
    transaction);
```

Each described query whose output is `codegen` gets a method that runs it through Microsoft.Data.SqlClient, on a `DbConnection` such as `SqlConnection`: see [query methods](generator.md#query-methods).  The project references Microsoft.Data.SqlClient 6.1 or later ([SQLSRC273](diagnostics.md#sqlsrc273), [SQLSRC267](diagnostics.md#sqlsrc267)).  Above, `placed_at` is a `datetime2(7)`, `total` a `decimal(12,2)` and `status` an `nvarchar(20)`, so the method sends `@placedAt` as a `datetime2`, with all seven digits of its time, `@total` as a `decimal(12,2)` and `@status` as an `nvarchar(20)`.  A value that fits its type never changes how it is sent; what happens to one that does not is [below](#method-parameters).

<a id="method-parameters"></a>

### How a parameter is sent

Each parameter is sent as the SQL Server type it was described with, with that type's length, precision and scale, and never as a type chosen from its value.  So a value keeps its digits, a null is a `NULL` of the parameter's type, which a column of that type takes, and the server sees one declaration of the query whatever the lengths of values that fit their types, and keeps one plan of it.  How a method names a parameter and sends a null is on [the generator page](generator.md#method-parameters).  For some types:

- **A string or a `byte[]` longer than its type** is sent whole, as a `varchar(max)`, an `nvarchar(max)` or a `varbinary(max)`, and never cut.  The server refuses an `INSERT` or an `UPDATE` that would cut it.  The error is 2628, "String or binary data would be truncated".  It is 8152 on SQL Server 2017, and in a database whose compatibility level is below 150 or whose `VERBOSE_TRUNCATION_WARNINGS` is off.  A `WHERE` that compares a column with such a value finds no row that holds its start: `abcdef` finds no `char(5)` that holds `abcde`.

  Spaces at the end of a string, and zero bytes at the end of a `byte[]`, are the exception.  When a value is longer than its column only by them, the server cuts them off with no error and stores the rest.  The server also pads the shorter of two strings with spaces to compare them, so `abcde ` finds the `char(5)` that holds `abcde`.
- **`datetime2`, `time` and `datetimeoffset`** are sent with seven digits of a second, whatever the scale of their type, and the server rounds a value to the scale of the column it stores it in: `12:00:00.1235678` written to a `datetime2(3)` column is `12:00:00.124`.  A `WHERE` compares a column with all seven digits of the value, so a `datetime2(3)` that holds `12:00:00.124` is found by `12:00:00.124` and not by `12:00:00.1235678`.
- **A `decimal` or `numeric` with more digits after the point than its type** is rounded to the type's scale, half away from zero, before it is sent, as the server rounds it when it stores it: `1.23456` for a `decimal(12,4)` is sent as `1.2346`, which an `INSERT` stores and a `WHERE` finds.  One with more digits before the point than the type holds throws `ArgumentException`, "Parameter value '123456789.1000' is out of range.", and nothing is sent.
- **`json`** is sent as an `nvarchar(max)`, which the server checks and stores as `json`.
- **`vector`** is sent as its `SqlVector<float>`, whose length is its dimension, and a null as the null `vector` of the parameter's dimension.
- **An alias type** is sent as its base type: a parameter of `dbo.Email`, made `FROM nvarchar(320)`, as an `nvarchar(320)`.
- **A parameter that has a [converter](settings.md#converters)** is sent as its database type too: an `Email` whose converter returns a `string` is sent as that `nvarchar(320)`, and a converted string longer than that whole, as above.
- **`hierarchyid`, `geography`, `geometry` and a CLR type of your own assembly**, which only a [type override](settings.md#setting-type-overrides) gives a C# type, such as `Microsoft.SqlServer.Types.SqlGeography`, are sent as their SQL Server type, and a null as a `NULL` of it.  A CLR type is named by its schema: `Money` of the schema `dbo` is sent as `dbo.Money`.
- **A type of a newer SQL Server**, which only a type override gives a C# type too, is sent by its value, and Microsoft.Data.SqlClient chooses its type from it.

<a id="method-results"></a>

### What a method reads and throws

- **Give each method the transaction** of a connection that has one: Microsoft.Data.SqlClient refuses a command without it, with `InvalidOperationException`, before anything is sent, as [the connection](generator.md#method-connection) says.
- **A column is read as its property's C# type**, the one [the types](#types) give it.  A type of yours that Microsoft.Data.SqlClient never reads, such as an id of your own, takes [converters](settings.md#converters): the column is read as the type of [the table](#type-table), an alias type as its base type, and converted.
- **A C# enum is read only from a column of its own underlying type**: an enum over `int`, the default, from an `int`, and an enum over `byte` from a `tinyint`.  An enum over `int` throws `InvalidCastException` for a `tinyint`, a `smallint` or a `bigint`, since Microsoft.Data.SqlClient never widens or narrows a value.  Declare the enum over its column's type, `enum Level : byte` for a `tinyint`.
- **A column whose property cannot be null** throws Microsoft.Data.SqlClient's `SqlNullValueException` when it holds a null, and a `vector` column the method's own `InvalidCastException`, "Column 'name' is null.", before any converter is called.
- **A query of several statements returns the rows of its first result set**, as its model has them, and its other statements still run.  An error that a statement after that result set raises, such as a `THROW`, is thrown as the error's `SqlException` by every method: one that reads rows, `one`, `one-optional` or `many`, reads the rest of the batch before it returns, so it throws the error in place of its rows, and a loop over [streamed rows](settings.md#streaming-rows) throws it after the last row.  The statements before the error have run, and in a transaction the error may have rolled the transaction back, depending on the error and on `XACT_ABORT`: under `SET XACT_ABORT ON` nearly every error does, and with it off `CAST('x' AS int)` does where a `THROW`, a divide by zero or `CAST(300 AS tinyint)` leaves it open.  Its `rowcount` adds up the rows its statements insert, update or delete, and a `SELECT` among them adds nothing.
- **A loop over [streamed rows](settings.md#streaming-rows) that ends early throws an error of a row it left unread.**  At a `break`, or an exception in the loop's body, the rows still to come and the statements after them are read before the loop ends, and an error that SQL Server raises for one of them, such as a divide by zero, is thrown: at the `break`, or in place of the exception that the loop's body threw, which is then lost.  In a transaction, the error may have rolled the transaction back, as for an error after the rows.
- **A method's own exception in a transaction that the rest of the batch rolled back is `TransactionRolledBackException`.**  A method that throws an exception of its own before it has read the rest of its batch, `UnexpectedRowCountException`, a null in a column whose property cannot be null, a `vector` column's `InvalidCastException` or a converter's exception, throws it in place of an error that the rest of the batch raises.  The rest of the batch still runs, and in a transaction such an error can roll the transaction back, as for an error after the rows.  When it has, the method throws [`TransactionRolledBackException`](generator.md#transaction-rolled-back), whose `InnerException` is its own exception, so a `catch` of it tells you that the transaction you gave the method has completed: its `Connection` is null, and `Commit` throws `InvalidOperationException`, "This SqlTransaction has completed; it is no longer usable.".  Without a transaction, or with the transaction still open, the method's own exception is thrown as it is.  The same holds at a row of a loop over [streamed rows](settings.md#streaming-rows).  An error that SQL Server raises for a row, such as a divide by zero, is thrown as its `SqlException`, even when the rest of the batch then rolls the transaction back: see [known issues](#known-issues).
- **`rowcount` is `-1` for a `SELECT`, and under `SET NOCOUNT ON`.**  A query that sets `NOCOUNT` returns `-1`.  When it has no parameters, the setting also lasts for the connection's session, so a `rowcount` method called after it on the same open connection returns `-1` too; a query with parameters, which Microsoft.Data.SqlClient runs through `sp_executesql`, sets it for itself alone.  See [the generator's known issues](generator.md#known-issues).
- **`commandTimeout`** that passes throws `SqlException` with `Number` `-2`, whose message starts "Execution Timeout Expired.".
- **A cancelled method throws as Microsoft.Data.SqlClient does**, and the method passes it on unchanged: `TaskCanceledException` when the token is cancelled before the call or once the server has begun to send the result, and `SqlException`, with `Number` `0` and a message that ends "Operation cancelled by user.", when it is cancelled while the command runs before that.  A `SELECT` usually begins its result before its first row, so a cancel while it waits for that row is a `TaskCanceledException`, and a cancel while an `UPDATE` waits on a lock is a `SqlException`.  A `catch (OperationCanceledException)` does not catch the second: check the token in a `catch (SqlException)` as well.
- **The connection's commands must create Microsoft.Data.SqlClient's own parameters**, as those of `SqlConnection` do.  A connection that wraps a `SqlConnection` and creates parameters of its own is a [known issue](generator.md#known-issues).

<a id="known-issues"></a>

## Known issues

- **A project that uses EF Core 8 or 9 references Microsoft.Data.SqlClient 5.1**, which EF Core's SQL Server provider brings, and gets [SQLSRC267](diagnostics.md#sqlsrc267).  Reference Microsoft.Data.SqlClient 6.1 or later from the project itself, beside the EF Core packages:

  ```xml
  <ItemGroup>
      <PackageReference Include="Microsoft.Data.SqlClient" Version="6.1.7" />
  </ItemGroup>
  ```

- **Some columns that are never null get a property that can be null.**  SQL Server treats a column as possibly null when it cannot show that it never is, and SqlSource keeps its answer where it cannot show otherwise from the query, so `EOMONTH`, `AT TIME ZONE` and other functions of values that are never null, `LAG` and `LEAD` with a default, the columns of a recursive `WITH` query, `COALESCE` over both sides of a `FULL JOIN`, a computed column that is not persisted, a column that a filter keeps from being null, and a count or a sum in an `OUTPUT` clause or a `UNION`, among others, get a property such as `int?` where `int` would do.  No value is wrong, but your code checks for a null that never comes.  Give such a column a `-- column: name not null` marker, or end its alias with `!`, as [which columns can be null](#nullability) shows, or write `ISNULL(x, 0)` in the place of a `CASE` that tests `x` for null.

- **A connection that turns `ANSI_WARNINGS` off can read null from a column whose property cannot be null.**  With `ANSI_WARNINGS` and `ARITHABORT` both off, SQL Server returns null where an arithmetic overflow, a value too large for its type or a division by zero would otherwise be an error, so a count, a sum, a cast or a conversion, `COALESCE`, `GREATEST`, `LEAST` or arithmetic that SqlSource treats as never null can be null.  Microsoft.Data.SqlClient turns `ANSI_WARNINGS` on when it connects: leave it on, and run no `SET ANSI_WARNINGS OFF` on a connection that runs your queries.

- **A query that returns several result sets gets a model of the first alone**, and nothing is reported of the others.  Its method reads that one alone.  Give each `SELECT` a `-- name:` of its own, or give the query the output `models` and read the result sets after the first with your own code.

- **A transaction can be rolled back by an error hidden behind an error at a row.**  When SQL Server raises an error for a row that a method reads, such as a divide by zero, the method throws that error's `SqlException`, and the rest of the batch still runs.  With `XACT_ABORT` off the first error alone leaves the transaction open, but an error that the rest of the batch raises can roll it back, such as a later `CAST('x' AS int)`, and it is not thrown: the `SqlException` says nothing of it.  The same holds for a loop over [streamed rows](settings.md#streaming-rows).  If your code catches the `SqlException` and goes on with the transaction, the transaction's `Connection` is null, the commands you then give it run outside any transaction and are committed one by one, and only `Commit` throws `InvalidOperationException`, "This SqlTransaction has completed; it is no longer usable.".  After catching such an error, check the transaction, and treat a `Connection` that is null as a rollback:

  ```csharp
  try
  {
      var orders = await connection.ListOrders(new ListOrdersArgs(customerId), transaction: transaction, cancellationToken: token);
  }
  catch (SqlException exception) when (transaction.Connection is null)
  {
      throw new InvalidOperationException("The transaction was rolled back by an error the query raised.", exception);
  }
  ```

- **A `rowcount` method returns `-1` once `SET NOCOUNT ON` has run on the connection's session.**  See [the generator's known issues](generator.md#known-issues).

- **A query method fails on a connection that wraps a `SqlConnection` and creates parameters of its own.**  Give the method the `SqlConnection` inside: see [the generator's known issues](generator.md#known-issues).

- **A `-- param:` type whose name holds an accent written as a separate combining mark, a letter number such as `Ⅰ`, or an invisible formatting character after its first character** is refused as [SQLSRC228](diagnostics.md#sqlsrc228), "it is not written as a type name", though SQL Server accepts the name.  Write that part of the name in brackets, as in `-- param: @p dbo.[PartⅠ]`, or write an accented letter as one character.
