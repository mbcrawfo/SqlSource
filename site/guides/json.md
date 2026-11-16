# Read and write JSON columns

Read a `json` or `jsonb` column as System.Text.Json's `JsonElement`, or as a class of your own, and write it back.

**Use this when** your tables have JSON columns, and you want a C# type for them other than `string`.  Use it too when your JSON is in a column of a string type, such as `text` or `nvarchar(max)`.

<a id="turn-json-on"></a>

## Turn JSON on

Turn System.Text.Json on with `"libraryTypes": { "json": "on" }` in a settings file.  The settings file of the SQL Server example project turns it on, beside NodaTime and NetTopologySuite:

```jsonc
  "libraryTypes": {
    // SQL Server turns a library on only when asked, so the project turns on NodaTime, NetTopologySuite and
    // System.Text.Json, whose types only the methods of Methods/ get.  The type override above still wins over NodaTime
    // for every date, which stays a DateOnly.
    "nodaTime": "on",
    "netTopologySuite": "on",
    "json": "on",
  },
```

**What you get.**  PostgreSQL's `json` and `jsonb` columns and SQL Server's `json` columns are a `JsonElement` in your query methods.  Until you turn System.Text.Json on, they are a `string`.  The deliveries of [the PostgreSQL example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/PostgresExamples/README.md) and [the SQL Server example](https://github.com/mbcrawfo/SqlSource/blob/main/examples/SqlServerExamples/README.md), `Methods/DeliveryQueries.sql`, show System.Text.Json in a whole project.

**The rules you need.**

- **Only query methods get a `JsonElement`.**  A query whose output is `models` keeps the `string`.  See JSON on [PostgreSQL](../reference/databases/postgresql/libraries.md#json) and [SQL Server](../reference/databases/sqlserver/libraries.md#json).
- **A type override, a column type or a marker can give a JSON column another type.**  That can be a `JsonDocument` or a `JsonNode`, in place of the `JsonElement`.  It can also be a class of your own, which the query methods read and send through System.Text.Json's serializer.  See [Read a class of your own from a JSON column](#class-in-a-json-column).
- **You register nothing in your application**, on either database.
- **A column of a string type that holds JSON, such as an `nvarchar(max)`, takes a class by the JSON opt-in**: `-> json OrderMetadata`.  See [JSON in a text or nvarchar(max) column](#json-in-text).

<a id="class-in-a-json-column"></a>

## Read a class of your own from a JSON column

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "postgres",
    "columnTypes": {
        "orders.metadata": "OrderMetadata",
    },
}
```

```sql postgres
-- name: GetOrder -> one
SELECT id, metadata FROM orders WHERE id = @id;

-- name: SetMetadata
-- param: @metadata -> OrderMetadata
UPDATE orders SET metadata = @metadata WHERE id = @id;
```

```csharp
namespace MyApp;

public sealed record OrderMetadata(string Channel, int Priority);
```

```csharp
GetOrderDto order = await orders.GetOrder(new GetOrderParams(orderId));
// order.Metadata is an OrderMetadata, read from the jsonb column metadata
await orders.SetMetadata(new SetMetadataParams(order.Metadata with { Priority = 2 }, orderId));
```

**What you get.**  `GetOrderDto.Metadata` is an `OrderMetadata`, read from the `jsonb` column `metadata`.  `SetMetadata` takes `@metadata` as an `OrderMetadata`.  Since `metadata` is a `jsonb`, the query methods read and send the `OrderMetadata` through System.Text.Json's serializer.  You register nothing on the data source.

**With options of your own**, such as camelCase names, give the class two converters between it and `string`:

```csharp
using System;
using System.Text.Json;
using SqlSource;

namespace MyApp;

public sealed record OrderMetadata(string Channel, int Priority)
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    [SqlSourceConverter]
    public static OrderMetadata FromDatabase(string json) =>
        JsonSerializer.Deserialize<OrderMetadata>(json, Options)
        ?? throw new InvalidCastException("The order's metadata is a JSON null.");

    [SqlSourceConverter]
    public static string ToDatabase(OrderMetadata metadata) => JsonSerializer.Serialize(metadata, Options);
}
```

A SQL `NULL` never reaches a converter.  A JSON `null` does, as the text `null`, and `Deserialize` returns null for it.  The converter decides what it gives then.

**In a trimmed or AOT project**, the calls of the serializer warn, IL2026 and IL3050.  They fail when the reflection of System.Text.Json is off.  Give the converters a context that System.Text.Json's source generator writes:

```csharp
using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using SqlSource;

namespace MyApp;

public sealed record OrderMetadata(string Channel, int Priority)
{
    [SqlSourceConverter]
    public static OrderMetadata FromDatabase(string json) =>
        JsonSerializer.Deserialize(json, OrderJson.Default.OrderMetadata)
        ?? throw new InvalidCastException("The order's metadata is a JSON null.");

    [SqlSourceConverter]
    public static string ToDatabase(OrderMetadata metadata) =>
        JsonSerializer.Serialize(metadata, OrderJson.Default.OrderMetadata);
}

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(OrderMetadata))]
internal sealed partial class OrderJson : JsonSerializerContext;
```

**The rules you need.**

- **The default options of the serializer write the names of the properties as the record has them**: `{"Channel":"web","Priority":2}`.  They also match the names with their case.  So JSON that other code wrote in camelCase, `{"channel":"web"}`, reads as an `OrderMetadata` whose properties have their default values, `null` and `0`.  There is no error.  See [a class in a JSON column](../reference/library-types-and-json.md#json-class).
- **For options of your own, give the class two [converters](../reference/converters.md#converters)** between it and `string`.  The query methods call them in place of the serializer.  The sample under "With options of your own", above, shows them.
- **In a trimmed or AOT project, the converters need a context that System.Text.Json's source generator writes.**  The sample under "In a trimmed or AOT project", above, shows it.
- **On SQL Server, the same works for a `json` column.**  A column of a string type, such as an `nvarchar(max)` before SQL Server 2025, can hold JSON.  Such a column needs [the JSON opt-in](../reference/library-types-and-json.md#json-opt-in), as [JSON in a text or nvarchar(max) column](#json-in-text) shows.  See JSON on [PostgreSQL](../reference/databases/postgresql/libraries.md#json) and [SQL Server](../reference/databases/sqlserver/libraries.md#json).

<a id="json-in-text"></a>

## JSON in a text or nvarchar(max) column

If your JSON is in a column of a string type, such as `text` or `nvarchar(max)`, write `json` before your class:

```jsonc
// sqlsource.json, in the project's folder
{
    "root": true,
    "dialect": "mssql",
    "columnTypes": {
        "orders.metadata": { "type": "OrderMetadata", "json": true },
    },
}
```

```sql mssql
-- name: GetOrder -> one
SELECT id, metadata FROM dbo.orders WHERE id = @id;

