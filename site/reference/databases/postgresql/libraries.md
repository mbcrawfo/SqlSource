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

A project that references `Npgsql.NodaTime`, Npgsql's plugin for NodaTime, gets NodaTime's types for the columns and parameters of its queries whose output is `methods` or `repository`, read and sent by the plugin.  Above, `starts_at` is a `timestamptz`, `day` a `date`, `length` an `interval` and `slots` a `tstzrange`, each `NOT NULL`.  The [library types](../../../settings.md#setting-library-types) turn NodaTime off for a project, a folder, a file or a query.  The types are the same on Npgsql 8, 9 and 10: reference the `Npgsql.NodaTime` of the same major as your Npgsql.

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

An array of one of these types is an array of its C# type, `NodaTime.Instant[]` for `timestamptz[]`, and a domain over one has its C# type, as [the kinds](types.md#type-table) say.  Every other type keeps the type of [the types](types.md#type-table).

- **Only query methods get NodaTime's types.**  A query whose output is `models` keeps the types of [the types](types.md#type-table), and so do its models, whether rows are read through Dapper or your own code.  A model that such a query shares with a query with a method is [SQLSRC257](../../../diagnostics.md#sqlsrc257): see [library types](../../../settings.md#setting-library-types).
- **Your own choices win.**  A [type override](../../../settings.md#setting-type-overrides), a [column type](../../../settings.md#setting-column-types) and a `-- param:` or `-- column:` marker give their type over NodaTime's, so a project with `"timestamptz": "System.DateTimeOffset"` keeps its `DateTimeOffset` columns.  `"timestamptz": "default"` in a nearer settings file, or `-- type-override: timestamptz=default` in a file, gives `timestamptz` to NodaTime there.
- **The plugin reads other types too**, which the type override, a column type or a marker can give: a `timestamptz` as a `NodaTime.ZonedDateTime`, read in the zone UTC, a `NodaTime.OffsetDateTime`, read at offset 0, a `System.DateTimeOffset` or a `System.DateTime`; a `timestamp` as a `System.DateTime`; an `interval` as a `NodaTime.Duration`; a `tstzrange` as a `NodaTime.Interval` and a `daterange` as a `NodaTime.DateInterval`, with their multiranges as arrays of them.  A query method reads and sends each through the plugin.
- **A `ZonedDateTime` or an `OffsetDateTime` is sent in UTC alone.**  One in another zone, or at another offset, throws `ArgumentException`, the query does not run, and the connection is closed: a transaction open on it is lost, and the next command on it throws `InvalidOperationException`, so open a new connection to go on.  Npgsql 8 and 10 do the same.  Convert the value to UTC before the call, with `value.WithZone(DateTimeZone.Utc)` or `value.WithOffset(Offset.Zero)`; a value in UTC leaves the connection open.
- **An `interval` with months or years** read as a `NodaTime.Duration` throws `InvalidCastException`, which says to read it as a `NodaTime.Period`, the default.
- **`NodaTime.Interval` and `NodaTime.DateInterval` cannot hold every range**, which is why the defaults are `NpgsqlRange<T>`.  An empty range throws when it is read: `ArgumentOutOfRangeException` for a `tstzrange` as an `Interval`, and `ArgumentException` for a `daterange` as a `DateInterval`.  A `tstzrange` whose start is excluded, `(a,b]`, is read as the `Interval` `[a+1ns, b+1ns)`, since an `Interval` includes its start and excludes its end, and sent back it is stored as `[a,b)`, since PostgreSQL keeps microseconds: a read and a write move the range.  A multirange never holds an empty range, but its ranges move the same way.
- **Without the plugin**, a project that references `NodaTime` alone gets none of NodaTime's types by default.  A type override, a column type or a marker that names one, `timestamptz=NodaTime.Instant`, gives the property that type, as it would any type, and the query method asks Npgsql for it, which throws `InvalidCastException` when the query runs.  Add `Npgsql.NodaTime`, and call `UseNodaTime()` as [what a method reads and throws](query-methods.md#method-results) says.

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

A project that references `Npgsql.NetTopologySuite`, Npgsql's plugin for NetTopologySuite, gets NetTopologySuite's `Geometry` for PostGIS's types in the columns and parameters of its queries whose output is `methods` or `repository`, read and sent by the plugin.  Above, `outline` is a `geometry(Polygon,4326)`, `centre` a `geography(Point,4326)` and `stops` a `geometry[]`, each `NOT NULL`.  The [library types](../../../settings.md#setting-library-types) turn NetTopologySuite off for a project, a folder, a file or a query.  Reference the `Npgsql.NetTopologySuite` of the same major as your Npgsql, with NetTopologySuite 2.5.0 or later, as [SQLSRC282](../../../diagnostics.md#sqlsrc282) says; the application calls `UseNetTopologySuite()` on its data source, as [what a method reads and throws](query-methods.md#method-results) says.

| PostgreSQL type | C# type with NetTopologySuite |
|----|----|
| `geometry`, `geography`, with or without a modifier such as `(Point,4326)` | `NetTopologySuite.Geometries.Geometry` |

They are known by their name in whatever schema PostGIS was created.  An array of one is a `NetTopologySuite.Geometries.Geometry[]`, whose `NULL` element is null, and a domain over one has its C# type, as [the kinds](types.md#type-table) say.  Every other type keeps the type of [the types](types.md#type-table).

- **Only query methods get NetTopologySuite's types.**  A query whose output is `models` has no C# type for a PostGIS type, with the plugin or without it, and is [SQLSRC246](../../../diagnostics.md#sqlsrc246): give the query the output `methods` or `repository`, or map the type with the [type override](../../../settings.md#setting-type-overrides) to the C# type your code reads it as.
- **A shape is yours to choose**, with the type override, a [column type](../../../settings.md#setting-column-types) or a `-- param:` or `-- column:` marker: `NetTopologySuite.Geometries.Point`, `Polygon` or another of `Geometry`'s subtypes, which the plugin reads and sends.  A type override names the type with its schema, `public.geometry`, and never `geometry` alone, which is SQL Server's.  Every column of `geometry` has that one key, whatever its modifier, so give one query a shape with a `-- type-override:` inside it, `-- type-override: public.geometry = NetTopologySuite.Geometries.Point`, or one column with a column type.  A value of another shape throws `InvalidCastException`, "Unable to cast object of type 'NetTopologySuite.Geometries.LineString' to type 'NetTopologySuite.Geometries.Point'.", when it is read.
- **The SRID is kept**, written and read, and so are Z and M: a point of four dimensions comes back with both.  A `geometry` with no SRID, which PostGIS stores as SRID 0, is read with the SRID -1, whether your code or the server wrote it, so compare SRIDs knowing that.  A `geography` sent with SRID 0 is stored as 4326.
- **A parameter's type** is described and checked as any other's.  A `-- param:` marker writes it as a column definition does, `-- param: @outline geometry(Polygon,4326)`, or without its modifier, `-- param: @outline geometry`: the parameter's type is the same, since PostgreSQL gives a parameter no modifier.
- **Without the plugin**, a project that references NetTopologySuite alone gets none of its types by default.  A type override, a column type or a marker that names one gives the property that type, and the query method asks Npgsql for it, which throws `InvalidCastException` when the query runs.  Add `Npgsql.NetTopologySuite`, and call `UseNetTopologySuite()`.

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

A `json` or `jsonb` column is a `string`, as [the types](types.md#type-table) say, until the [library types](../../../settings.md#setting-library-types) turn System.Text.Json on, named `json`.  Then the `json` and `jsonb` columns and parameters of its queries whose output is `methods` or `repository` are `System.Text.Json.JsonElement`, and an array of one, `jsonb[]`, a `JsonElement[]`.  Above, `payload` and `metadata` are `jsonb`, `document` a `json` and `history` a `jsonb[]`, each `NOT NULL`, and the column types give `metadata` a class of the project and `document` a `JsonDocument`.  `json` is off until a settings file or a marker turns it on, for a project, a folder, a file or a query.  Every .NET project has System.Text.Json, so it needs no package, and Npgsql reads and sends `JsonElement` with nothing to register on the data source.  `jsonpath` stays a `string`.

| PostgreSQL type | C# type with `json` on |
|----|----|
| `json`, `jsonb` | `System.Text.Json.JsonElement` |
| An array of one, `jsonb[]` | `System.Text.Json.JsonElement[]` |

A domain over one of these types has its C# type, as [the kinds](types.md#type-table) say.  Every other type keeps the type of [the types](types.md#type-table).

- **Only query methods get `JsonElement`.**  A query whose output is `models` keeps the `string` of [the types](types.md#type-table), and so do its models.  A model that such a query shares with a query with a method is [SQLSRC257](../../../diagnostics.md#sqlsrc257): see [library types](../../../settings.md#setting-library-types).
- **Your own choices win.**  A [type override](../../../settings.md#setting-type-overrides), a [column type](../../../settings.md#setting-column-types) and a `-- param:` or `-- column:` marker give their type over `JsonElement`.
- **System.Text.Json's other types are yours to choose**, with the type override, a column type or a marker, with `json` on or off: `JsonDocument`, and `JsonNode`, `JsonObject` and `JsonArray` of `System.Text.Json.Nodes`, for a `json` or `jsonb` column or parameter.  Npgsql reads and sends `JsonElement` and `JsonDocument` on Npgsql 8, 9 and 10, and `JsonNode`, `JsonObject` and `JsonArray` on Npgsql 10.  Npgsql 8 and 9 read those three only after `EnableDynamicJson()` on the data source, so there the query methods convert them themselves, from and to the JSON's text, and the data source needs nothing.  There a `JsonObject` read from a JSON array, or a `JsonArray` from an object, throws `InvalidCastException`.
- **A `JsonDocument` is yours to dispose.**  Each one that a method returns, in its row's property, holds memory that is returned only when the document is disposed: dispose it, with `using` or once you have read it.  One that you send stays yours.  A `JsonElement` needs no disposal.
- **A class of your own** in a `json` or `jsonb` column, such as `OrderMetadata` above, is read and sent through System.Text.Json's serializer, with `json` on or off, and with nothing to register on the data source: no `EnableDynamicJson()`, which Npgsql's own route for a class needs.  An array of a class, `OrderMetadata[]` for a `jsonb[]`, is read and sent element by element.  A `text` or `varchar` column that holds JSON takes a class by [the JSON opt-in](../../../settings.md#json-opt-in).  What a class is, the serializer's options, and the converters that replace it, are in [a class in a JSON column](../../../settings.md#json-class).
- **A JSON `null` is not a SQL `NULL`.**  A column that holds the JSON `null`, `'null'::jsonb`, is read as a `JsonElement` of the kind `Null`, and as a `JsonDocument` whose root is one, whether its property can be null or not.  A `JsonNode`, a `JsonObject` and a `JsonArray` cannot hold it: into a property that can be null it is null, and into one that cannot it throws `InvalidCastException`, "Column 'node' holds a JSON null.", as a class does.  On Npgsql 8 and 9 a `JsonNode[]` throws the same for an element that is a JSON `null`, and a `JsonNode?[]` gives null.  A `JsonNode[]` that Npgsql 10 reads gives such an element as null, unchecked, as for a `NULL` element: write `JsonNode?[]` where an element can be one.  A SQL `NULL` gives its property null, or throws as any column does.
- **`jsonb` keeps the structure, not the text.**  It stores JSON in a form of its own: its white space is its own, the keys of an object are in its own order, and of two keys of one name the last alone is kept.  So a value reads back equal in structure, not in text: compare JSON read back by its structure.  `json` keeps the text as it was sent.
