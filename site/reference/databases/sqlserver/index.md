# SQL Server

SqlSource reads SQL Server and Azure SQL files by the `mssql` [dialect](../../../settings.md#setting-dialect), and the `sqlsource` tool can describe their queries: it asks your database what each query's parameters and columns are, and writes the answer to the query's [sidecar](../../../tool.md#sidecar-files).  These pages are about describing, about the C# types that a described query's columns and parameters get, about the models built from them, and about the query methods that run them.  How SqlSource reads a T-SQL file, its comments, strings and `[bracketed]` names, is on [the dialects page](../../dialects.md#what-a-dialect-changes).

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

The connection is given for the query's [logical database](../../../tool.md#connections), which is `mssql` when you name none, so its variable is `SQLSOURCE_CONNECTION_MSSQL`, or `SQLSOURCE_CONNECTION` when it is the run's only database.  The summary names that database first, and its dialect in parentheses.

Under `mssql`, a name that a query declares with `DECLARE @name` is a local variable and not a [query parameter](../../../concepts.md#query-parameter): see [variables declared with `DECLARE`](../../parameters.md#declared-variables).  The rest of what the dialect changes is on [the dialects page](../../dialects.md#what-a-dialect-changes).

<a id="connecting"></a>

## Connecting

- **The connection string is Microsoft.Data.SqlClient's**: `Server=localhost,1433;Database=app;User ID=app;Password=...`, with the keywords of [SqlClient's documentation](https://learn.microsoft.com/dotnet/api/microsoft.data.sqlclient.sqlconnection.connectionstring).  A string that SqlClient cannot read, or a database that cannot be opened, is [SQLSRC224](../../../diagnostics.md#sqlsrc224), whose section lists what each of its messages means.
- **A server whose certificate this machine does not trust needs `TrustServerCertificate=True`.**  SqlClient encrypts the connection and checks the server's certificate unless the connection string says otherwise, and SQL Server in a local container, or any server that made its own certificate, has one that no authority signed: without the keyword, opening it is SQLSRC224 with `the server's certificate is not trusted, or the TLS handshake failed`.  Add the keyword only for a server of your own; for any other, install the authority of its certificate.
- **The supported versions of SQL Server** are in [Supported versions](../index.md#supported-versions), and what holds for Azure SQL is in [Azure SQL](../index.md#azure-sql).  An older server is [SQLSRC235](../../../diagnostics.md#sqlsrc235).
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

SQL Server gives each [query parameter](../../../concepts.md#query-parameter) a type from the way the query uses it: in `WHERE id = @id`, `@id` takes the type of `id`.  A [`-- param:` marker](../../parameters.md#param-marker) gives a parameter a type of your own instead, in SQL Server's words, `nvarchar(100)` or an alias type such as `dbo.Email`, which SQL Server checks against every use.

- **A parameter used more than once** is typed at each use, and the uses must agree.  When they disagree, a use that says nothing of the type, such as `@name IS NULL`, is left out, so the optional filter above needs no marker: `@name` takes the type of `name`.  Two uses that give two types, `WHERE id = @p OR name = @p`, are [SQLSRC262](../../../diagnostics.md#sqlsrc262): declare the parameter's type, or use two parameters.
- **A parameter whose every use is alone where nothing gives it a type**, as in `WHERE @p IS NULL` or `@p IS NULL OR @p IS NOT NULL`, is an `int`, since SQL Server gives it that type.  Declare its type when you mean another: `-- param: @p nvarchar(100)`.
- **A use that fixes no type**, `SELECT @p AS x`, `COALESCE(@p, name)` or `@a + @b` with nothing else, is [SQLSRC226](../../../diagnostics.md#sqlsrc226).  Write `CAST(@p AS int)` where the parameter is used, or declare its type.
- **A declared type the database does not have** is [SQLSRC228](../../../diagnostics.md#sqlsrc228).

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
| Paths that return different columns, such as an `IF` whose two `SELECT`s differ | [SQLSRC263](../../../diagnostics.md#sqlsrc263) | Return the same columns on every path, or write a query for each |
| SQL built in a string and run, `EXEC(@sql)` | [SQLSRC264](../../../diagnostics.md#sqlsrc264) | Write the SQL in the query, with a parameter where the string took a value, or a [token](../../tokens.md#tokens) for a part that changes |
| A temporary table that the query creates and uses in a later statement | [SQLSRC265](../../../diagnostics.md#sqlsrc265) | Make the query one statement, or use a table variable |
| A column with no name, such as `COUNT(*)` without an alias | [SQLSRC266](../../../diagnostics.md#sqlsrc266) | Give the column an alias |
| `CREATE PROCEDURE`, and another statement whose result SQL Server cannot work out | [SQLSRC225](../../../diagnostics.md#sqlsrc225), `Msg 11527` | Give the query `-- output: sql` |
| An `OUTPUT` without `INTO`, on a table that has an enabled trigger | [SQLSRC225](../../../diagnostics.md#sqlsrc225), `Msg 334` | Output the rows `INTO` a table variable and select them from it |
| A `GO` inside the query | [SQLSRC225](../../../diagnostics.md#sqlsrc225), `Msg 102` | Give each batch a `-- name:` of its own |
| A parameter that SqlSource takes for a declared variable, or the other way round | [SQLSRC231](../../../diagnostics.md#sqlsrc231) | Do what the error's `help:` line says |

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

A described SQL Server query gets the [models](../../../generator.md#models) that every described query gets, by the rules of the generator page; their namespace and documentation are left out above.  What SQL Server decides in them:

- **Each property's type** is its column's or parameter's C# type, from [the type table](types.md#type-table) under the version of Microsoft.Data.SqlClient that the project references, or [NodaTime's](libraries.md#nodatime), [NetTopologySuite's](libraries.md#nettopologysuite) or [System.Text.Json's](libraries.md#json) for a query whose output is `methods` or `repository` where the library types turn that library on, or the one you chose, by the [type override](../../../settings.md#setting-type-overrides), a [column type](../../../settings.md#setting-column-types) or a marker.  Above, `id` is a `bigint`, `placed_at` a `datetime2` and `status` an `nvarchar(20)`, all `NOT NULL`: `@since` takes the type of `placed_at`, and `@status` that of `status`.
- **A column's property can be null** by [which columns can be null](nullability.md#nullability): `customer_name` comes from the optional side of the `LEFT JOIN`, so its property is a `string?`.  A [`-- column:` marker](../../../generator.md#column-marker) can say otherwise, and so can a column's alias with `!` or `?`.
- **A parameter's property can be null** only when its `-- param:` marker says `null`, as `@status` does, or, for a [list parameter](../../parameters.md#list-parameters), when the query tests the list for null, as `@ids IS NULL` does, since SQL Server does not say whether a parameter can be null.  A marker with `null` and no type leaves the type to SQL Server.
- **A column with no name**, as an expression or a literal without an alias, is [SQLSRC266](../../../diagnostics.md#sqlsrc266) when the tool describes the query.  Give it an alias.
- **A query whose rows are a whole table** gets a model named after the table, with the table's schema before its name unless the schema is `dbo`: `dbo.customers` gives `CustomersDto`, and `billing.invoices` gives `BillingInvoicesDto`.  See [table models](../../../generator.md#table-models).
- **A query that returns several result sets** gets an output model of the first alone: see [what can be described](#what-can-be-described).

<a id="limits"></a>

## Limits

<a id="limit-several-result-sets"></a>

### Only the first result set of a query has a model

- **A query that returns several result sets gets a model of the first alone**, and nothing is reported of the others.  Its method reads that one alone.  Give each `SELECT` a `-- name:` of its own, or give the query the output `models` and read the result sets after the first with your own code.

<a id="limit-type-name-unicode"></a>

### SQLSRC228 for a `-- param:` type name with a character that Unicode added after version 3.2

- **A `-- param:` type whose name, written without brackets, holds a character that Unicode added or reclassified after version 3.2** can be read differently by SqlSource and by SQL Server, which reads a name by Unicode 3.2.  `dbo.x・` is refused as [SQLSRC228](../../../diagnostics.md#sqlsrc228), "it is not written as a type name", though SQL Server accepts the name, and `dbo.ꞔ` passes SqlSource and SQL Server refuses it, SQLSRC228 "it is not a type".  Write that part of the name in brackets, as in `-- param: @p dbo.[x・]`.

<a id="limit-parameter-name-unicode"></a>

### SQLSRC231 for a parameter name with a character that Unicode added after version 3.2

- **A parameter whose name holds a character that Unicode added or reclassified after version 3.2** can be read differently by SqlSource and by SQL Server too.  `@x・` is read by SqlSource as the parameter `@x` followed by text, and by SQL Server as one variable, so describing the query is [SQLSRC231](../../../diagnostics.md#sqlsrc231).  Write the name without that character.

<a id="limit-parameter-two-spellings"></a>

### Two parameters where SQL Server sees one

- **A parameter whose name is written in two ways that SQL Server reads as one is two parameters to SqlSource.**  SqlSource compares names ignoring case alone, and SQL Server by its collation, which can also take `é` written as one character and as `e` followed by a combining accent, or the full-width `Ａ` and `A`, for one letter.  A query that writes `@café` both ways, or declares it one way and uses it the other, fails when it is described.  Write a parameter's name the same way each time.
