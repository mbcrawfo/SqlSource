using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Settings.Libraries;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generation.Converters;

public sealed class LibraryConvertersTests
{
    private const string ClassName = "global::SqlSource.NodaTimeConversions";

    private const string DateTime = "global::System.DateTime";

    private const string DateTimeOffset = "global::System.DateTimeOffset";

    private const string TimeSpan = "global::System.TimeSpan";

    // Every edge of NodaTime's SQL Server conversions: its argument, its return type and the method's name.
    private static readonly (string Argument, string Return, string Name)[] Edges =
    [
        (DateTime, "global::NodaTime.LocalDate", "LocalDateFromDateTime"),
        ("global::NodaTime.LocalDate", DateTime, "DateTimeFromLocalDate"),
        (TimeSpan, "global::NodaTime.LocalTime", "LocalTimeFromTimeSpan"),
        ("global::NodaTime.LocalTime", TimeSpan, "TimeSpanFromLocalTime"),
        (DateTime, "global::NodaTime.LocalDateTime", "LocalDateTimeFromDateTime"),
        ("global::NodaTime.LocalDateTime", DateTime, "DateTimeFromLocalDateTime"),
        (DateTimeOffset, "global::NodaTime.OffsetDateTime", "OffsetDateTimeFromDateTimeOffset"),
        ("global::NodaTime.OffsetDateTime", DateTimeOffset, "DateTimeOffsetFromOffsetDateTime"),
        (DateTime, "global::NodaTime.Instant", "InstantFromDateTime"),
        ("global::NodaTime.Instant", DateTime, "DateTimeFromInstant"),
        (DateTimeOffset, "global::NodaTime.Instant", "InstantFromDateTimeOffset"),
        ("global::NodaTime.Instant", DateTimeOffset, "DateTimeOffsetFromInstant"),
        (TimeSpan, "global::NodaTime.Duration", "DurationFromTimeSpan"),
        ("global::NodaTime.Duration", TimeSpan, "TimeSpanFromDuration"),
    ];

    private const string JsonClassName = "global::SqlSource.JsonConversions";

    // Every edge of System.Text.Json's conversions: its argument, its return type, the method's name and the targets it
    // serves.
    private static readonly (string Argument, string Return, string Name, ConverterTargets Targets)[] JsonEdges =
    [
        ("string", JsonTypes.Element, "JsonElementFromString", ConverterTargets.SqlServer),
        (JsonTypes.Element, "string", "StringFromJsonElement", ConverterTargets.SqlServer),
        ("string", JsonTypes.Document, "JsonDocumentFromString", ConverterTargets.SqlServer),
        (JsonTypes.Document, "string", "StringFromJsonDocument", ConverterTargets.SqlServer),
        ("string", JsonTypes.Node, "JsonNodeFromString", NodeTargets),
        (JsonTypes.Node, "string", "StringFromJsonNode", NodeTargets),
        ("string", JsonTypes.Object, "JsonObjectFromString", NodeTargets),
        (JsonTypes.Object, "string", "StringFromJsonObject", NodeTargets),
        ("string", JsonTypes.Array, "JsonArrayFromString", NodeTargets),
        (JsonTypes.Array, "string", "StringFromJsonArray", NodeTargets),
    ];

    private const ConverterTargets NodeTargets = ConverterTargets.SqlServer | ConverterTargets.NpgsqlBefore10;

    [Fact]
    public void Of_WithoutNodaTime_IsEmpty() =>
        LibraryConverters
            .Of(new LibraryProbe(NodaTime: false, NpgsqlNodaTime: true, NodaTimeVersion: null))
            .ShouldBe(EquatableArray<Converter>.Empty);

    [Fact]
    public void Of_WithNodaTime_HasTheFourteenEdges()
    {
        var edges = LibraryConverters.Of(Probe());

        edges
            .Select(edge => (edge.ArgumentText, edge.ReturnText, edge.Method))
            .ShouldBe(
                Edges
                    .Select(edge => (edge.Argument, edge.Return, ClassName + "." + edge.Name))
                    .OrderBy(edge => edge.Argument, System.StringComparer.Ordinal)
                    .ThenBy(edge => edge.Return, System.StringComparer.Ordinal)
            );
        edges.Count.ShouldBe(14);
    }

    [Fact]
    public void Of_WithNodaTime_IsSortedAsTheSetSortsThem()
    {
        var edges = LibraryConverters.Of(Probe());

        edges.ShouldBe(edges.OrderBy(edge => edge, ConverterSet.Comparer));
    }

