# PostgreSQL: NodaTime, NetTopologySuite and JSON

<a id="nodatime"></a>

## NodaTime

```sql postgres
-- name: GetShift -> one
SELECT id, starts_at, day, length, slots
FROM shifts
WHERE id = @id;
```

```csharp
public sealed record GetShiftDto(
    long Id,
    global::NodaTime.Instant StartsAt,
    global::NodaTime.LocalDate Day,
    global::NodaTime.Period Length,
    global::NpgsqlTypes.NpgsqlRange<global::NodaTime.Instant> Slots);
```

A project that references `Npgsql.NodaTime` gets NodaTime's types with no setting.  `Npgsql.NodaTime` is Npgsql's plugin for NodaTime.  The types go to the columns and query parameters of each query whose output is `repository` or `methods`, and the plugin reads and sends them.  In the sample, `starts_at` is a `timestamptz`, `day` a `date`, `length` an `interval` and `slots` a `tstzrange`, and each is `NOT NULL`.

The application must call `UseNodaTime()` on its data source, or Npgsql throws `InvalidCastException` when the query runs: see [what a method reads and throws](query-methods.md#method-results).  NodaTime is on by default in such a project.  The [library types](../../library-types-and-json.md#setting-library-types) can turn it off for a project, a folder, a file or a query.  The types are the same on Npgsql 8, 9 and 10.  Reference the `Npgsql.NodaTime` of the same major version as your Npgsql.

| PostgreSQL type | C# type with NodaTime |
|----|----|
| `timestamptz` (`timestamp with time zone`) | `NodaTime.Instant` |
| `timestamp` (`timestamp without time zone`) | `NodaTime.LocalDateTime` |
| `date` | `NodaTime.LocalDate` |
| `time` (`time without time zone`) | `NodaTime.LocalTime` |
| `timetz` (`time with time zone`) | `NodaTime.OffsetTime` |
| `interval` | `NodaTime.Period` |
| `tstzrange`, `tsrange`, `daterange` | `NpgsqlTypes.NpgsqlRange<NodaTime.Instant>`, `NpgsqlTypes.NpgsqlRange<NodaTime.LocalDateTime>`, `NpgsqlTypes.NpgsqlRange<NodaTime.LocalDate>` |
| `tstzmultirange`, `tsmultirange`, `datemultirange` | An array of the range's type: `NpgsqlTypes.NpgsqlRange<NodaTime.Instant>[]` and so on |

An array of one of these types is an array of its C# type: a `timestamptz[]` is a `NodaTime.Instant[]`.  A domain over one of these types has its C# type, as [the kinds](types.md#type-table) say.  Every other type keeps its C# type from [the type table](types.md#type-table).

- **Only query methods get NodaTime's types.**  A query whose output is `models` keeps the types of [the type table](types.md#type-table), and so do its models.  This holds whether Dapper or your own code reads the rows.  If such a query shares a model with a query that has a method, that model is [SQLSRC257](../../../diagnostics.md#sqlsrc257): see [library types](../../library-types-and-json.md#setting-library-types).
- **Your own choices win.**  A [type override](../../type-mapping.md#setting-type-overrides), a [column type](../../model-properties.md#setting-column-types) and a `-- param:` or `-- column:` marker each give their type over NodaTime's.  So a project with `"timestamptz": "System.DateTimeOffset"` keeps its `DateTimeOffset` columns.  `"timestamptz": "default"` in a nearer settings file gives `timestamptz` to NodaTime in that folder.  `-- type-override: timestamptz=default` in a file does the same in that file.
- **The plugin reads other types too.**  The type override, a column type or a marker can give one of these types.  A query method reads and sends each through the plugin:
  - a `timestamptz` as a `NodaTime.ZonedDateTime`, read in the zone UTC, a `NodaTime.OffsetDateTime`, read at offset 0, a `System.DateTimeOffset` or a `System.DateTime`;
  - a `timestamp` as a `System.DateTime`;
  - an `interval` as a `NodaTime.Duration`;
  - a `tstzrange` as a `NodaTime.Interval`, and a `daterange` as a `NodaTime.DateInterval`, with their multiranges as arrays of them.
- **A `ZonedDateTime` or an `OffsetDateTime` is sent in UTC alone.**  On Npgsql 8 and 10 alike, a value in another zone, or at another offset, throws `ArgumentException`.  Then all of these are true:
  - The query does not run.
  - The connection is closed, so a transaction open on it is lost.
  - The next command on the connection throws `InvalidOperationException`, so open a new connection to go on.
- **Convert such a value to UTC before the call**, with `value.WithZone(DateTimeZone.Utc)` or `value.WithOffset(Offset.Zero)`.  A value in UTC leaves the connection open.
- **An `interval` with months or years cannot be read as a `NodaTime.Duration`.**  The plugin throws `InvalidCastException`, whose message says to read it as a `NodaTime.Period`, the default.
- **`NodaTime.Interval` and `NodaTime.DateInterval` cannot hold every range.**  That is why the defaults are `NpgsqlRange<T>`.
- **An empty range throws when it is read as one of them.**  The exception depends on the type:
  - a `tstzrange` read as an `Interval` throws `ArgumentOutOfRangeException`;
  - a `daterange` read as a `DateInterval` throws `ArgumentException`.
- **A read and a write move a `tstzrange` whose start is excluded.**  An `Interval` includes its start and excludes its end.  So the range `(a,b]` is read as the `Interval` `[a+1ns, b+1ns)`.  Sent back, it is stored as `[a,b)`, since PostgreSQL keeps microseconds.  A multirange never holds an empty range, but its ranges move the same way.
- **Without the plugin, a project gets none of NodaTime's types by default.**  This holds for a project that references `NodaTime` alone.  A type override, a column type or a marker can still name one of the types, such as `timestamptz=NodaTime.Instant`.  The property then gets that type, as it would get any type.  The query method asks Npgsql for the type, and Npgsql throws `InvalidCastException` when the query runs.  To fix it, add `Npgsql.NodaTime`, and call `UseNodaTime()` as [what a method reads and throws](query-methods.md#method-results) says.

<a id="nettopologysuite"></a>

## NetTopologySuite

```sql postgres
-- name: GetPlace -> one
SELECT id, outline, centre, stops
FROM places
WHERE id = @id;
```

```csharp
public sealed record GetPlaceDto(
    long Id,
    global::NetTopologySuite.Geometries.Geometry Outline,
    global::NetTopologySuite.Geometries.Geometry Centre,
    global::NetTopologySuite.Geometries.Geometry[] Stops);
```

A project that references `Npgsql.NetTopologySuite` gets NetTopologySuite's `Geometry` for PostGIS's types with no setting.  `Npgsql.NetTopologySuite` is Npgsql's plugin for NetTopologySuite.  The type goes to the columns and query parameters of each query whose output is `repository` or `methods`, and the plugin reads and sends it.  In the sample, `outline` is a `geometry(Polygon,4326)`, `centre` a `geography(Point,4326)` and `stops` a `geometry[]`, and each is `NOT NULL`.

The application must call `UseNetTopologySuite()` on its data source, as [what a method reads and throws](query-methods.md#method-results) says.  NetTopologySuite is on by default in such a project.  The [library types](../../library-types-and-json.md#setting-library-types) can turn it off for a project, a folder, a file or a query.  Reference the `Npgsql.NetTopologySuite` of the same major version as your Npgsql.  The project's NetTopologySuite must be 2.5.0 or later: an older one is [SQLSRC282](../../../diagnostics.md#sqlsrc282).

| PostgreSQL type | C# type with NetTopologySuite |
|----|----|
| `geometry`, `geography`, with or without a modifier such as `(Point,4326)` | `NetTopologySuite.Geometries.Geometry` |

SqlSource knows these types by their name, whatever schema PostGIS was created in.  An array of one is a `NetTopologySuite.Geometries.Geometry[]`, and a `NULL` element of the array is null.  A domain over one has its C# type, as [the kinds](types.md#type-table) say.  Every other type keeps its C# type from [the type table](types.md#type-table).

- **Only query methods get NetTopologySuite's types.**  A query whose output is `models` has no C# type for a PostGIS type, with the plugin or without it.  Such a query is [SQLSRC246](../../../diagnostics.md#sqlsrc246).  Give the query the output `repository` or `methods`.  Or map the type with the [type override](../../type-mapping.md#setting-type-overrides) to the C# type that your code reads it as.
- **A shape is yours to choose.**  The type override, a [column type](../../model-properties.md#setting-column-types) or a `-- param:` or `-- column:` marker can name `NetTopologySuite.Geometries.Point`, `Polygon` or another subtype of `Geometry`.  The plugin reads and sends each of them.  A value of another shape throws `InvalidCastException` when it is read, with the message "Unable to cast object of type 'NetTopologySuite.Geometries.LineString' to type 'NetTopologySuite.Geometries.Point'."
- **A type override names a PostGIS type with its schema**, `public.geometry`, and never `geometry` alone, which is SQL Server's.  Every column of `geometry` has that one key, whatever its modifier.  To give one query a shape, write a `-- type-override:` inside the query: `-- type-override: public.geometry = NetTopologySuite.Geometries.Point`.  To give one column a shape, use a column type.
- **The SRID is kept, written and read, and so are Z and M.**  A point of four dimensions comes back with both.
- **A `geometry` with no SRID is read with the SRID -1.**  PostGIS stores such a `geometry` as SRID 0.  This holds whether your code or the server wrote it, so compare SRIDs knowing that.  A `geography` sent with SRID 0 is stored as 4326.
- **A parameter of a PostGIS type is described and checked as any other parameter.**  A `-- param:` marker writes its type as a column definition does: `-- param: @outline geometry(Polygon,4326)`.  The marker can also leave out the modifier: `-- param: @outline geometry`.  The parameter's type is the same in both, since PostgreSQL gives a parameter no modifier.
- **Without the plugin, a project gets none of NetTopologySuite's types by default.**  This holds for a project that references NetTopologySuite alone.  A type override, a column type or a marker can still name one of the types, and the property then gets that type.  The query method asks Npgsql for the type, and Npgsql throws `InvalidCastException` when the query runs.  To fix it, add `Npgsql.NetTopologySuite`, and call `UseNetTopologySuite()`.

<a id="json"></a>

## JSON

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "libraryTypes": {
        "json": "on",
    },
    "columnTypes": {
        "orders.metadata": "OrderMetadata",
        "orders.document": "System.Text.Json.JsonDocument",
    },
}
```

```sql postgres
-- name: GetOrder -> one
SELECT id, payload, history, metadata, document
FROM orders
WHERE id = @id;
```

```csharp
public sealed record GetOrderDto(
    long Id,
    global::System.Text.Json.JsonElement Payload,
    global::System.Text.Json.JsonElement[] History,
    global::MyApp.OrderMetadata Metadata,
    global::System.Text.Json.JsonDocument Document);
```

A `json` or `jsonb` column is a `string` by default, as [the type table](types.md#type-table) says.  System.Text.Json, named `json` in the [library types](../../library-types-and-json.md#setting-library-types), changes that.  When `json` is on, each `json` and `jsonb` column and query parameter of a query whose output is `repository` or `methods` is a `System.Text.Json.JsonElement`.  An array of one, a `jsonb[]`, is a `JsonElement[]`.

In the sample, `payload` and `metadata` are `jsonb`, `document` is a `json` and `history` is a `jsonb[]`, and each is `NOT NULL`.  The column types of the settings file give `metadata` a class of the project and `document` a `JsonDocument`.

`json` is off by default, and the library types can turn it on for a project, a folder, a file or a query.  Every .NET project has System.Text.Json, so it needs no package.  Npgsql reads and sends `JsonElement` with nothing to register on the data source.  `jsonpath` stays a `string`.

| PostgreSQL type | C# type with `json` on |
|----|----|
| `json`, `jsonb` | `System.Text.Json.JsonElement` |
| An array of one, `jsonb[]` | `System.Text.Json.JsonElement[]` |

A domain over one of these types has its C# type, as [the kinds](types.md#type-table) say.  Every other type keeps its C# type from [the type table](types.md#type-table).

- **Only query methods get `JsonElement`.**  A query whose output is `models` keeps the `string` of [the type table](types.md#type-table), and so do its models.  If such a query shares a model with a query that has a method, that model is [SQLSRC257](../../../diagnostics.md#sqlsrc257): see [library types](../../library-types-and-json.md#setting-library-types).
- **Your own choices win.**  A [type override](../../type-mapping.md#setting-type-overrides), a [column type](../../model-properties.md#setting-column-types) and a `-- param:` or `-- column:` marker give their type over `JsonElement`.
- **System.Text.Json's other types are yours to choose**, with `json` on or off.  For a `json` or `jsonb` column or query parameter, the type override, a column type or a marker can name `JsonDocument`.  It can also name `JsonNode`, `JsonObject` or `JsonArray`, of `System.Text.Json.Nodes`.
- **On Npgsql 8 and 9, the query methods convert the three node types themselves.**  Npgsql 8, 9 and 10 read and send `JsonElement` and `JsonDocument`, and Npgsql 10 also reads and sends `JsonNode`, `JsonObject` and `JsonArray`.  Npgsql 8 and 9 read those three only after `EnableDynamicJson()` on the data source.  So on these versions the query methods convert them from and to the JSON's text, and the data source needs nothing.  On these versions, a `JsonObject` read from a JSON array, or a `JsonArray` read from an object, throws `InvalidCastException`.
- **A `JsonDocument` is yours to dispose.**  A method can return a `JsonDocument` in a property of its row.  Each such document holds memory that it gives back only when it is disposed.  Dispose it with `using`, or once you have read it.  A `JsonDocument` that you send stays yours.  A `JsonElement` needs no disposal.
- **A class of your own in a `json` or `jsonb` column goes through System.Text.Json's serializer.**  `OrderMetadata` in the sample is such a class, and the query method reads and sends it with `json` on or off.  You register nothing on the data source: the class needs no `EnableDynamicJson()`, which Npgsql's own route for a class needs.  An array of a class, such as `OrderMetadata[]` for a `jsonb[]`, is read and sent element by element.  A `text` or `varchar` column that holds JSON takes a class by [the JSON opt-in](../../library-types-and-json.md#json-opt-in).  [A class in a JSON column](../../library-types-and-json.md#json-class) says what a class is, and gives the serializer's options and the converters that replace it.
- **A JSON `null` is not a SQL `NULL`.**  A column can hold the JSON `null`, `'null'::jsonb`.  It is read as a `JsonElement` of the kind `Null`, or as a `JsonDocument` whose root is such an element.  This holds whether its property is nullable or not.  A SQL `NULL` gives its property null, or throws as for any column.
- **A `JsonNode`, a `JsonObject` or a `JsonArray` cannot hold a JSON `null`.**  If the property is nullable, it is null.  If the property is not nullable, the query method throws `InvalidCastException`, "Column 'node' holds a JSON null.", as it does for a class.
- **In a `JsonNode[]`, an element that is a JSON `null` depends on the version of Npgsql.**  On Npgsql 8 and 9, a `JsonNode[]` throws the same `InvalidCastException` for it, and a `JsonNode?[]` gives null.  On Npgsql 10, a `JsonNode[]` gives such an element as null, unchecked, as for a `NULL` element.  Write `JsonNode?[]` where an element can be a JSON `null`.
- **`jsonb` keeps the structure, not the text.**  It stores JSON in a form of its own.  Its white space is its own, and the keys of an object are in its own order.  Of two keys of one name, it keeps only the last.  So a value reads back equal in structure, not in text: compare JSON that you read back by its structure.  `json` keeps the text as it was sent.
