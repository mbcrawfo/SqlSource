// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation.Converters;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The class of a library's conversions, SqlSource.NodaTimeConversions or SqlSource.JsonConversions, which a project
// gets once, in a file of its own, when a method of a CodeGen query calls one of its conversions, and never otherwise.
// Each run is under mssql on SqlClient 7 with NodaTime 3.3.7, unless it says otherwise, with an entry written for each
// query's current hash.
public sealed class LibraryConversionTests
{
    // The file of the class, as the tests name it, not taken from the product.
    private const string ClassFile = "SqlSource-NodaTimeConversions.g.cs";

    private const string Conversions = "global::SqlSource.NodaTimeConversions.";

    private const string DateTime = "global::System.DateTime";

    private const string Instant = "global::NodaTime.Instant";

    private const string NodaTimeOn = "-- library-types: nodatime=on\n\n";

    // A query that reads a date and finds by one.
    private const string FindDates = "-- name: FindDates\nSELECT d FROM dates WHERE d = @d;\n";

    // System.Text.Json's class, as the tests name it, not taken from the product.
    private const string JsonClassFile = "SqlSource-JsonConversions.g.cs";

    private const string Json = "global::SqlSource.JsonConversions.";

    private const string JsonOn = "-- library-types: json=on\n\n";

    private const string JsonElement = "global::System.Text.Json.JsonElement";

    private const string JsonDocument = "global::System.Text.Json.JsonDocument";

    private const string JsonNode = "global::System.Text.Json.Nodes.JsonNode";

    // A query that reads a document that is not null and one that can be, and finds by both.
    private const string FindDocs =
        "-- name: FindDocs\nSELECT doc, maybe_doc FROM docs WHERE doc = @doc AND maybe_doc = @maybe_doc;\n";

    private static readonly SqlServerType Date = TestSidecars.SqlServer("date", 3, 10);

    private static readonly SqlServerType DateTime2 = TestSidecars.SqlServer("datetime2", 8, 27, 7);

    private static readonly SqlServerType SqlServerJson = TestSidecars.SqlServer("json", -1, 0);

    private static readonly SqlServerType NVarCharMax = TestSidecars.SqlServer("nvarchar(max)", -1, 0);

    // The nodes, as an override names them.
    private static readonly string[] NodeTypes =
    [
        "System.Text.Json.Nodes.JsonNode",
        "System.Text.Json.Nodes.JsonObject",
        "System.Text.Json.Nodes.JsonArray",
    ];

    // SqlClient 7 and NodaTime 3.3.7.
    private static MetadataReference[] WithNodaTime =>
        [SqlClientReferences.SqlClient7, LibraryReferences.NodaTime("3.3.7")];

    [Fact]
    public void Run_NodaTimeOnAndACodeGenQuery_EmitsTheClass()
    {
        var run = RunDates(NodaTimeOn + FindDates, WithNodaTime);

        run.ShouldBeClean();
        run.Sources[ClassFile].ShouldBe(NodaTimeConversions.Source);
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(" + Conversions + "LocalDateFromDateTime(reader.GetFieldValue<" + DateTime + ">(0)))");
        methods.ShouldContain("parameter.Value = " + Conversions + "DateTimeFromLocalDate(parameters.D);\n");
    }

    // The class's file starts as every generated file does.
    [Fact]
    public void Run_NodaTimeOnAndACodeGenQuery_EmitsAFileThatStartsAsAGeneratedOne()
    {
        var run = RunDates(NodaTimeOn + FindDates, WithNodaTime);

        run.Sources[ClassFile].ShouldStartWith(GeneratedFile.ModelHeader);
        run.Sources[ClassFile].ShouldContain("namespace SqlSource\n{\n    internal static class NodaTimeConversions\n");
    }