    [Fact]
    public void Of_EveryEdge_IsALibraryEdgeWithoutAFaultOrALocation()
    {
        foreach (var edge in LibraryConverters.Of(Probe()))
        {
            edge.Origin.ShouldBe(ConverterOrigin.Library);
            edge.Fault.ShouldBeNull();
            edge.Location.ShouldBeNull();
            edge.WarningIds.Count.ShouldBe(0);
            edge.ArgumentIsValueType.ShouldBeTrue();
            edge.ReturnIsValueType.ShouldBeTrue();
            edge.Library.ShouldBe(new LibraryScope(SqlLibrary.NodaTime, ConverterTargets.SqlServer));
        }
    }

    [Fact]
    public void Of_EveryEdge_ServesSqlServerAlone()
    {
        foreach (var edge in LibraryConverters.Of(Probe()))
        {
            edge.Serves(ConverterTargets.SqlServer).ShouldBeTrue();
            edge.Serves(ConverterTargets.NpgsqlBefore10).ShouldBeFalse();
            edge.Serves(ConverterTargets.Npgsql10).ShouldBeFalse();
        }
    }

    [Fact]
    public void Of_EveryEdge_IsNamedByItsMethodAndItsArgument()
    {
        var edges = LibraryConverters.Of(Probe()).ToDictionary(edge => edge.Method);

        edges[ClassName + ".LocalDateFromDateTime"]
            .DisplayName.ShouldBe("SqlSource.NodaTimeConversions.LocalDateFromDateTime(DateTime)");
        edges[ClassName + ".DateTimeOffsetFromInstant"]
            .DisplayName.ShouldBe("SqlSource.NodaTimeConversions.DateTimeOffsetFromInstant(Instant)");
    }

    [Fact]
    public void Of_TwiceOverEqualProbes_IsEqual() =>
        LibraryConverters.Of(Probe()).ShouldBe(LibraryConverters.Of(Probe()));

    [Fact]
    public void SourceOf_NodaTime_IsTheClassOfTheEdgesWithTheStandardHeader()
    {
        var source = LibraryConverters.SourceOf(SqlLibrary.NodaTime).ShouldNotBeNull();

        source.ShouldBe(NodaTimeConversions.Source);
        source.ShouldStartWith("// <auto-generated/>\n#nullable enable\n");
        source.ShouldNotContain("\r");
        source.ShouldContain("namespace SqlSource");
        source.ShouldContain("internal static class NodaTimeConversions");
    }

    [Fact]
    public void HintNameOf_NodaTime_IsAFileNoTypeOrModelCanHave()
    {
        var name = LibraryConverters.HintNameOf(SqlLibrary.NodaTime);

        name.ShouldBe("SqlSource-NodaTimeConversions.g.cs");
        name.ShouldNotEndWith("-Model.g.cs");
        name.ShouldNotEndWith("-Methods.g.cs");
    }

