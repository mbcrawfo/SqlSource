# SQL Server

SqlSource reads SQL Server and Azure SQL files by the `mssql` [dialect](dialects.md) today: every query gets its constant or its token method, with T-SQL's comments, strings and `[bracketed]` names read as SQL Server reads them.  Describing SQL Server queries with the `sqlsource` tool comes in a later release.

```xml
<PropertyGroup>
    <SqlSourceDialect>mssql</SqlSourceDialect>
    <SqlSourceOutput>sql</SqlSourceOutput>
</PropertyGroup>
```

Until SQL Server queries can be described, a query of `mssql` whose [output](settings.md#setting-output) is `models` or `codegen`, the default, is [SQLSRC216](diagnostics.md#sqlsrc216) when you run the tool, and [SQLSRC241](diagnostics.md#sqlsrc241) in the build, since its file's sidecar has no entry for it.  It is not [SQLSRC209](diagnostics.md#sqlsrc209), which is for a dialect the tool does not describe, every row marked No in [database support](dialects.md#database-support).  Set the output to `sql`, as above: the tool leaves the queries out, and the build reports neither.  That is also the fix when SQLSRC209's message suggests the dialect `mssql`.

Under `mssql`, a name that a query declares with `DECLARE @name` is a local variable and not a [query parameter](concepts.md#query-parameter): see [variables declared with `DECLARE`](parameters-and-tokens.md#declared-variables).  The rest of what the dialect changes is on [the dialects page](dialects.md#what-a-dialect-changes).

> **Coming in a later release.**  The tool will describe SQL Server queries, and this page will say what that needs of your database.  See [what works today](README.md#what-works-today).

<a id="types"></a>

## Types

```sql mssql
-- name: GetOrder
SELECT id, placed_at, total, notes, row_version
FROM dbo.orders
WHERE id = @id;
```

Each column and each parameter of a described SQL Server query gets a C# type: the type that Microsoft.Data.SqlClient reads a value of its SQL Server type as.  If `id` is a `bigint`, `placed_at` a `datetime2(7)`, `total` a `decimal(12,2)`, `notes` an `nvarchar(max)` and `row_version` a `rowversion`, they are `long`, `System.DateTime`, `decimal`, `string` and `byte[]`; `@id` is a `long`.  A column that can be null gets its type with `?`: a `bigint` column that can be null is a `long?`, and an `nvarchar` one a `string?`.

The build gives these types to each query whose output is `models` or `codegen` and whose entry in its sidecar is current, and reports an error for a column or parameter that has none, such as [SQLSRC246](diagnostics.md#sqlsrc246) for `geography`.  They are the types of the properties of the query's [models](generator.md#models), and a query with such an error has no models.  To give a type another C# type, use the [type override](settings.md#setting-type-overrides): `date=System.DateOnly`.

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
- **`float(1)` to `float(24)`** are a `real` to SQL Server, so they are a `float` in C#.  `float(25)` to `float(53)`, and `float` without a length, are a `double`.
- **`sql_variant`** is an `object`, whose value is the C# type of the value the row holds: an `int` for an `int`, a `string` for an `nvarchar`.
- **`json`**, SQL Server 2025's type, is a `string`.
- **`vector`** is a `Microsoft.Data.SqlTypes.SqlVector<float>`, a type of Microsoft.Data.SqlClient, which the project references to name it.

<a id="known-issues"></a>

## Known issues

- **A project that uses EF Core 8 or 9 references Microsoft.Data.SqlClient 5.1**, which EF Core's SQL Server provider brings, and gets [SQLSRC267](diagnostics.md#sqlsrc267).  Reference Microsoft.Data.SqlClient 6.1 or later from the project itself, beside the EF Core packages:

  ```xml
  <ItemGroup>
      <PackageReference Include="Microsoft.Data.SqlClient" Version="6.1.7" />
  </ItemGroup>
  ```

- **A `vector` column in a project that references a Microsoft.Data.SqlClient older than 6.1** gets, beside [SQLSRC267](diagnostics.md#sqlsrc267), a compiler error in the generated model, because the `SqlVector<float>` it names is not in that version.  The error goes away with the package update that clears SQLSRC267.
