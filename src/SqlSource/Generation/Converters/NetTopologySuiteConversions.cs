using System;
using System.Collections.Generic;
using System.Text;
using SqlSource.Generation.Emitting;

namespace SqlSource.Generation.Converters;

/// <summary>
/// NetTopologySuite's spatial read path on SQL Server: the class the generator emits to hold the shared readers and
/// writers of NetTopologySuite.IO.SqlServerBytes that a method calls for a <c>geography</c> or <c>geometry</c>, and
/// the geometry types the path reads and binds.  It is no converter, and has no edges.  It names NetTopologySuite's
/// types only as text.
/// </summary>
internal static class NetTopologySuiteConversions
{
    /// <summary>The class's name, as a method names one of its fields.</summary>
    public const string ClassName = "global::SqlSource.NetTopologySuiteConversions";

    /// <summary>The class's metadata name, which <see cref="LibraryConverters.ClassMetadataNames" /> lists.</summary>
    public const string MetadataName = Namespace + "." + ShortClassName;

    /// <summary>NetTopologySuite's <c>Geometry</c>, which the read gives without a cast.</summary>
    public const string Geometry = Geometries + "Geometry";

    /// <summary>The type of the value that the write binds and the read reads, SQL Server's bytes.</summary>
    public const string SqlBytes = "global::System.Data.SqlTypes.SqlBytes";

    private const string Namespace = "SqlSource";

    private const string ShortClassName = "NetTopologySuiteConversions";

    private const string Geometries = "global::NetTopologySuite.Geometries.";

    private const string Reader = "global::NetTopologySuite.IO.SqlServerBytesReader";

    private const string Writer = "global::NetTopologySuite.IO.SqlServerBytesWriter";

    // Every field, in the order the class declares them: its name, its type, and whether it reads or writes geography.
    private static readonly (string Name, string Type, bool IsGeography)[] Fields =
    [
        ("GeographyReader", Reader, true),
        ("GeographyWriter", Writer, true),
        ("GeometryReader", Reader, false),
        ("GeometryWriter", Writer, false),
    ];

    // Geometry and each of its subtypes, which a type override may name for the path to cast the read to.
    private static readonly HashSet<string> GeometryTypes = new(
        [
            Geometry,
            Geometries + "Point",
            Geometries + "LineString",
            Geometries + "LinearRing",
            Geometries + "Polygon",
            Geometries + "MultiPoint",
            Geometries + "MultiLineString",
            Geometries + "MultiPolygon",
            Geometries + "GeometryCollection",
        ],
        StringComparer.Ordinal
    );

    /// <summary>
    /// The whole file of the class: the header every generated file has, then <c>internal static class
    /// NetTopologySuiteConversions</c> in the namespace <c>SqlSource</c> with the four shared instances and an empty
    /// static constructor.  It has <c>\n</c> line endings and compiles without a warning against NetTopologySuite 2.5.0
    /// and later with NetTopologySuite.IO.SqlServerBytes 2.1.0.
    /// </summary>
    public static string Source { get; } = BuildSource();

    /// <summary>
    /// Whether <paramref name="text" />, written from <c>global::</c>, is NetTopologySuite's <c>Geometry</c> or one of
    /// its subtypes.
    /// </summary>
    public static bool IsGeometryType(string text) => GeometryTypes.Contains(text);

    /// <summary>
    /// The field a method calls for a column read or a parameter bound of <paramref name="key" />,
    /// <c>geography</c> or any other spatial key, <c>geometry</c>:
    /// <c>global::SqlSource.NetTopologySuiteConversions.GeographyReader</c> and so on.
    /// </summary>
    public static string InstanceOf(string key, bool isRead) =>
        ClassName
        + "."
        + (key.Equals(SqlServerNames.Geography, StringComparison.OrdinalIgnoreCase) ? "Geography" : "Geometry")
        + (isRead ? "Reader" : "Writer");

    private static string BuildSource()
    {
        const string Member = GeneratedText.Indent + GeneratedText.Indent;
        var builder = new StringBuilder(GeneratedText.Header);
        _ = builder.Append("\nnamespace ").Append(Namespace).Append("\n{\n");
        _ = builder.Append(GeneratedText.Indent).Append("internal static class ").Append(ShortClassName).Append('\n');
        _ = builder.Append(GeneratedText.Indent).Append("{\n");
        foreach (var (name, type, isGeography) in Fields)
        {
            _ = builder
                .Append(Member)
                .Append("internal static readonly ")
                .Append(type)
                .Append(' ')
                .Append(name)
                .Append(" = new ")
                .Append(type)
                .Append(isGeography ? " { IsGeography = true };\n\n" : "();\n\n");
        }

        // A reader takes NtsGeometryServices.Instance when it is made, so the static constructor, which keeps the
        // runtime from making the fields before the class is first used, lets an application set Instance first.
        _ = builder
            .Append(Member)
            .Append("// Makes the fields at the first spatial query, after the application has set\n")
            .Append(Member)
            .Append("// NtsGeometryServices.Instance, which a reader takes when it is made.\n")
            .Append(Member)
            .Append("static ")
            .Append(ShortClassName)
            .Append("() { }\n");
        _ = builder.Append(GeneratedText.Indent).Append("}\n}\n");
        return builder.ToString();
    }
}
