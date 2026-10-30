using System.Collections.Immutable;
using System.Text;
using SqlSource.Generation.Emitting;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Converters;

/// <summary>
/// System.Text.Json's conversions: the edges that join its types to the <c>string</c> a driver reads and writes JSON
/// as, and the text of the class the generator emits to hold their methods.  One table drives both, so a converter
/// never names a method the class lacks.  It names System.Text.Json's types only as text.
/// </summary>
internal static class JsonConversions
{
    /// <summary>The class's name, as a converter's method is written.</summary>
    public const string ClassName = "global::SqlSource.JsonConversions";

    /// <summary>The class's metadata name, which <see cref="LibraryConverters.ClassMetadataNames" /> lists.</summary>
    public const string MetadataName = Namespace + "." + ShortClassName;

    private const string Namespace = "SqlSource";

    private const string ShortClassName = "JsonConversions";

    // As ClrTypeName.KeywordTextOf writes the type of a converter.
    private const string String = "string";

    private const string Parse = "global::System.Text.Json.Nodes.JsonNode.Parse(value)";

    private const string BodyIndent = GeneratedText.Indent + GeneratedText.Indent + GeneratedText.Indent;

    // Npgsql 10 reads and writes the nodes itself, and every version of Npgsql the element and the document.
    private static readonly LibraryScope SqlServerOnly = new(SqlLibrary.Json, ConverterTargets.SqlServer);

    private static readonly LibraryScope NodeTargets = new(
        SqlLibrary.Json,
        ConverterTargets.SqlServer | ConverterTargets.NpgsqlBefore10
    );

    // Every edge, in the order the class declares its methods.  A name is the return type's short name, From, then the
    // argument's.  The body is the method's whole body over the argument, value: => and an expression, or a block.  A
    // node's parse gives null for a JSON null, so its read returns its type with ?, and a cast to the object or the
    // array throws InvalidCastException for a value of another kind.
    private static readonly Edge[] Table =
    [
        new(
            String,
            JsonTypes.Element,
            "{\n"
                + BodyIndent
                + "using var document = global::System.Text.Json.JsonDocument.Parse(value);\n"
                + BodyIndent
                + "return document.RootElement.Clone();\n"
                + GeneratedText.Indent
                + GeneratedText.Indent
                + "}",
            SqlServerOnly
        ),
        new(JsonTypes.Element, String, "=> value.GetRawText();", SqlServerOnly),
        new(String, JsonTypes.Document, "=> global::System.Text.Json.JsonDocument.Parse(value);", SqlServerOnly),
        new(JsonTypes.Document, String, "=> value.RootElement.GetRawText();", SqlServerOnly),
        new(String, JsonTypes.Node, "=> " + Parse + ";", NodeTargets, returnsNull: true),
        new(JsonTypes.Node, String, "=> value.ToJsonString();", NodeTargets),
        new(String, JsonTypes.Object, "=> (" + JsonTypes.Object + "?)" + Parse + ";", NodeTargets, returnsNull: true),
        new(JsonTypes.Object, String, "=> value.ToJsonString();", NodeTargets),
        new(String, JsonTypes.Array, "=> (" + JsonTypes.Array + "?)" + Parse + ";", NodeTargets, returnsNull: true),
        new(JsonTypes.Array, String, "=> value.ToJsonString();", NodeTargets),
    ];

    /// <summary>The ten edges, in the order of <see cref="ConverterSet.Comparer" />.</summary>
    public static EquatableArray<Converter> Edges { get; } = BuildEdges();

    /// <summary>
    /// The whole file of the class: the header every generated file has, then <c>internal static class
    /// JsonConversions</c> in the namespace <c>SqlSource</c> with one method for each edge.  It has <c>\n</c> line
    /// endings and compiles without a warning on .NET 8 and later.
    /// </summary>
    public static string Source { get; } = BuildSource();

    private static EquatableArray<Converter> BuildEdges()
    {
        var edges = ImmutableArray.CreateBuilder<Converter>(Table.Length);
        foreach (var edge in Table)
        {
            edges.Add(
                new Converter(
                    ClassName + "." + edge.Name,
                    Namespace + "." + ShortClassName + "." + edge.Name + "(" + ShortNameOf(edge.Argument) + ")",
                    edge.Argument,
                    IsValueType(edge.Argument),
                    edge.Return,
                    IsValueType(edge.Return),
                    EquatableArray<string>.Empty,
                    Fault: null,
                    ConverterOrigin.Library,
                    Location: null,
                    edge.Scope
                )
            );
        }

        edges.Sort(ConverterSet.Comparer);
        return new EquatableArray<Converter>(edges.ToImmutable());
    }

    private static string BuildSource()
    {
        var builder = new StringBuilder(GeneratedText.Header);
        _ = builder.Append("\nnamespace ").Append(Namespace).Append("\n{\n");
        _ = builder.Append(GeneratedText.Indent).Append("internal static class ").Append(ShortClassName).Append('\n');
        _ = builder.Append(GeneratedText.Indent).Append("{\n");
        for (var index = 0; index < Table.Length; index++)
        {
            var edge = Table[index];
            if (index > 0)
            {
                _ = builder.Append('\n');
            }

            _ = builder
                .Append(GeneratedText.Indent)
                .Append(GeneratedText.Indent)
                .Append("internal static ")
                .Append(edge.Return)
                .Append(edge.ReturnsNull ? "? " : " ")
                .Append(edge.Name)
                .Append('(')
                .Append(edge.Argument)
                .Append(" value)");
            _ =
                edge.Body[0] == '{'
                    ? builder.Append('\n').Append(GeneratedText.Indent).Append(GeneratedText.Indent)
                    : builder.Append(' ');
            _ = builder.Append(edge.Body).Append('\n');
        }

        _ = builder.Append(GeneratedText.Indent).Append("}\n}\n");
        return builder.ToString();
    }

    // Of the types of the table, only the element is a value type.
    private static bool IsValueType(string type) => type == JsonTypes.Element;

    // A type's own name, string for string.
    private static string ShortNameOf(string type) => type.Substring(type.LastIndexOf('.') + 1);

    // One edge of the table.  Its method's name comes from the types' own names, String for string.
    private readonly struct Edge(
        string argument,
        string returnType,
        string body,
        LibraryScope scope,
        bool returnsNull = false
    )
    {
        public string Argument { get; } = argument;

        public string Return { get; } = returnType;

        public string Body { get; } = body;

        public LibraryScope Scope { get; } = scope;

        // Whether the method returns its type with ?, which the edge's return type is without.
        public bool ReturnsNull { get; } = returnsNull;

        public string Name => NameOf(Return) + "From" + NameOf(Argument);

        private static string NameOf(string type) => type == String ? "String" : ShortNameOf(type);
    }
}
