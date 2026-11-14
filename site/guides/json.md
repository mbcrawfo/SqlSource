# Read and write JSON columns

<a id="turn-json-on"></a>

## Turn JSON on

A project's JSON columns are `string`s until it turns System.Text.Json on, with `"libraryTypes": { "json": "on" }` in a settings file: then PostgreSQL's `json` and `jsonb` and SQL Server's `json` are a `JsonElement` in its query methods.  A type override, a column type or a marker gives a JSON column a `JsonDocument` or a `JsonNode` instead, or a class of your own, which the query methods read and send through System.Text.Json's serializer.  Nothing is registered on the data source, and a column of a string type that holds JSON, such as an `nvarchar(max)`, takes a class by the JSON opt-in, `-> json OrderMetadata`.  See JSON on [PostgreSQL](../reference/databases/postgresql/libraries.md#json) and [SQL Server](../reference/databases/sqlserver/libraries.md#json), and [the recipe](#class-in-a-json-column).

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

`metadata` is a `jsonb`, so the query methods read and send the `OrderMetadata` through System.Text.Json's serializer, with nothing to register on the data source.  The serializer's default options write the names of the properties as the record has them, `{"Channel":"web","Priority":2}`, and match them with their case, so JSON that other code wrote in camelCase, `{"channel":"web"}`, reads as an `OrderMetadata` whose properties have their default values, `null` and `0`, with no error.

**With options of your own**, such as camelCase names, give the class two converters between it and `string`, which the query methods call in place of the serializer:

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

A SQL `NULL` never reaches a converter, but a JSON `null` does, as the text `null`, for which `Deserialize` returns null: the converter decides what it gives.

**In a trimmed or AOT project**, the serializer's calls warn, IL2026 and IL3050, and fail when System.Text.Json's reflection is off.  Give the converters a context that System.Text.Json's source generator writes:

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

On SQL Server, the same works for a `json` column.  A column of a string type that holds JSON, such as an `nvarchar(max)` before SQL Server 2025, says so with the JSON opt-in, `"orders.metadata": { "type": "OrderMetadata", "json": true }`, and a parameter with `-- param: @metadata -> json OrderMetadata`.

Rules: [a class in a JSON column](../reference/library-types-and-json.md#json-class), [the JSON opt-in](../reference/library-types-and-json.md#json-opt-in), [converters](../reference/converters.md#converters), JSON on [PostgreSQL](../reference/databases/postgresql/libraries.md#json) and [SQL Server](../reference/databases/sqlserver/libraries.md#json).

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
