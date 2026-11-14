# SQL Server: types

<a id="types"></a>

## Types

```sql mssql
-- name: GetOrder
SELECT id, placed_at, total, notes, row_version
FROM dbo.orders
WHERE id = @id;
```

Each column and each parameter of a described SQL Server query gets a C# type: the type that Microsoft.Data.SqlClient reads a value of its SQL Server type as.  If `id` is a `bigint`, `placed_at` a `datetime2(7)`, `total` a `decimal(12,2)`, `notes` an `nvarchar(max)` and `row_version` a `rowversion`, they are `long`, `System.DateTime`, `decimal`, `string` and `byte[]`; `@id` is a `long`.  A column that can be null gets its type with `?`: a `bigint` column that can be null is a `long?`, and an `nvarchar` one a `string?`.

The build gives these types to each query whose output is `models`, `methods` or `repository` and whose entry in its sidecar is current, and reports an error for a column or parameter that has none, such as [SQLSRC246](../../../diagnostics.md#sqlsrc246) for `geography`.  They are the types of the properties of the query's [models](index.md#models), and a query with such an error has no models.  To give a type another C# type, use the [type override](../../../settings.md#setting-type-overrides), `"date": "System.DateOnly"` in a settings file's `typeOverrides`.  To give one column another, use a [column type](../../../settings.md#setting-column-types), `"users.id": "MyApp.UserId"` in its `columnTypes`, and one parameter, its [`-- param:` marker](../../parameters.md#param-marker), `-- param: @id -> MyApp.UserId`.  A query method reads and sends that C# type as it is, so a type of yours that Microsoft.Data.SqlClient does not read, such as an id or an `Email` over an alias type, needs [converters](../../../settings.md#converters) to and from the type of the table below.  Rows read through Dapper with a type of your choice need a type handler: see [the recipe](../../../recipes.md#dapper-with-sql-server).  A project that references NodaTime can give the date and time types of its queries whose output is `methods` or `repository` NodaTime's types: see [NodaTime](libraries.md#nodatime).  One that references `NetTopologySuite.IO.SqlServerBytes` can give their `geography` and `geometry` NetTopologySuite's: see [NetTopologySuite](libraries.md#nettopologysuite).  A project can read its JSON, in `json` or in a string column, as System.Text.Json's types or as classes of its own: see [JSON](libraries.md#json).

<a id="which-sqlclient"></a>

## Which version of Microsoft.Data.SqlClient

Microsoft.Data.SqlClient 6.1 and 7 read every type as the same C# type, so the types are those of the table below whatever version of 6.1 or later the project references, directly or through another package:

- **Microsoft.Data.SqlClient 6.1 or 7** gets the table's types.
- **Microsoft.Data.SqlClient older than 6.1** is [SQLSRC267](../../../diagnostics.md#sqlsrc267), an error.  Update it to 6.1 or later.  EF Core 8 and 9 bring an older one: see [limits](#limit-ef-core-sqlclient).  Until it is updated, every type but `vector` gets the table's type, and no query gets its method; a `vector` column or parameter, whose C# type is first in Microsoft.Data.SqlClient 6.1, is [SQLSRC270](../../../diagnostics.md#sqlsrc270) at its query.
- **Microsoft.Data.SqlClient 8 or later** gets the types of Microsoft.Data.SqlClient 7, with the information [SQLSRC268](../../../diagnostics.md#sqlsrc268), since this version of SqlSource does not know a newer one.
- **No Microsoft.Data.SqlClient**, as in a library that holds the queries and leaves the driver to the application, gets the table's types too, but for `vector`, whose C# type is Microsoft.Data.SqlClient's own: it is [SQLSRC269](../../../diagnostics.md#sqlsrc269) until the project references Microsoft.Data.SqlClient.  Such a project gets models alone: give its queries the output `models`, since a query method needs the driver ([SQLSRC273](../../../diagnostics.md#sqlsrc273)).  The older `System.Data.SqlClient` package is not Microsoft.Data.SqlClient, and a project that references it alone has no Microsoft.Data.SqlClient.

<a id="type-table"></a>

## The types

The names are SQL Server's, which are the [type override's](../../../settings.md#setting-type-overrides) keys, with, in parentheses, the other one-word names that are also keys.  A type's length, precision or scale does not change its C# type: `nvarchar(50)` and `nvarchar(max)` are both `string`, and `decimal(12,2)` is a `decimal`.

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
| `hierarchyid`, `geography`, `geometry` | None: SQLSRC246.  Reference the package `Microsoft.SqlServer.Types` and map the type with the type override: `hierarchyid=Microsoft.SqlServer.Types.SqlHierarchyId`, `geography=Microsoft.SqlServer.Types.SqlGeography`, `geometry=Microsoft.SqlServer.Types.SqlGeometry`.  A query method then reads the column as that type, and sends the parameter as its SQL Server type.  For `geography` and `geometry`, the query methods of a project that turns [NetTopologySuite](libraries.md#nettopologysuite) on get `NetTopologySuite.Geometries.Geometry` instead |

A type that the table does not name gets its C# type by what it is:

| Kind | C# type |
|----|----|
| An alias type, `CREATE TYPE dbo.Email FROM nvarchar(320)` | Its base type's C# type: `dbo.Email` is a `string`.  To give it another, map it with the type override by its schema and name, `dbo.Email=MyApp.Email`.  `sysname` is a `string` |
| A CLR type of your own assembly, or a type of a newer SQL Server | None: SQLSRC246.  Map it with the type override to the type the application reads it as; the message names the key to write |

<a id="type-notes"></a>

## What to know about some types

- **`timestamp`** is SQL Server's row version, which it also calls `rowversion`, and not a time, so it is a `byte[]`.  PostgreSQL's `timestamp` is a time: see [type overrides](../../../settings.md#setting-type-overrides) for an entry in a project with both.
- **`date` and `time`** are a `System.DateTime` and a `System.TimeSpan`, and never a `System.DateOnly` or a `System.TimeOnly`, on Microsoft.Data.SqlClient 6.1 and 7.
- **`date`, `datetime`, `datetime2` and `smalldatetime`** are read as a `System.DateTime` whose `Kind` is `Unspecified`, and `datetimeoffset` keeps its offset.  A query method sends a `datetime2` parameter as a `datetime2`, with all seven digits: see [how a parameter is sent](query-methods.md#method-parameters).  Dapper sends a `DateTime` parameter as a `datetime`, to 1/300 of a second, so a `datetime2` loses digits unless it is sent as a `datetime2`: see [the recipe](../../../recipes.md#dapper-with-sql-server).
- **`float(1)` to `float(24)`** are a `real` to SQL Server, so they are a `float` in C#.  `float(25)` to `float(53)`, and `float` without a length, are a `double`.
- **`sql_variant`** is an `object`, whose value is the C# type of the value the row holds: an `int` for an `int`, a `string` for an `nvarchar`.
- **`json`**, SQL Server 2025's type, is a `string`, or a `JsonElement` where the library types turn System.Text.Json on: see [JSON](libraries.md#json).
- **`vector`** is a `Microsoft.Data.SqlTypes.SqlVector<float>`, a type of Microsoft.Data.SqlClient, which the project references to name it.

<a id="limits"></a>

## Limits

<a id="limit-ef-core-sqlclient"></a>

### SQLSRC267 in a project that uses EF Core 8 or 9

- **A project that uses EF Core 8 or 9 references Microsoft.Data.SqlClient 5.1**, which EF Core's SQL Server provider brings, and gets [SQLSRC267](../../../diagnostics.md#sqlsrc267).  Reference Microsoft.Data.SqlClient 6.1 or later from the project itself, beside the EF Core packages:

  ```xml
  <ItemGroup>
      <PackageReference Include="Microsoft.Data.SqlClient" Version="6.1.7" />
  </ItemGroup>
  ```
