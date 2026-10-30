using System;

namespace SqlSource.Generation.Converters;

/// <summary>
/// The types of System.Text.Json that hold a JSON value, as <see cref="Settings.ClrTypes.ClrTypeName.KeywordTextOf" />
/// writes them.  It names them as text alone, since the generator references no library.
/// </summary>
internal static class JsonTypes
{
    /// <summary>A JSON value, as a value type.</summary>
    public const string Element = "global::System.Text.Json.JsonElement";

    /// <summary>A parsed JSON document, which owns the memory of its root element.</summary>
    public const string Document = "global::System.Text.Json.JsonDocument";

    /// <summary>A mutable JSON value of any kind.</summary>
    public const string Node = NodesNamespace + "JsonNode";

    /// <summary>A mutable JSON object.</summary>
    public const string Object = NodesNamespace + "JsonObject";

    /// <summary>A mutable JSON array.</summary>
    public const string Array = NodesNamespace + "JsonArray";

    private const string NodesNamespace = "global::System.Text.Json.Nodes.";

    /// <summary>Whether <paramref name="text" /> is one of the five types, compared ordinally.</summary>
    public static bool IsJsonType(string text) =>
        string.Equals(text, Element, StringComparison.Ordinal)
        || string.Equals(text, Document, StringComparison.Ordinal)
        || IsNode(text);

    /// <summary>
    /// Whether <paramref name="text" /> is <see cref="Node" />, <see cref="Object" /> or <see cref="Array" />, whose
    /// parse gives null for a JSON <c>null</c>, compared ordinally.
    /// </summary>
    public static bool IsNode(string text) =>
        string.Equals(text, Node, StringComparison.Ordinal)
        || string.Equals(text, Object, StringComparison.Ordinal)
        || string.Equals(text, Array, StringComparison.Ordinal);
}
