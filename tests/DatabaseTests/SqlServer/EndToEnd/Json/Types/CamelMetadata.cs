using System;
using System.Text.Json;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd.Json.Types;

// A class of the project that the column types of Queries/Json.sql give camel, an nvarchar, with converters of its own
// between it and a string, which a method calls with no opt-in, and which replace the serializer's default options with
// the web's: its properties are stored in camelCase, orderId and note.
internal sealed record CamelMetadata(int OrderId, string? Note)
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [SqlSourceConverter]
    public static CamelMetadata FromDatabase(string value) =>
        JsonSerializer.Deserialize<CamelMetadata>(value, Web)
        ?? throw new InvalidCastException("A CamelMetadata cannot be a JSON null.");

    [SqlSourceConverter]
    public static string ToDatabase(CamelMetadata metadata) => JsonSerializer.Serialize(metadata, Web);
}