    // A type at Models has the library's types in no model and calls nothing.
    [Fact]
    public void Run_NodaTimeOnAndOnlyAModelsQuery_EmitsNoClass()
    {
        const string Models = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Output = GeneratorOutput.Models)]
                public partial class Sample;
            }
            """;

        var run = RunDates(NodaTimeOn + FindDates, WithNodaTime, Models);

        run.ShouldBeClean();
        run.Sources.Keys.ShouldNotContain(ClassFile);
    }

    // NodaTime is on, and the project does not reference it: SQLSRC281, the base map's types, and no class.
    [Fact]
    public void Run_NodaTimeOnWithoutTheAssembly_EmitsNoClass()
    {
        var run = RunDates(NodaTimeOn + FindDates, [SqlClientReferences.SqlClient7]);

        run.Problems().ShouldHaveSingleItem().ShouldStartWith("SQLSRC281 ");
        run.Sources.Keys.ShouldNotContain(ClassFile);
        run.Sources[MethodRuns.MethodsFile].ShouldContain("parameter.Value = parameters.D;\n");
    }

    // NodaTime referenced and nothing that turns it on or names one of its types: the methods are what they were
    // without the reference, byte for byte, and there is no class.
    [Fact]
    public void Run_NodaTimeReferencedAndNothingOn_IsUnchanged()
    {
        var without = RunDates(FindDates, [SqlClientReferences.SqlClient7]);
        var with = RunDates(FindDates, WithNodaTime);

        without.ShouldBeClean();
        with.ShouldBeClean();
        with.Sources[MethodRuns.MethodsFile].ShouldBe(without.Sources[MethodRuns.MethodsFile]);
        with.Sources.Keys.ShouldNotContain(ClassFile);
        without.Sources.Keys.ShouldNotContain(ClassFile);
    }

    // On PostgreSQL the plugin teaches Npgsql NodaTime's types, so its rows go to the driver and call nothing.
    [Fact]
    public void Run_PostgresPluginRow_EmitsNoClass()
    {
        var timestamptz = Catalog("timestamptz") with { Name = "timestamp with time zone" };
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListEvents\nSELECT at FROM events;\n");

        var run = MethodRuns.Run(
            file,
            [Entry(file, "ListEvents", [], [Column("at", timestamptz)])],
            references:
            [
                NpgsqlReferences.Npgsql10,
                LibraryReferences.NodaTime("3.3.7"),
                LibraryReferences.NpgsqlNodaTime("10.0.3"),
            ]
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("reader.GetFieldValue<" + Instant + ">(0)");
        run.Sources.Keys.ShouldNotContain(ClassFile);
    }

    // An override to one of NodaTime's types finds the library's edge whether the switch is on or not, and the class
    // is emitted for it.
    [Theory]
    [InlineData("datetime2", "NodaTime.Instant", "InstantFromDateTime", "DateTimeFromInstant")]
    [InlineData("time", "NodaTime.Duration", "DurationFromTimeSpan", "TimeSpanFromDuration")]
    [InlineData("datetimeoffset", "NodaTime.Instant", "InstantFromDateTimeOffset", "DateTimeOffsetFromInstant")]
    public void Run_OverrideWithTheSwitchOff_GetsTheConversion(
        string databaseType,
        string type,
        string read,
        string write
    )
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: "
                + databaseType
                + "="
                + type
                + "\n-- name: FindAt\nSELECT at FROM events WHERE at = @at;\n"
        );
        var sqlType = TypeOf(databaseType);

        var run = MethodRuns.RunSqlServer(
            file,
            [
                MethodRuns.SqlServerEntry(
                    file,
                    "FindAt",
                    [TestSidecars.Parameter(0, "at", sqlType, false)],
                    [TestSidecars.Column(0, "at", sqlType)]
                ),
            ],
            references: WithNodaTime
        );

        run.ShouldBeClean();
        run.Sources[ClassFile].ShouldBe(NodaTimeConversions.Source);
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(Conversions + read + "(reader.GetFieldValue<");
        methods.ShouldContain("parameter.Value = " + Conversions + write + "(parameters.At);\n");
    }

    // The project's converter of DateTime to LocalDate replaces the library's read, and not its write, which is the
    // other edge.
    [Fact]
    public void Run_ProjectConverterOfTheEdge_ReplacesTheLibrarys()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample;

                public static class Dates
                {
                    [SqlSourceConverter]
                    public static global::NodaTime.LocalDate FromDb(global::System.DateTime value) =>
                        global::NodaTime.LocalDate.FromDateTime(value);
                }
            }
            """;

        var run = RunDates(NodaTimeOn + FindDates, WithNodaTime, Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(global::App.Dates.FromDb(reader.GetFieldValue<" + DateTime + ">(0)))");
        methods.ShouldNotContain("LocalDateFromDateTime");
        methods.ShouldContain("parameter.Value = " + Conversions + "DateTimeFromLocalDate(parameters.D);\n");
        run.Sources.Keys.ShouldContain(ClassFile);
    }

    // A referenced assembly's Lib.Clock, which one query maps a bigint to, declares a converter of DateTime to
    // Instant, which joins the set with Clock's own: the other query's read of a datetime2 overridden to Instant calls
    // it, and its write calls the library's.
    [Fact]
    public void Run_MappedTypeConverter_ReplacesTheLibrarys()
    {
        var library = GeneratorHarness
            .CreateProject(
                OutputKind.Sql,
                "Lib",
                """
                namespace Lib;

                public readonly record struct Clock(long Ticks)
                {
                    [SqlSource.SqlSourceConverter]
                    public static Clock FromTicks(long value) => new(value);

                    [SqlSource.SqlSourceConverter]
                    public static long ToTicks(Clock clock) => clock.Ticks;

                    [SqlSource.SqlSourceConverter]
                    public static NodaTime.Instant FromDb(System.DateTime value) =>
                        NodaTime.Instant.FromDateTimeUtc(System.DateTime.SpecifyKind(value, System.DateTimeKind.Utc));
                }
                """,
                references: [LibraryReferences.NodaTime("3.3.7")]
            )
            .Reference;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: bigint=Lib.Clock\n-- type-override: datetime2=NodaTime.Instant\n\n"
                + "-- name: GetClock\nSELECT ticks FROM clocks;\n\n"
                + "-- name: FindAt\nSELECT at FROM events WHERE at = @at;\n"
        );

        var run = MethodRuns.RunSqlServer(
            file,
            [
                MethodRuns.SqlServerEntry(
                    file,
                    "GetClock",
                    [],
                    [TestSidecars.Column(0, "ticks", TestSidecars.SqlServer("bigint", 8, 19))]
                ),
                MethodRuns.SqlServerEntry(
                    file,
                    "FindAt",
                    [TestSidecars.Parameter(0, "at", DateTime2, false)],
                    [TestSidecars.Column(0, "at", DateTime2)]
                ),
            ],
            references: [.. WithNodaTime, library]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("(global::Lib.Clock.FromDb(reader.GetFieldValue<" + DateTime + ">(0)))");
        methods.ShouldNotContain("InstantFromDateTime");
        methods.ShouldContain("parameter.Value = " + Conversions + "DateTimeFromInstant(parameters.At);\n");
        run.Sources.Keys.ShouldContain(ClassFile);
    }

    // Every row of NodaTime's, and the three overrides, read and written, nullable and not, compile with no error and
    // no warning in what is generated, against SqlClient 6.1 and 7 and the oldest NodaTime and the newest.
    [Theory]
    [InlineData(false, "3.1.9")]
    [InlineData(false, "3.3.7")]
    [InlineData(true, "3.1.9")]
    [InlineData(true, "3.3.7")]
    public void Run_GeneratedCode_CompilesWithoutWarnings(bool sqlClient7, string nodaTime)
    {
        SqlServerType[] rows =
        [
            Date,
            TestSidecars.SqlServer("time", 5, 16, 7),
            DateTime2,
            TestSidecars.SqlServer("datetime", 8, 23, 3),
            TestSidecars.SqlServer("smalldatetime", 4, 16),
            TestSidecars.SqlServer("datetimeoffset", 10, 34, 7),
        ];
        SqlServerType[] overridden =
        [
            DateTime2,
            TestSidecars.SqlServer("time", 5, 16, 7),
            TestSidecars.SqlServer("datetimeoffset", 10, 34, 7),
        ];
        var everyRow = new SqlFile(GeneratorHarness.UsersPath, NodaTimeOn + EverythingQuery("Rows", rows.Length));
        var overrides = new SqlFile(
            "/app/Repo/Overrides.sql",
            "-- type-override: datetime2=NodaTime.Instant\n-- type-override: time=NodaTime.Duration\n"
                + "-- type-override: datetimeoffset=NodaTime.Instant\n\n"
                + EverythingQuery("Overrides", overridden.Length)
        );

        var run = MethodRuns.RunSqlServer(
            everyRow,
            [EverythingEntry(everyRow, "Rows", rows)],
            references:
            [
                sqlClient7 ? SqlClientReferences.SqlClient7 : SqlClientReferences.SqlClient61,
                LibraryReferences.NodaTime(nodaTime),
            ],
            otherFiles: [overrides, Sidecars.Of(overrides, EverythingEntry(overrides, "Overrides", overridden))]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(Conversions + "OffsetDateTimeFromDateTimeOffset(");
        methods.ShouldContain(Conversions + "DurationFromTimeSpan(");
        run.Sources.Keys.ShouldContain(ClassFile);
    }

    // json on: SQL Server's json is a JsonElement, which the driver reads and binds as a string, so the class's
    // conversions turn it from and to one, behind the check of a NULL for the column that can be one.
    [Fact]
    public void Run_SqlServerJsonColumnWithJsonOn_ReadsAndWritesThroughJsonConversions()
    {
        var run = RunSqlServerDocs(JsonOn + FindDocs, SqlServerJson);

        run.ShouldBeClean();
        run.Sources[JsonClassFile].ShouldBe(JsonConversions.Source);
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("FindDocsDto(" + Json + "JsonElementFromString(reader.GetFieldValue<string>(0)), ");
        methods.ShouldContain(
            "reader.IsDBNull(1) ? default("
                + JsonElement
                + "?) : "
                + Json
                + "JsonElementFromString(reader.GetFieldValue<string>(1))"
        );
        methods.ShouldContain("parameter.Value = " + Json + "StringFromJsonElement(parameters.Doc);\n");
        methods.ShouldContain(
            "parameter.Value = parameters.MaybeDoc is { } value ? (object)"
                + Json
                + "StringFromJsonElement(value) : global::System.DBNull.Value;\n"
        );
        methods.ShouldContain(
            "parameter.SqlDbType = global::System.Data.SqlDbType.NVarChar;\n                    parameter.Size = -1;\n"
        );
        methods.ShouldNotContain("NodaTimeConversions");
    }

    // An override to each of System.Text.Json's types, on nvarchar(max) and on json with the switch off: each column
    // is converted by the edge of its type.
    [Theory]
    [InlineData("System.Text.Json.JsonDocument", "JsonDocumentFromString")]
    [InlineData("System.Text.Json.Nodes.JsonNode", "JsonNodeFromString")]
    [InlineData("System.Text.Json.Nodes.JsonObject", "JsonObjectFromString")]
    [InlineData("System.Text.Json.Nodes.JsonArray", "JsonArrayFromString")]
    [InlineData("System.Text.Json.JsonElement", "JsonElementFromString")]
    public void Run_SqlServerOverridesToEachJsonType_ConvertWithJsonOff(string type, string read)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListDocs\n-- column: text_doc -> "
                + type
                + "\n-- column: json_doc -> "
                + type
                + "\nSELECT text_doc, json_doc FROM docs;\n"
        );

        var run = MethodRuns.RunSqlServer(
            file,
            [
                MethodRuns.SqlServerEntry(
                    file,
                    "ListDocs",
                    [],
                    [
                        TestSidecars.Column(0, "text_doc", NVarCharMax, nullable: true),
                        TestSidecars.Column(0, "json_doc", SqlServerJson, nullable: true),
                    ]
                ),
            ]
        );

        run.ShouldBeClean();
        run.Sources[JsonClassFile].ShouldBe(JsonConversions.Source);
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" : " + Json + read + "(reader.GetFieldValue<string>(0))");
        methods.ShouldContain(" : " + Json + read + "(reader.GetFieldValue<string>(1))");
    }

    // Npgsql before 10 reads and writes no JsonNode, so its edge converts it from and to the string Npgsql reads jsonb
    // as; Npgsql 10 reads and writes it itself, and nothing is converted.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_PostgresJsonNode_ByEdgeOnNpgsql8AndByTheDriverOn10(bool npgsql10)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: jsonb=System.Text.Json.Nodes.JsonNode\n\n-- name: FindDocs\n"
                + "SELECT doc FROM docs WHERE doc = @doc;\n"
        );
        var jsonb = Catalog("jsonb") with { BindName = "jsonb" };

        var run = MethodRuns.Run(
            file,
            [
                Entry(
                    file,
                    "FindDocs",
                    [Parameter("doc", jsonb, nullable: false)],
                    [Column("doc", jsonb, nullable: true)]
                ),
            ],
            references: [npgsql10 ? NpgsqlReferences.Npgsql10 : NpgsqlReferences.Npgsql8]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        if (npgsql10)
        {
            methods.ShouldContain("reader.GetFieldValue<" + JsonNode + ">(0)");
            methods.ShouldContain("new global::Npgsql.NpgsqlParameter<" + JsonNode + ">();");
            methods.ShouldNotContain("JsonConversions");
            run.Sources.Keys.ShouldNotContain(JsonClassFile);
        }
        else
        {
            methods.ShouldContain(" : " + Json + "JsonNodeFromString(reader.GetFieldValue<string>(0))");
            methods.ShouldContain("new global::Npgsql.NpgsqlParameter<string>();");
            methods.ShouldContain(
                "parameter.TypedValue = parameters.Doc is { } value ? " + Json + "StringFromJsonNode(value) : null;\n"
            );
            run.Sources[JsonClassFile].ShouldBe(JsonConversions.Source);
        }

        methods.ShouldContain(" parameter.DataTypeName = \"pg_catalog.jsonb\";\n");
    }

    // Every version of Npgsql reads and writes the element and the document, so the library's row and an override go
    // to the driver, and no class is emitted.
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_PostgresJsonElementAndDocument_GoToTheDriver(bool npgsql10)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- library-types: json=on\n-- type-override: jsonb=System.Text.Json.JsonDocument\n\n-- name: FindDocs\n"
                + "SELECT element, document FROM docs WHERE element = @element AND document = @document;\n"
        );
        var json = Catalog("json") with { BindName = "json" };
        var jsonb = Catalog("jsonb") with { BindName = "jsonb" };

        var run = MethodRuns.Run(
            file,
            [
                Entry(
                    file,
                    "FindDocs",
                    [Parameter("element", json, nullable: false), Parameter("document", jsonb, nullable: false)],
                    [Column("element", json), Column("document", jsonb)]
                ),
            ],
            references: [npgsql10 ? NpgsqlReferences.Npgsql10 : NpgsqlReferences.Npgsql8]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("reader.GetFieldValue<" + JsonElement + ">(0)");
        methods.ShouldContain("reader.GetFieldValue<" + JsonDocument + ">(1)");
        methods.ShouldContain("new global::Npgsql.NpgsqlParameter<" + JsonElement + ">();");
        methods.ShouldContain("new global::Npgsql.NpgsqlParameter<" + JsonDocument + ">();");
        methods.ShouldContain(" parameter.DataTypeName = \"pg_catalog.json\";\n");
        methods.ShouldContain(" parameter.DataTypeName = \"pg_catalog.jsonb\";\n");
        methods.ShouldNotContain("JsonConversions");
        run.Sources.Keys.ShouldNotContain(JsonClassFile);
    }

    // The project's converters of string and JsonElement replace both of the library's, with no diagnostic, and the
    // class is not emitted, since nothing calls it.
    [Fact]
    public void Run_ProjectConverterOfJsonElement_ReplacesTheLibrarys()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample;

                public static class Documents
                {
                    [SqlSourceConverter]
                    public static global::System.Text.Json.JsonElement FromDb(string value) =>
                        global::System.Text.Json.JsonDocument.Parse(value).RootElement;

                    [SqlSourceConverter]
                    public static string ToDb(global::System.Text.Json.JsonElement value) => value.GetRawText();
                }
            }
            """;

        var run = RunSqlServerDocs(JsonOn + FindDocs, SqlServerJson, Source);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("FindDocsDto(global::App.Documents.FromDb(reader.GetFieldValue<string>(0)), ");
        methods.ShouldContain("parameter.Value = global::App.Documents.ToDb(parameters.Doc);\n");
        methods.ShouldNotContain("JsonConversions");
        run.Sources.Keys.ShouldNotContain(JsonClassFile);
    }

    // A type at Models has JsonElement in its models and calls nothing.
    [Fact]
    public void Run_NoCodeGenUse_EmitsNoJsonConversions()
    {
        const string Models = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Output = GeneratorOutput.Models)]
                public partial class Sample;
            }
            """;

        var run = RunSqlServerDocs(JsonOn + FindDocs, SqlServerJson, Models);

        run.ShouldBeClean();
        run.Sources.Keys.ShouldNotContain(JsonClassFile);
    }

    // Every edge of System.Text.Json that a query of SQL Server and one of Npgsql 8 use, read and written, nullable and
    // not, whole and, on Npgsql, by element, compiles with no error and no warning in what is generated, against each
    // driver, with the reference assemblies of .NET 8 and with the runtime's, of .NET 10.  Npgsql 10 reads and writes
    // the same nodes itself.  A not-null node, whose read can give null for a JSON null, throws on one.
    [Theory]
    [InlineData("SqlClient 6.1", false)]
    [InlineData("SqlClient 6.1", true)]
    [InlineData("SqlClient 7", false)]
    [InlineData("SqlClient 7", true)]
    [InlineData("Npgsql 8", false)]
    [InlineData("Npgsql 8", true)]
    [InlineData("Npgsql 10", false)]
    [InlineData("Npgsql 10", true)]
    public void Run_JsonGeneratedCode_CompilesWithoutWarnings(string driver, bool net8)
    {
        string[] types = ["System.Text.Json.JsonElement", "System.Text.Json.JsonDocument", .. NodeTypes];
        var isSqlServer = driver.StartsWith("SqlClient", System.StringComparison.Ordinal);
        string[] postgres =
        [
            .. NodeTypes,
            .. NodeTypes.Select(node => node + "[]"),
            .. NodeTypes.Select(node => node + "?[]"),
        ];
        var used = isSqlServer ? types : postgres;
        var markers = new System.Text.StringBuilder();
        for (var index = 0; index < used.Length; index++)
        {
            var name = "v" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            _ = markers.Append("-- column: ").Append(name).Append(" -> ").Append(used[index]).Append('\n');
            _ = markers.Append("-- column: maybe_").Append(name).Append(" -> ").Append(used[index]).Append('\n');
            _ = markers.Append("-- param: @").Append(name).Append(" -> ").Append(used[index]).Append('\n');
            _ = markers.Append("-- param: @maybe_").Append(name).Append(" -> ").Append(used[index]).Append('\n');
        }

        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            EverythingQuery("Everything", used.Length, markers.ToString())
        );
        var entry = isSqlServer
            ? JsonEntry(file, used, NVarCharMax, SqlServerJson)
            : JsonEntry(
                file,
                used,
                Catalog("jsonb") with
                {
                    BindName = "jsonb",
                },
                Catalog("json") with
                {
                    BindName = "json",
                }
            );
        var driverReference = driver switch
        {
            "SqlClient 6.1" => SqlClientReferences.SqlClient61,
            "SqlClient 7" => SqlClientReferences.SqlClient7,
            "Npgsql 8" => NpgsqlReferences.Npgsql8,
            _ => NpgsqlReferences.Npgsql10,
        };

        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, MethodRuns.Source)],
            [file, Sidecars.Of(file, entry)],
            supportedFramework: !net8,
            references: net8 ? [.. LibraryReferences.Net8Framework, driverReference] : [driverReference],
            dialect: isSqlServer ? "mssql" : "postgres"
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("holds a JSON null.");
        if (driver == "Npgsql 10")
        {
            methods.ShouldNotContain("JsonConversions");
            run.Sources.Keys.ShouldNotContain(JsonClassFile);
            return;
        }

        foreach (var each in used.Where(each => !each.EndsWith("[]", System.StringComparison.Ordinal)))
        {
            var shortName = each[(each.LastIndexOf('.') + 1)..];
            methods.ShouldContain(Json + shortName + "FromString(");
            methods.ShouldContain(Json + "StringFrom" + shortName + "(");
        }

        run.Sources[JsonClassFile].ShouldBe(JsonConversions.Source);
    }

    // The entry of the query of Run_JsonGeneratedCode_CompilesWithoutWarnings, over the types the override names: v<i>
    // and its parameter not null, and maybe_v<i> nullable; the even ones of the first database type and the odd ones
    // of the second, or of its array for an array.
    private static SidecarEntry JsonEntry(SqlFile file, string[] types, SidecarType even, SidecarType odd)
    {
        var parameters = new System.Collections.Generic.List<SidecarParameter>();
        var columns = new System.Collections.Generic.List<SidecarColumn>();
        for (var index = 0; index < types.Length; index++)
        {
            var type = index % 2 == 0 ? even : odd;
            if (types[index].EndsWith("[]", System.StringComparison.Ordinal) && type is PostgresType element)
            {
                type = ArrayOf(element, "_" + element.InternalName) with { BindName = element.BindName + "[]" };
            }

            var name = "v" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            parameters.Add(TestSidecars.Parameter(0, name, type, false));
            parameters.Add(TestSidecars.Parameter(0, "maybe_" + name, type, true));
            columns.Add(TestSidecars.Column(0, name, type, nullable: false));
            columns.Add(TestSidecars.Column(0, "maybe_" + name, type, nullable: true));
        }

        return even is SqlServerType
            ? MethodRuns.SqlServerEntry(file, "Everything", [.. parameters], [.. columns])
            : Entry(file, "Everything", [.. parameters], [.. columns]);
    }

    // The type of SQL Server of the name, with the facets the tool writes for it.
    private static SqlServerType TypeOf(string name) =>
        name switch
        {
            "datetime2" => DateTime2,
            "time" => TestSidecars.SqlServer("time", 5, 16, 7),
            _ => TestSidecars.SqlServer("datetimeoffset", 10, 34, 7),
        };

    // A query of the name that reads v0 to v<count - 1> and maybe_v0 to maybe_v<count - 1>, and finds by parameters
    // of the same names, with the markers after its name.
    private static string EverythingQuery(string name, int count, string markers = "")
    {
        var names = Enumerable
            .Range(0, count)
            .Select(index => "v" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        return "-- name: "
            + name
            + "\n"
            + markers
            + "SELECT "
            + string.Join(", ", names.Select(each => each + ", maybe_" + each))
            + " FROM everything WHERE "
            + string.Join(
                " AND ",
                names.Select(each => each + " = @" + each + " AND maybe_" + each + " = @maybe_" + each)
            )
            + ";\n";
    }

    // The entry of EverythingQuery over these types: each name's parameter and column not null, and each maybe_
    // name's nullable.
    private static SidecarEntry EverythingEntry(SqlFile file, string name, SqlServerType[] types)
    {
        var parameters = types
            .SelectMany(
                (type, index) =>
                    new[]
                    {
                        TestSidecars.Parameter(0, "v" + index, type, false),
                        TestSidecars.Parameter(0, "maybe_v" + index, type, true),
                    }
            )
            .ToArray();
        var columns = types
            .SelectMany(
                (type, index) =>
                    new[]
                    {
                        TestSidecars.Column(0, "v" + index, type, nullable: false),
                        TestSidecars.Column(0, "maybe_v" + index, type, nullable: true),
                    }
            )
            .ToArray();
        return MethodRuns.SqlServerEntry(file, name, parameters, columns);
    }

    // The query of the text, FindDocs, whose column and parameter doc are not null and maybe_doc can be, all of the
    // type, at CodeGen under mssql on the source, App.Sample unless another is given.
    private static GeneratorRun RunSqlServerDocs(string text, SqlServerType type, string? source = null)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        return MethodRuns.RunSqlServer(
            file,
            [
                MethodRuns.SqlServerEntry(
                    file,
                    "FindDocs",
                    [TestSidecars.Parameter(0, "doc", type, false), TestSidecars.Parameter(0, "maybe_doc", type, true)],
                    [
                        TestSidecars.Column(0, "doc", type, nullable: false),
                        TestSidecars.Column(0, "maybe_doc", type, nullable: true),
                    ]
                ),
            ],
            source ?? MethodRuns.Source
        );
    }

    // FindDates, or the query of the text, whose column and parameter d are a not-null date, at CodeGen under mssql on
    // the source, App.Sample unless another is given.
    private static GeneratorRun RunDates(string text, MetadataReference[] references, string? source = null)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        return MethodRuns.RunSqlServer(
            file,
            [
                MethodRuns.SqlServerEntry(
                    file,
                    "FindDates",
                    [TestSidecars.Parameter(0, "d", Date, false)],
                    [TestSidecars.Column(0, "d", Date)]
                ),
            ],
            source ?? MethodRuns.Source,
            references
        );
    }
}
