# SQL Server: NodaTime, NetTopologySuite and JSON

<a id="nodatime"></a>

## NodaTime

```jsonc
// sqlsource.json, in the folder of a project that references NodaTime
{
    "root": true,
    "dialect": "mssql",
    "libraryTypes": {
        "nodaTime": "on",
    },
}
```

```sql mssql
-- name: GetShift -> one
SELECT id, day, opens_at, starts_at, booked_at
FROM dbo.shifts
WHERE id = @id;
```

```csharp
public sealed record GetShiftDto(
    long Id,
    global::NodaTime.LocalDate Day,
    global::NodaTime.LocalTime OpensAt,
    global::NodaTime.LocalDateTime StartsAt,
    global::NodaTime.OffsetDateTime BookedAt);
```

A project that references NodaTime, and turns it on with the [library types](../../library-types-and-json.md#setting-library-types), gets NodaTime's types for the columns and parameters of its queries whose output is `repository` or `methods`.  Above, `day` is a `date`, `opens_at` a `time`, `starts_at` a `datetime2` and `booked_at` a `datetimeoffset`, each `NOT NULL`.

- **On SQL Server, NodaTime is off until a settings file or a marker turns it on**, for a project, a folder, a file or a query.  This holds whether the project references NodaTime or not.
- **Nothing is registered in your application.**  Microsoft.Data.SqlClient reads and sends no NodaTime type.  So the query methods convert each value from the C# type of [the types](types.md#type-table) as they read it, and to that type before they send it.
- **The query methods that use NodaTime need NodaTime 3.1.9 or later.**  An older one is [SQLSRC282](../../../diagnostics.md#sqlsrc282).

| SQL Server type | C# type with NodaTime |
|----|----|
| `date` | `NodaTime.LocalDate` |
| `time` | `NodaTime.LocalTime` |
| `datetime2`, `datetime`, `smalldatetime` | `NodaTime.LocalDateTime` |
| `datetimeoffset` | `NodaTime.OffsetDateTime` |

- **An alias type over one of these types has its C# type**, as [the kinds](types.md#type-table) say.  A CLR type of your own never has one of them.
- **Every other type keeps the type of [the types](types.md#type-table).**
- **Reading a column as one of these types changes no value.**  `datetime2(7)`, `time(7)` and `datetimeoffset(7)` hold the values of NodaTime to the 100 nanoseconds.  What sending a value can change or refuse is below.

- **Only query methods get NodaTime's types.**  A query whose output is `models` keeps the types of [the types](types.md#type-table), and so do its models.  This holds whether Dapper or your own code reads the rows.  A model that such a query shares with a query with a method is [SQLSRC257](../../../diagnostics.md#sqlsrc257): see [library types](../../library-types-and-json.md#setting-library-types).
- **Your own choices win.**  A [type override](../../type-mapping.md#setting-type-overrides), a [column type](../../model-properties.md#setting-column-types) and a `-- param:` or `-- column:` marker give their type over NodaTime's, so a project with `"date": "System.DateOnly"` keeps its `DateOnly` columns.  `"date": "default"` in a nearer settings file, or `-- type-override: date=default` in a file, gives `date` to NodaTime there.
- **`NodaTime.Instant` and `NodaTime.Duration` are yours to choose**, with the type override, a column type or a marker, whether NodaTime is on or not.  The query methods convert them as they convert the types above.
  - `datetime2=NodaTime.Instant` reads a `datetime2` as an `Instant`, taking the value stored as UTC, and sends an `Instant` as its date and time in UTC.
  - `datetimeoffset=NodaTime.Instant` reads the instant of a `datetimeoffset` and drops its offset, and sends an `Instant` at offset zero.
  - `time=NodaTime.Duration` reads a `time` as the `Duration` since midnight.
- **Each NodaTime type is converted from the C# type that some database types are read as**, whichever of those database types you give it to:
  - a `LocalDate`, a `LocalDateTime` or an `Instant` from the `System.DateTime` of a `date`, a `datetime2`, a `datetime` or a `smalldatetime`;
  - a `LocalTime` or a `Duration` from the `System.TimeSpan` of a `time`;
  - an `OffsetDateTime` or an `Instant` from the `System.DateTimeOffset` of a `datetimeoffset`.

  So `datetime2=NodaTime.LocalDate` reads the date of a `datetime2` and drops its time.
- **One of these types given to another database type is [SQLSRC279](../../../diagnostics.md#sqlsrc279)**, such as `datetimeoffset=NodaTime.LocalDateTime`.  The error names the type that it can be converted from.
- **A NodaTime type that none of these is, such as a `ZonedDateTime`, needs [converters](../../converters.md#converters) of your own.**  Without them, Microsoft.Data.SqlClient throws `InvalidCastException` when the query runs.
- **Your converters replace the query methods' own.**  A [converter](../../converters.md#converters) of your project between a NodaTime type and the C# type it is read as, such as one from `System.DateTime` to `NodaTime.Instant`, is used in its direction in place of SqlSource's conversion, with no error, and SqlSource's is kept for the other direction.

What sending a value of NodaTime can change or refuse:

- **Nanoseconds below 100 are dropped**, never rounded: a `LocalTime` of `12:00:00.999999999` is sent as `12:00:00.9999999`, the most that `time(7)` holds, and so for the other types.
- **An `OffsetDateTime` whose offset has seconds is sent with the offset cut to the minute**, with no error, since `DateTimeOffset` holds no seconds of an offset.  `+05:30:15` is sent as `+05:30` with the same local date and time, so the instant stored moves by 15 seconds.
- **An offset beyond 14 hours throws `InvalidOperationException`.**  NodaTime allows such an offset, and `DateTimeOffset` does not.
- **An `OffsetDateTime` whose local date and time has an instant before year 1 or after year 9999 throws `ArgumentOutOfRangeException`.**
- **A value outside the range of `System.DateTime`, before year 1 or after year 9999, throws before the query is sent.**  NodaTime allows such a value.  A `LocalDate` throws `ArgumentOutOfRangeException`, and a `LocalDateTime` or an `Instant` throws `InvalidOperationException`.
- **`datetime` and `smalldatetime` are rounded before they are sent**, as for a `System.DateTime`.  A `datetime` is rounded to its three-hundredths of a second, `.1234567` to `.123`, and a `smalldatetime` to the minute, `12:34:56` to `12:35`.  `23:59:59.999` sent to a `datetime` is the next day's `00:00:00`.
- **A `datetime` or a `smalldatetime` outside its range, or rounded past it, throws before the query is sent**:
  - `SqlTypeException` for a `datetime` before 1753;
  - `OverflowException` for a `smalldatetime` before 1900, or one that rounds past `2079-06-06 23:59`;
  - `OverflowException` for a `datetime` of `9999-12-31 23:59:59.999`.

  `9999-12-31 23:59:59.9999999`, the last value before year 10000, is stored in a `datetime` as `23:59:59.997`.
- **A `Duration` of 24 hours or more, or below zero**, throws `OverflowException`, "SqlDbType.Time overflow", before the query is sent: a `time` holds less than a day.

<a id="nettopologysuite"></a>

## NetTopologySuite

```jsonc
// sqlsource.json, in the folder of a project that references NetTopologySuite and NetTopologySuite.IO.SqlServerBytes
{
    "root": true,
    "dialect": "mssql",
    "libraryTypes": {
        "netTopologySuite": "on",
    },
}
```

```sql mssql
-- name: GetPlace -> one
SELECT id, centre, outline
FROM dbo.places
WHERE id = @id;
```

```csharp
public sealed record GetPlaceDto(
    long Id,
    global::NetTopologySuite.Geometries.Geometry Centre,
    global::NetTopologySuite.Geometries.Geometry Outline);
```

A project gets NetTopologySuite's `Geometry` for the `geography` and `geometry` columns and parameters of its queries whose output is `repository` or `methods`, when all of these are true:

- the project references `NetTopologySuite.IO.SqlServerBytes`;
- it references NetTopologySuite 2.5.0 or later;
- it turns NetTopologySuite on with the [library types](../../library-types-and-json.md#setting-library-types).

Above, `centre` is a `geography` and `outline` a `geometry`, each `NOT NULL`.

- **Reference NetTopologySuite by name.**  `NetTopologySuite.IO.SqlServerBytes` alone brings NetTopologySuite 2.0.0, which is [SQLSRC282](../../../diagnostics.md#sqlsrc282).
- **On SQL Server, NetTopologySuite is off until a settings file or a marker turns it on**, for a project, a folder, a file or a query, whatever the project references.
- **Nothing is registered in your application, and Microsoft.SqlServer.Types is not needed.**  Microsoft.Data.SqlClient reads and sends no NetTopologySuite type, so the query methods read and write each value themselves.
- **The query methods that use NetTopologySuite need NetTopologySuite 2.5.0 or later and `NetTopologySuite.IO.SqlServerBytes` 2.1.0 or later.**  An older one is [SQLSRC282](../../../diagnostics.md#sqlsrc282).

| SQL Server type | C# type with NetTopologySuite |
|----|----|
| `geography`, `geometry` | `NetTopologySuite.Geometries.Geometry` |

Every other type keeps the type of [the types](types.md#type-table).

- **Only query methods get NetTopologySuite's types.**  A query whose output is `models` has no C# type for `geography` or `geometry`, with NetTopologySuite on or off, and is [SQLSRC246](../../../diagnostics.md#sqlsrc246).  Give the query the output `repository` or `methods`, or map the type to a type of Microsoft.SqlServer.Types, as [the types](types.md#type-table) say.
- **A shape is yours to choose**, with the [type override](../../type-mapping.md#setting-type-overrides), a [column type](../../model-properties.md#setting-column-types) or a `-- param:` or `-- column:` marker, whether NetTopologySuite is on or not: `geography=NetTopologySuite.Geometries.Point`, `Polygon` or another of `Geometry`'s subtypes.  A value of another shape throws `InvalidCastException` when it is read.
- **Your converters do not replace these reads and writes.**  A [converter](../../converters.md#converters) of yours between a NetTopologySuite type and another is not used for a `geography` or a `geometry`.  To read one in another way, map it to `Microsoft.SqlServer.Types.SqlGeography` or `SqlGeometry`, and convert from that.
- **Without `NetTopologySuite.IO.SqlServerBytes`, NetTopologySuite turned on is [SQLSRC281](../../../diagnostics.md#sqlsrc281).**  A type override to a NetTopologySuite type then leaves the value to Microsoft.Data.SqlClient, which cannot read it, so the query fails when it runs.
- **On a connection that wraps a `SqlConnection`**, a method reads a spatial column as it does on the `SqlConnection`.  A method with parameters has the [limit](../../query-methods.md#limit-wrapped-connection) of such a connection.

What reading and sending a value of NetTopologySuite keeps or refuses:

- **The SRID is the value's own**, sent with the geometry and read from the column, and Z and M are kept: a point of four dimensions comes back with both.
- **A `geography` needs an SRID.**  SQL Server refuses a `geography` of SRID 0, NetTopologySuite's default, with `SqlException` `Number` 8023, whose message says the value is not a valid instance of `geography`.  Make a `geography`'s values with its SRID, as a `GeometryFactory` of SRID 4326 does.  A `geometry` takes SRID 0.
- **A `geography` polygon's shell is counter-clockwise.**  One whose shell is clockwise throws `ArgumentException` before the query is sent.  Reverse it, with `Reverse()`, before the call.
- **NetTopologySuite's services are taken once.**  The query methods read and write with `NtsGeometryServices.Instance` as it is at the first query that reads or sends a spatial value, and keep it.  An application that uses services of its own, such as another precision model, sets `NtsGeometryServices.Instance` at startup, before that query.
- **EF Core's `UseNetTopologySuite(...)` does not set these services.**  Inside `UseSqlServer(...)`, it configures the services of EF Core, and not `NtsGeometryServices.Instance`.  So the query methods use the defaults of NetTopologySuite unless the application sets it.

<a id="json"></a>

## JSON

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "mssql",
    "libraryTypes": {
        "json": "on",
    },
    "columnTypes": {
        "orders.metadata": { "type": "OrderMetadata", "json": true },
        "orders.document": "System.Text.Json.JsonDocument",
    },
}
```

```sql mssql
-- name: GetOrder -> one
SELECT id, payload, metadata, document
FROM dbo.orders
WHERE id = @id;
```

```csharp
public sealed record GetOrderDto(
    long Id,
    global::System.Text.Json.JsonElement Payload,
    global::MyApp.OrderMetadata Metadata,
    global::System.Text.Json.JsonDocument Document);
```

A `json` column, the type of SQL Server 2025, is a `string`, as [the types](types.md#type-table) say, until the [library types](../../library-types-and-json.md#setting-library-types) turn System.Text.Json on, named `json`.  Then the `json` columns and parameters of the queries whose output is `repository` or `methods`, and those of an alias type over `json`, are `System.Text.Json.JsonElement`.  Above, `payload` is a `json`, and `metadata` and `document` are `nvarchar(max)` columns that hold JSON, each `NOT NULL`.  The column types give `metadata` a class of the project, by [the JSON opt-in](../../library-types-and-json.md#json-opt-in), and `document` a `JsonDocument`.

- **`json` is off until a settings file or a marker turns it on**, for a project, a folder, a file or a query.  Every .NET project has System.Text.Json, so it needs no package.
- **Nothing is registered in your application.**  Microsoft.Data.SqlClient reads `json` and the string types as a `string`, and reads and sends no type of System.Text.Json.  So the query methods convert each value from that `string` as they read it, and to a `string` before they send it.
- **An `nvarchar` column stays a `string` with `json` on**, since nothing says that it holds JSON.

- **Only query methods get `JsonElement`.**  A query whose output is `models` keeps the `string` of [the types](types.md#type-table), and so do its models.  A model that such a query shares with a query with a method is [SQLSRC257](../../../diagnostics.md#sqlsrc257): see [library types](../../library-types-and-json.md#setting-library-types).
- **Your own choices win.**  A [type override](../../type-mapping.md#setting-type-overrides), a [column type](../../model-properties.md#setting-column-types) and a `-- param:` or `-- column:` marker give their type over `JsonElement`.
- **System.Text.Json's types are yours to choose**, with the type override, a column type or a marker, with `json` on or off.  They are `JsonElement`, `JsonDocument`, and `JsonNode`, `JsonObject` and `JsonArray` of `System.Text.Json.Nodes`.  You can choose one for a `json` column, or for any column read as a `string`, such as `nvarchar(max)`, with no opt-in.  Each is parsed from the text of the column, and sent as the text of its JSON, as the parameter's own type.
- **Text that is not JSON throws System.Text.Json's `JsonException` when it is read.**  A string column can hold such text.
- **A `JsonObject` read from a JSON array, or a `JsonArray` read from an object, throws `InvalidCastException`.**
- **A `JsonDocument` is yours to dispose.**  Each one that a method returns, in its row's property, holds memory that is returned only when the document is disposed: dispose it, with `using` or once you have read it.  One that you send stays yours.  A `JsonElement` needs no disposal.
- **A class of your own in a `json` column is read and sent through System.Text.Json's serializer**, with `json` on or off.  So is one in a string column that holds JSON by [the JSON opt-in](../../library-types-and-json.md#json-opt-in), such as `metadata` above.  What a class is, the options of the serializer, and the converters that replace it, are in [a class in a JSON column](../../library-types-and-json.md#json-class).
- **Without the opt-in, a class given to a string column is given to Microsoft.Data.SqlClient as it is**, unless it has converters.  Microsoft.Data.SqlClient then throws `InvalidCastException` when the query runs.
- **Your converters replace the query methods' own.**  A [converter](../../converters.md#converters) of your project between one of System.Text.Json's types and `string` is used in its direction in place of SqlSource's conversion, with no error, and SqlSource's is kept for the other direction.
- **A JSON `null` is not a SQL `NULL`.**  A column that holds the JSON `null` is read as a `JsonElement` of the kind `Null`, and as a `JsonDocument` whose root is one, whether its property can be null or not.
- **A `JsonNode`, a `JsonObject` and a `JsonArray` cannot hold a JSON `null`.**  Into a property that can be null it is null.  Into one that cannot, it throws `InvalidCastException`, "Column 'node' holds a JSON null.", as a class does.
- **A SQL `NULL` gives its property null**, or throws as any column does.
- **A `json` column refuses a value that is not an object or an array.**  The `json` of SQL Server 2025 takes no JSON text that is a string, a number, `true`, `false` or `null` alone.  It refuses one with error 13609, "JSON text is not properly formatted", thrown as a `SqlException` when the query runs.  So these cannot be sent to a `json` column: a `JsonElement` of the kind `Null`, one that holds a number, and a class whose JSON is not an object or an array.  An `nvarchar(max)` column takes them.
- **`json` keeps the structure, not the text.**  It stores JSON in a form of its own:
  - the white space between its tokens is dropped;
  - escapes such as `\u0041` are written as their characters;
  - of two keys of one name, the first alone is kept, so `{"a":1,"a":2}` reads back as `{"a":1}`.

  So a value reads back equal in structure, not in text: compare JSON read back by its structure.  An `nvarchar(max)` column keeps the text as it was sent.