    [Theory]
    [InlineData("3.1.9")]
    [InlineData("3.3.7")]
    public void SourceOf_NodaTime_CompilesWithoutWarnings(string version)
    {
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/NodaTimeConversions.g.cs", NodaTimeConversions.Source)],
            references: [LibraryReferences.NodaTime(version)]
        );
        compilation = compilation.WithOptions(
            compilation
                .Options.WithGeneralDiagnosticOption(ReportDiagnostic.Error)
                .WithNullableContextOptions(NullableContextOptions.Enable)
        );

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.ToString())
            .ShouldBeEmpty();
    }

    [Fact]
    public void SourceOf_EveryEdge_DeclaresItsMethod()
    {
        var tree = CSharpSyntaxTree.ParseText(
            NodaTimeConversions.Source,
            cancellationToken: TestContext.Current.CancellationToken
        );
        var declared = tree.GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .Select(method => method.Identifier.ValueText)
            .ToArray();

        foreach (var edge in LibraryConverters.Of(Probe()))
        {
            declared.ShouldContain(edge.Method[(ClassName.Length + 1)..]);
        }

        declared.Length.ShouldBe(14);
    }

    [Fact]
    public void Of_WithJson_HasTheTenEdges()
    {
        var edges = LibraryConverters.Of(JsonProbe());

        edges
            .Select(edge => (edge.ArgumentText, edge.ReturnText, edge.Method, edge.Library!.Targets))
            .ShouldBe(
                JsonEdges
                    .Select(edge => (edge.Argument, edge.Return, JsonClassName + "." + edge.Name, edge.Targets))
                    .OrderBy(edge => edge.Argument, System.StringComparer.Ordinal)
                    .ThenBy(edge => edge.Return, System.StringComparer.Ordinal)
            );
        edges.ShouldBe(JsonConversions.Edges);
    }

    [Fact]
    public void Of_WithJson_EveryEdgeIsALibraryEdgeOfJson()
    {
        foreach (var edge in LibraryConverters.Of(JsonProbe()))
        {
            edge.Origin.ShouldBe(ConverterOrigin.Library);
            edge.Fault.ShouldBeNull();
            edge.Location.ShouldBeNull();
            edge.WarningIds.Count.ShouldBe(0);
            edge.ArgumentIsValueType.ShouldBe(edge.ArgumentText == JsonTypes.Element);
            edge.ReturnIsValueType.ShouldBe(edge.ReturnText == JsonTypes.Element);
            edge.Library.ShouldNotBeNull().Library.ShouldBe(SqlLibrary.Json);
            edge.Serves(ConverterTargets.SqlServer).ShouldBeTrue();
            edge.Serves(ConverterTargets.Npgsql10).ShouldBeFalse();
            edge.Serves(ConverterTargets.NpgsqlBefore10)
                .ShouldBe(JsonTypes.IsNode(edge.ArgumentText) || JsonTypes.IsNode(edge.ReturnText));
        }
    }

    [Fact]
    public void Of_WithJson_IsNamedByItsMethodAndItsArgument()
    {
        var edges = LibraryConverters.Of(JsonProbe()).ToDictionary(edge => edge.Method);

        edges[JsonClassName + ".JsonElementFromString"]
            .DisplayName.ShouldBe("SqlSource.JsonConversions.JsonElementFromString(string)");
        edges[JsonClassName + ".StringFromJsonObject"]
            .DisplayName.ShouldBe("SqlSource.JsonConversions.StringFromJsonObject(JsonObject)");
    }

    [Fact]
    public void Of_WithNodaTimeAndJson_IsBothSortedAsTheSetSortsThem()
    {
        var edges = LibraryConverters.Of(Probe() with { Json = true });

        edges.Count.ShouldBe(24);
        edges.ShouldBe(
            NodaTimeConversions.Edges.Concat(JsonConversions.Edges).OrderBy(edge => edge, ConverterSet.Comparer)
        );
    }

    [Fact]
    public void Of_WithOneLibrary_IsThatLibrarysEdges()
    {
        LibraryConverters.Of(Probe()).ShouldBe(NodaTimeConversions.Edges);
        LibraryConverters.Of(JsonProbe()).ShouldBe(JsonConversions.Edges);
    }

    [Fact]
    public void SourceOf_Json_IsTheClassOfTheEdgesWithTheStandardHeader()
    {
        var source = LibraryConverters.SourceOf(SqlLibrary.Json).ShouldNotBeNull();

        source.ShouldBe(JsonConversions.Source);
        source.ShouldStartWith("// <auto-generated/>\n#nullable enable\n");
        source.ShouldNotContain("\r");
        source.ShouldContain("namespace SqlSource\n{\n    internal static class JsonConversions\n");
        source.ShouldContain(
            "        internal static global::System.Text.Json.JsonElement JsonElementFromString(string value)\n"
                + "        {\n"
                + "            using var document = global::System.Text.Json.JsonDocument.Parse(value);\n"
                + "            return document.RootElement.Clone();\n"
                + "        }\n"
        );
        source.ShouldContain(
            "        internal static global::System.Text.Json.Nodes.JsonObject? JsonObjectFromString(string value) => "
                + "(global::System.Text.Json.Nodes.JsonObject?)global::System.Text.Json.Nodes.JsonNode.Parse(value);\n"
        );
        source.ShouldContain(
            "        internal static string StringFromJsonDocument(global::System.Text.Json.JsonDocument value) => "
                + "value.RootElement.GetRawText();\n"
        );
    }

    [Fact]
    public void HintNameOf_Json_IsTheFileOfItsClass() =>
        LibraryConverters.HintNameOf(SqlLibrary.Json).ShouldBe("SqlSource-JsonConversions.g.cs");

    // On the runtime's System.Text.Json and on .NET 8's.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SourceOf_Json_CompilesWithoutWarnings(bool net8)
    {
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/JsonConversions.g.cs", JsonConversions.Source)],
            supportedFramework: !net8,
            references: net8 ? LibraryReferences.Net8Framework : []
        );
        compilation = compilation.WithOptions(
            compilation
                .Options.WithGeneralDiagnosticOption(ReportDiagnostic.Error)
                .WithNullableContextOptions(NullableContextOptions.Enable)
        );

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.ToString())
            .ShouldBeEmpty();
    }

    [Fact]
    public void SourceOf_EveryJsonEdge_DeclaresItsMethod()
    {
        var declared = CSharpSyntaxTree
            .ParseText(JsonConversions.Source, cancellationToken: TestContext.Current.CancellationToken)
            .GetRoot(TestContext.Current.CancellationToken)
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.MethodDeclarationSyntax>()
            .Select(method => method.Identifier.ValueText)
            .ToArray();

        declared.ShouldBe(
            JsonConversions.Edges.Select(edge => edge.Method[(JsonClassName.Length + 1)..]),
            ignoreOrder: true
        );
    }

    // NetTopologySuite's class holds the four shared instances of the spatial read path and nothing else, and makes
    // them at its first use.
    [Fact]
    public void SourceOf_NetTopologySuite_IsTheClassOfTheFourInstances()
    {
        const string Reader = "global::NetTopologySuite.IO.SqlServerBytesReader";
        const string Writer = "global::NetTopologySuite.IO.SqlServerBytesWriter";
        const string Field = "        internal static readonly ";

        var source = LibraryConverters.SourceOf(SqlLibrary.NetTopologySuite).ShouldNotBeNull();

        source.ShouldBe(NetTopologySuiteConversions.Source);
        source.ShouldBe(
            "// <auto-generated/>\n#nullable enable\n\nnamespace SqlSource\n{\n"
                + "    internal static class NetTopologySuiteConversions\n    {\n"
                + $"{Field}{Reader} GeographyReader = new {Reader} {{ IsGeography = true }};\n\n"
                + $"{Field}{Writer} GeographyWriter = new {Writer} {{ IsGeography = true }};\n\n"
                + $"{Field}{Reader} GeometryReader = new {Reader}();\n\n"
                + $"{Field}{Writer} GeometryWriter = new {Writer}();\n\n"
                + "        // Makes the fields at the first spatial query, after the application has set\n"
                + "        // NtsGeometryServices.Instance, which a reader takes when it is made.\n"
                + "        static NetTopologySuiteConversions() { }\n"
                + "    }\n}\n"
        );
    }

    [Fact]
    public void HintNameOf_NetTopologySuite_IsAFileNoTypeOrModelCanHave() =>
        LibraryConverters
            .HintNameOf(SqlLibrary.NetTopologySuite)
            .ShouldBe("SqlSource-NetTopologySuiteConversions.g.cs");

    // NetTopologySuite has no edges: the spatial read path is no converter.
    [Fact]
    public void Of_WithNetTopologySuite_IsEmpty() =>
        LibraryConverters
            .Of(
                new LibraryProbe(
                    NodaTime: false,
                    NpgsqlNodaTime: false,
                    NodaTimeVersion: null,
                    NetTopologySuite: true,
                    NetTopologySuiteAtFloor: true,
                    SqlServerBytes: true
                )
            )
            .ShouldBe(EquatableArray<Converter>.Empty);

    [Theory]
    [InlineData("2.5.0")]
    [InlineData("2.6.0")]
    public void SourceOf_NetTopologySuite_CompilesWithoutWarnings(string version)
    {
        var compilation = GeneratorHarness.CreateCompilation(
            [new SourceFile("/app/NetTopologySuiteConversions.g.cs", NetTopologySuiteConversions.Source)],
            references: [LibraryReferences.NetTopologySuite(version), LibraryReferences.SqlServerBytes("2.1.0")]
        );
        compilation = compilation.WithOptions(
            compilation
                .Options.WithGeneralDiagnosticOption(ReportDiagnostic.Error)
                .WithNullableContextOptions(NullableContextOptions.Enable)
        );

        compilation
            .GetDiagnostics(TestContext.Current.CancellationToken)
            .Where(diagnostic => diagnostic.Severity >= DiagnosticSeverity.Warning)
            .Select(diagnostic => diagnostic.ToString())
            .ShouldBeEmpty();
    }

    // The suppressor of CS0436 knows a library's class by these names, so each class that a source declares is among
    // them, and nothing else is.
    [Fact]
    public void ClassMetadataNames_ComparedWithTheSources_AreEveryClassTheyDeclare()
    {
        var declared = System
            .Enum.GetValues<SqlLibrary>()
            .Select(LibraryConverters.SourceOf)
            .OfType<string>()
            .SelectMany(source =>
                CSharpSyntaxTree
                    .ParseText(source, cancellationToken: TestContext.Current.CancellationToken)
                    .GetRoot(TestContext.Current.CancellationToken)
                    .DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            )
            .Select(declaration =>
                declaration
                    .Ancestors()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.BaseNamespaceDeclarationSyntax>()
                    .Single()
                    .Name
                + "."
                + declaration.Identifier.ValueText
            );

        LibraryConverters.ClassMetadataNames.ShouldBe(declared, ignoreOrder: true);
    }

    private static LibraryProbe Probe() => new(NodaTime: true, NpgsqlNodaTime: false, NodaTimeVersion: null);

    private static LibraryProbe JsonProbe() =>
        new(NodaTime: false, NpgsqlNodaTime: false, NodaTimeVersion: null, Json: true);
}