-- name: SetMetadata
-- param: @metadata -> json OrderMetadata
UPDATE dbo.orders SET metadata = @metadata WHERE id = @id;
```

```csharp
namespace MyApp;

public sealed record OrderMetadata(string Channel, int Priority);
```

**What you get.**  `GetOrderDto.Metadata` is an `OrderMetadata`, read from the text of the column through System.Text.Json's serializer.  `SetMetadata` sends `@metadata` as the text of its JSON.

**The rules you need.**

- `"json": true` in a column type, or the word `json` before the type in a marker, says that a column or a parameter holds JSON.  The type override takes no `json`.  See [The JSON opt-in](../reference/library-types-and-json.md#json-opt-in).
- A column type never reaches a parameter.  Give each parameter its own `-- param:` line, with `-> json`.  See [Column types](../reference/model-properties.md#setting-column-types).
- For one query only, write `-- column: metadata -> json OrderMetadata` inside it.  See [Setting a column's nullability or type with a marker](../reference/model-properties.md#column-marker).
- On PostgreSQL, the same works for a `text` or a `varchar` column.  A `json` or `jsonb` column needs no `json` word.  See JSON on [PostgreSQL](../reference/databases/postgresql/libraries.md#json) and [SQL Server](../reference/databases/sqlserver/libraries.md#json).
- If you add or remove `json`, the query stays described: you need not run `dotnet sqlsource describe`.  See [The JSON opt-in](../reference/library-types-and-json.md#json-opt-in).
- For JSON in camelCase, write two converters, as [Read a class of your own from a JSON column](#class-in-a-json-column) shows.  See [A class in a JSON column](../reference/library-types-and-json.md#json-class).

## If it does not work

- **SQLSRC283.**  `json` is on a column or a parameter whose type is not a string or JSON type, such as an `int`.  Or it is before a type that is not a class of yours.  Remove `json`, or move it to the column that holds JSON.  See [SQLSRC283](../diagnostics.md#sqlsrc283).
- **`InvalidCastException` for your class in a string column.**  The column or the parameter has no `json`, so the driver is given your class.  Write `json` before the type.  See [A class in a JSON column](../reference/library-types-and-json.md#json-class).
- **`JsonException` when a row is read.**  The text of the column is not the JSON of your class, such as an array for an object.  See [A class in a JSON column](../reference/library-types-and-json.md#json-class).
- **Properties of your class have their default values, such as `null` and `0`, with no error.**  The names in the JSON have another case than the properties, such as camelCase.  Write two converters with your options.  See [Read a class of your own from a JSON column](#class-in-a-json-column).
- **IL2026 and IL3050 warnings, or `InvalidOperationException` when the method runs, in a trimmed or AOT project.**  The serializer reads your class by reflection, which such a project turns off by default.  Give your converters a context that System.Text.Json's source generator writes.  See [Read a class of your own from a JSON column](#class-in-a-json-column).
