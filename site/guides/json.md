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
