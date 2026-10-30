using System.Collections.Immutable;
using System.Text;
using SqlSource.Generation.Emitting;
using SqlSource.Settings.Libraries;

namespace SqlSource.Generation.Converters;

/// <summary>
/// NodaTime's conversions on SQL Server: the edges that join NodaTime's types to the types a SQL Server driver reads
/// and writes, and the text of the class the generator emits to hold their methods.  One table drives both, so a
/// converter never names a method the class lacks.  It names NodaTime's types only as text.
/// </summary>
internal static class NodaTimeConversions
{
    /// <summary>The class's name, as a converter's method is written.</summary>
    public const string ClassName = "global::SqlSource.NodaTimeConversions";

    private const string Namespace = "SqlSource";

    private const string ShortClassName = "NodaTimeConversions";

    // Each edge is written as C# writes the types of a converter, ClrTypeName.KeywordTextOf, which keeps the name of a
    // type with no keyword.  DateTime, DateTimeOffset and TimeSpan have none.
    private const string DateTime = "global::System.DateTime";

    private const string DateTimeOffset = "global::System.DateTimeOffset";

    private const string TimeSpan = "global::System.TimeSpan";

    private const string NodaTime = "global::NodaTime.";

    // Every edge, in the order the class declares its methods.  A name is the return type's short name, From, then the
    // argument's.  The body is an expression over the argument, value.
    private static readonly Edge[] Table =
    [
        new(DateTime, NodaTime + "LocalDate", NodaTime + "LocalDate.FromDateTime(value)"),
        new(NodaTime + "LocalDate", DateTime, "value.ToDateTimeUnspecified()"),
        new(TimeSpan, NodaTime + "LocalTime", NodaTime + "LocalTime.FromTicksSinceMidnight(value.Ticks)"),
        new(NodaTime + "LocalTime", TimeSpan, "new global::System.TimeSpan(value.TickOfDay)"),
        new(DateTime, NodaTime + "LocalDateTime", NodaTime + "LocalDateTime.FromDateTime(value)"),
        new(NodaTime + "LocalDateTime", DateTime, "value.ToDateTimeUnspecified()"),
        new(DateTimeOffset, NodaTime + "OffsetDateTime", NodaTime + "OffsetDateTime.FromDateTimeOffset(value)"),
        new(NodaTime + "OffsetDateTime", DateTimeOffset, "value.ToDateTimeOffset()"),
        new(
            DateTime,
            NodaTime + "Instant",
            NodaTime
                + "Instant.FromDateTimeUtc(global::System.DateTime.SpecifyKind(value, global::System.DateTimeKind.Utc))"
        ),
        new(NodaTime + "Instant", DateTime, "value.ToDateTimeUtc()"),
        new(DateTimeOffset, NodaTime + "Instant", NodaTime + "Instant.FromDateTimeOffset(value)"),
        new(NodaTime + "Instant", DateTimeOffset, "value.ToDateTimeOffset()"),
        new(TimeSpan, NodaTime + "Duration", NodaTime + "Duration.FromTimeSpan(value)"),
        new(NodaTime + "Duration", TimeSpan, "value.ToTimeSpan()"),
    ];

    private static readonly LibraryScope Scope = new(SqlLibrary.NodaTime, ConverterTargets.SqlServer);

    /// <summary>The fourteen edges, in the order of <see cref="ConverterSet.Comparer" />.</summary>
    public static EquatableArray<Converter> Edges { get; } = BuildEdges();

    /// <summary>
    /// The whole file of the class: the header every generated file has, then <c>internal static class
    /// NodaTimeConversions</c> in the namespace <c>SqlSource</c> with one method for each edge.  It has <c>\n</c> line
    /// endings and compiles without a warning against NodaTime 3.1.9 and later.
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
                    Namespace + "." + ShortClassName + "." + edge.Name + "(" + edge.ArgumentShortName + ")",
                    edge.Argument,
                    ArgumentIsValueType: true,
                    edge.Return,
                    ReturnIsValueType: true,
                    EquatableArray<string>.Empty,
                    Fault: null,
                    ConverterOrigin.Library,
                    Location: null,
                    Scope
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
                .Append(' ')
                .Append(edge.Name)
                .Append('(')
                .Append(edge.Argument)
                .Append(" value) => ")
                .Append(edge.Body)
                .Append(";\n");
        }

        _ = builder.Append(GeneratedText.Indent).Append("}\n}\n");
        return builder.ToString();
    }

    // One edge of the table.  Its method's name and its argument's short name come from the types' own names.
    private readonly struct Edge(string argument, string returnType, string body)
    {
        public string Argument { get; } = argument;

        public string Return { get; } = returnType;

        public string Body { get; } = body;

        public string Name => ShortNameOf(Return) + "From" + ShortNameOf(Argument);

        public string ArgumentShortName => ShortNameOf(Argument);

        private static string ShortNameOf(string type) => type.Substring(type.LastIndexOf('.') + 1);
    }
}
