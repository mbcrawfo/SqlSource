// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Converters;
using SqlSource.Generation.Types;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;

namespace SqlSource.Tests.Generator;

// The library map, step 4 of a column's type: below a -- column: marker, a column type and the type override, above the
// base map, for a library that is on for the query by its level of markers and settings files, or by the engine's
// default when none says.  Each run is under postgres on Npgsql 10, with an entry written for each query's current
// hash, and NodaTime and its Npgsql plugin when it says so.  Typing runs once for a file, whatever the output of the
// types that claim it, so a property of a library row keeps beside it the type it would have without the library.
public sealed class LibraryTypeTests
{
    private const string Instant = "global::NodaTime.Instant";

    private const string DateTime = "global::System.DateTime";

    private const string DateTimeOffset = "global::System.DateTimeOffset";

    // The settings file of the folder of GeneratorHarness.UsersPath.
    private const string FolderSettingsPath = "/app/Repo/sqlsource.json";

    private const string ListEvents = "-- name: ListEvents\nSELECT at FROM events;\n";

    private static readonly PostgresType Timestamptz = Catalog("timestamptz") with
    {
        Name = "timestamp with time zone",
    };

    private static readonly SidecarOrigin EventsAt = new("public", "events", "at");

    // What the base map gives timestamptz, with no place.
    private static readonly PropertyType DateTimeOfTheMap = new(
        DateTime,
        true,
        true,
        EquatableArray<string>.Empty,
        "timestamptz",
        null
    );

    // What NodaTime's row gives it, a place of its own whose database side is the base map's type, with the base map's
    // type beside it.
    private static readonly PropertyType InstantOfTheLibrary = new(
        Instant,
        true,
        true,
        EquatableArray<string>.Empty,
        "timestamptz",
        "the NodaTime library",
        new DatabaseType(DateTime, true, false),
        Library: SqlLibrary.NodaTime,
        WithoutLibrary: DateTimeOfTheMap
    );

    // Npgsql, NodaTime and the plugin, which turn the library on by default.
    private static MetadataReference[] WithThePlugin =>
        [NpgsqlReferences.Npgsql10, LibraryReferences.NodaTime("3.3.7"), LibraryReferences.NpgsqlNodaTime("10.0.3")];

    [Fact]
    public void Detected_PluginReferenced_TimestamptzIsInstant()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, ListEvents);

        var run = MethodRuns.Run(file, [EventsEntry(file)], references: WithThePlugin);

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run).ShouldBe([InstantOfTheLibrary]);
        QueryOf(run).UnavailableLibraries.ShouldBeEmpty();
        // The plugin reads the type, so the method reads it as it is, with no conversion.
        run.Sources[MethodRuns.MethodsFile].ShouldContain("reader.GetFieldValue<global::NodaTime.Instant>(0)");
    }

    // Typing does not know the output, so a query at Models is typed the same, and its models pick by the output.
    [Fact]
    public void Detected_PluginReferenced_TimestamptzIsInstant_AtModels()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, ListEvents);

        var run = Run(Source, [file, Sidecars.Of(file, EventsEntry(file))], references: WithThePlugin);

        run.ShouldBeClean();
        var column = StepOutputs.ColumnsOf(run).ShouldHaveSingleItem();
        column.ShouldBe(InstantOfTheLibrary);
        column.WithoutLibrary.ShouldNotBeNull().Text.ShouldBe(DateTime);
    }

    // NodaTime alone is not enough on PostgreSQL: the driver reads its types only through the plugin.
    [Fact]
    public void NotDetected_NoPlugin_TimestamptzIsDateTime()
    {
        var run = RunEvents(ListEvents, [NpgsqlReferences.Npgsql10, LibraryReferences.NodaTime("3.3.7")]);

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run).ShouldBe([DateTimeOfTheMap]);
        QueryOf(run).UnavailableLibraries.ShouldBeEmpty();
    }

    [Fact]
    public void SettingsFileOff_TurnsADetectedLibraryOff()
    {
        var run = RunEvents(ListEvents, WithThePlugin, NodaTimeIs("off"));

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run).ShouldBe([DateTimeOfTheMap]);
    }

    [Fact]
    public void HeaderOn_OverSettingsFileOff()
    {
        var run = RunEvents("-- library-types: nodatime=on\n\n" + ListEvents, WithThePlugin, NodaTimeIs("off"));

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run).ShouldBe([InstantOfTheLibrary]);
    }

    // The query's marker turns the library off for that query alone, and the other query of the file keeps its
    // header's.
    [Fact]
    public void QueryOff_OverHeaderOn()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- library-types: nodatime=on\n\n-- name: ListEvents\n-- library-types: nodatime=off\n"
                + "SELECT at FROM events;\n\n-- name: ListOthers\nSELECT at FROM events;\n"
        );

        var run = MethodRuns.Run(
            file,
            [EventsEntry(file), EventsEntry(file, "ListOthers")],
            references: WithThePlugin,
            settings: NodaTimeIs("off")
        );

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run, "ListEvents").ShouldBe([DateTimeOfTheMap]);
        StepOutputs.ColumnsOf(run, "ListOthers").ShouldBe([InstantOfTheLibrary]);
    }

    // A nearer default takes the library out of the farther levels, so the engine's default applies: detection, which
    // finds the plugin.
    [Fact]
    public void Default_HidesAFartherOff_GivesDetection()
    {
        var run = RunEvents(ListEvents, WithThePlugin, NodaTimeIs("off"), folderFile: NodaTimeIs("default"));

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run).ShouldBe([InstantOfTheLibrary]);
    }

    [Fact]
    public void NearerSettingsFile_WinsOverFarther()
    {
        var run = RunEvents(ListEvents, WithThePlugin, NodaTimeIs("on"), folderFile: NodaTimeIs("off"));

        run.ShouldBeClean();
        StepOutputs.ColumnsOf(run).ShouldBe([DateTimeOfTheMap]);
    }

    // Each one alone chooses the column's type over the library, which is on.
    [Theory]
    [InlineData("columnMarker")]
    [InlineData("columnType")]
    [InlineData("typeOverride")]
    public void ColumnMarker_ColumnType_Override_WinOverTheLibrary(string chooser)
    {
        var text =
            chooser == "columnMarker"
                ? "-- name: ListEvents\n-- column: at -> System.DateTimeOffset\nSELECT at FROM events;\n"
                : ListEvents;
        var settings = chooser switch
        {
            "columnType" => Settings("columnTypes", new JsonObject { ["events.at"] = "System.DateTimeOffset" }),
            "typeOverride" => Settings("typeOverrides", new JsonObject { ["timestamptz"] = "System.DateTimeOffset" }),
            _ => null,
        };

        var run = RunEvents(text, WithThePlugin, settings);

        run.ShouldBeClean();
        var column = StepOutputs.ColumnsOf(run).ShouldHaveSingleItem();
        column.Text.ShouldBe(DateTimeOffset);
        _ = column.Place.ShouldNotBeNull();
        column.Library.ShouldBeNull();
        column.WithoutLibrary.ShouldBeNull();
    }

    // The parameter's marker chooses its type; the column, which nothing chooses, has the library's.
    [Fact]
    public void ParamMarker_WinsOverTheLibrary()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListEvents\n-- param: @since -> System.DateTimeOffset\nSELECT at FROM events WHERE at > @since;\n"
        );
        var entry = Entry(
            file,
            "ListEvents",
            [Parameter("since", Timestamptz)],
            [Column("at", Timestamptz, origin: EventsAt)]
        );

        var run = Run(Source, [file, Sidecars.Of(file, entry)], references: WithThePlugin);

        run.ShouldBeClean();
        var query = QueryOf(run);
        var parameter = query.Parameters.ShouldHaveSingleItem();
        parameter.Text.ShouldBe(DateTimeOffset);
        parameter.Library.ShouldBeNull();
        parameter.WithoutLibrary.ShouldBeNull();
        query.Columns.ShouldNotBeNull().ShouldBe([InstantOfTheLibrary]);
    }

    // A parameter that nothing chooses a type for has the library's, as a column has.
    [Fact]
    public void Detected_PluginReferenced_ParameterIsInstant()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListEvents\nSELECT at FROM events WHERE at > @since;\n"
        );
        var entry = Entry(
            file,
            "ListEvents",
            [Parameter("since", Timestamptz)],
            [Column("at", Timestamptz, origin: EventsAt)]
        );

        var run = MethodRuns.Run(file, [entry], references: WithThePlugin);

        run.ShouldBeClean();
        QueryOf(run).Parameters.ShouldBe([InstantOfTheLibrary]);
    }

    // An array of a library's type is its element's with [], beside the base map's array.
    [Fact]
    public void Detected_PluginReferenced_ArrayIsOfTheElementsType()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, ListEvents);
        var entry = Entry(file, "ListEvents", [], [Column("at", ArrayOf(Timestamptz, "_timestamptz"))]);

        var run = Run(Source, [file, Sidecars.Of(file, entry)], references: WithThePlugin);

        run.ShouldBeClean();
        var column = StepOutputs.ColumnsOf(run).ShouldHaveSingleItem();
        column.Text.ShouldBe(Instant + "[]");
        column.Library.ShouldBe(SqlLibrary.NodaTime);
        column.WithoutLibrary.ShouldNotBeNull().Text.ShouldBe(DateTime + "[]");
    }

    // An assembly with NodaTime's and the plugin's types and no Npgsql: tstzrange has the library's row, while the base
    // map, with no driver, has no C# type for it.  The base map's failure is the query's, and the row is not used.
    [Fact]
    public void Detected_BaseMapFails_TheBaseMapsErrorIsTheQuerys()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, ListEvents);
        var range = Catalog("tstzrange", SidecarValues.Kind.Range, Timestamptz);
        var entry = Entry(file, "ListEvents", [], [Column("during", range)]);

        var run = Run(
            Source,
            [file, Sidecars.Of(file, entry)],
            references: [LibraryReferences.Synthetic("3.3.0.0", nodaTimeTypes: true, pluginExtensions: true)]
        );

        run.CompilationErrors.ShouldBeEmpty();
        run.Diagnostics.ShouldContain(diagnostic => diagnostic.StartsWith("SQLSRC249 ", StringComparison.Ordinal));
        run.Diagnostics.ShouldNotContain(diagnostic => diagnostic.Contains("NodaTime", StringComparison.Ordinal));
        StepOutputs.Single<FileTypes>(run, TrackingNames.FileTypes).Queries.ShouldBeEmpty();
    }

    // On, though the plugin is missing: the query keeps the base map's types, and the entry that says on is recorded.
    [Fact]
    public void On_WithoutPlugin_KeepsTheBaseTypes_AndRecordsTheEntry()
    {
        var run = RunEvents(ListEvents, [NpgsqlReferences.Npgsql10], NodaTimeIs("on"));

        run.CompilationErrors.ShouldBeEmpty();
        var query = QueryOf(run);
        query.Columns.ShouldNotBeNull().ShouldBe([DateTimeOfTheMap]);
        var entry = query.UnavailableLibraries.ShouldHaveSingleItem();
        entry.Library.ShouldBe(SqlLibrary.NodaTime);
        entry.State.ShouldBe(LibraryState.On);
        entry.Place.Kind.ShouldBe(SettingPlaceKind.SettingsFile);
        entry.Place.Path.ShouldBe(GeneratorHarness.ProjectSettingsPath);
    }

    // SQL Server has NodaTime's rows, and the library is on only when something says so.
    [Fact]
    public void Run_SqlServerNodaTimeOn_GivesCodeGenModelsNodaTimeTypes()
    {
        var run = RunDates(
            "-- library-types: nodatime=on\n\n-- name: ListDates\nSELECT d FROM dates;\n",
            [SqlClientReferences.SqlClient7, LibraryReferences.NodaTime("3.3.7")]
        );

        run.ShouldBeClean();
        var column = QueryOf(run).Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        column.Text.ShouldBe("global::NodaTime.LocalDate");
        column.Library.ShouldBe(SqlLibrary.NodaTime);
        run.Sources[ModelFiles(run).ShouldHaveSingleItem()].ShouldContain("(global::NodaTime.LocalDate? D);");
        run.Sources[MethodRuns.MethodsFile].ShouldContain("global::NodaTime.LocalDate");
    }

    [Fact]
    public void Run_SqlServerNodaTimeOn_ModelsClaimKeepsDateTime()
    {
        const string Models = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Output = GeneratorOutput.Models)]
                public partial class Rows;
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- library-types: nodatime=on\n\n-- name: ListDates\nSELECT d FROM dates;\n"
        );

        var run = MethodRuns.RunSqlServer(
            file,
            [DatesEntry(file)],
            Models,
            [SqlClientReferences.SqlClient7, LibraryReferences.NodaTime("3.3.7")]
        );

        run.ShouldBeClean();
        var model = ModelFiles(run).ShouldHaveSingleItem();
        run.Sources[model].ShouldContain("(" + DateTime + "? D);");
        run.Sources[model].ShouldNotContain("NodaTime");
    }

    [Fact]
    public void Run_SqlServerNodaTimeDefault_IsOff()
    {
        var run = RunDates(
            "-- name: ListDates\nSELECT d FROM dates;\n",
            [SqlClientReferences.SqlClient7, LibraryReferences.NodaTime("3.3.7")]
        );

        run.ShouldBeClean();
        var column = QueryOf(run).Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        column.Text.ShouldBe(DateTime);
        column.Library.ShouldBeNull();
        run.Sources[ModelFiles(run).ShouldHaveSingleItem()].ShouldContain("(" + DateTime + "? D);");
    }

    [Fact]
    public void SqlServer_NodaTimeOn_WithoutNodaTime_RecordsTheEntry()
    {
        var run = RunOrders([SqlClientReferences.SqlClient7]);

        run.CompilationErrors.ShouldBeEmpty();
        var query = QueryOf(run);
        query.Columns.ShouldNotBeNull().ShouldHaveSingleItem().Text.ShouldBe(DateTime);
        var entry = query.UnavailableLibraries.ShouldHaveSingleItem();
        entry.Library.ShouldBe(SqlLibrary.NodaTime);
        entry.State.ShouldBe(LibraryState.On);
    }

    // The types of one file's queries are the same for every type that claims it, and the output of the claim picks: a
    // Models claim's model has the base map's, and a CodeGen claim's model and method have the library's.
    [Fact]
    public void ModelsClaim_GetsTheBaseTypes_MethodsGetTheLibrarys()
    {
        const string TwoNamespaces = """
            using SqlSource;

            namespace Methods
            {
                [SqlSourceGenerate(Output = GeneratorOutput.CodeGen)]
                public partial class Repo;
            }

            namespace Dtos
            {
                [SqlSourceGenerate(Output = GeneratorOutput.Models)]
                public partial class Rows;
            }
            """;
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ListEvents\nSELECT at FROM events WHERE at > @since;\n"
        );
        var entry = Entry(
            file,
            "ListEvents",
            [Parameter("since", Timestamptz with { BindName = "timestamptz" })],
            [Column("at", Timestamptz, origin: EventsAt)]
        );

        var run = MethodRuns.Run(file, [entry], TwoNamespaces, WithThePlugin);

        run.ShouldBeClean();
        ModelFiles(run)
            .ShouldBe([
                "Dtos.ListEventsDto-Model.g.cs",
                "Dtos.ListEventsParams-Model.g.cs",
                "Methods.ListEventsDto-Model.g.cs",
                "Methods.ListEventsParams-Model.g.cs",
            ]);
        run.Sources["Dtos.ListEventsDto-Model.g.cs"].ShouldContain("(" + DateTime + " At);");
        run.Sources["Dtos.ListEventsParams-Model.g.cs"].ShouldContain("(" + DateTime + " Since);");
        run.Sources["Methods.ListEventsDto-Model.g.cs"].ShouldContain("(" + Instant + " At);");
        run.Sources["Methods.ListEventsParams-Model.g.cs"].ShouldContain("(" + Instant + " Since);");
        var methods = run.Sources["Methods.Repo-Methods.g.cs"];
        methods.ShouldContain("reader.GetFieldValue<" + Instant + ">(0)");
        methods.ShouldContain("new global::Npgsql.NpgsqlParameter<" + Instant + ">();");
        methods.ShouldContain(" parameter.DataTypeName = \"pg_catalog.timestamptz\";\n");
    }

    // Every row of NodaTime's, as a parameter and a column that can and cannot be null, and an array of one, compile
    // with no error and no warning in what is generated, against the newest Npgsql, plugin and NodaTime and
    // the oldest.
    [Theory]
    [InlineData("10.0.3", "3.3.7", true)]
    [InlineData("8.0.3", "3.1.9", false)]
    public void CodeGen_NodaTime_CompilesWithoutWarnings(string npgsql, string nodaTime, bool newest)
    {
        var tstz = Timestamptz;
        var tsrange = Catalog("tsrange", SidecarValues.Kind.Range, Catalog("timestamp"));
        var daterange = Catalog("daterange", SidecarValues.Kind.Range, Catalog("date"));
        var tstzrange = Catalog("tstzrange", SidecarValues.Kind.Range, tstz);
        static PostgresType Multi(PostgresType range) =>
            TestSidecars.Postgres(
                name: range.Name.Replace("range", "multirange", StringComparison.Ordinal),
                kind: SidecarValues.Kind.Multirange,
                internalName: range.InternalName.Replace("range", "multirange", StringComparison.Ordinal),
                subtype: range
            );

        PostgresType[] types =
        [
            tstz,
            Catalog("timestamp"),
            Catalog("date"),
            Catalog("time"),
            Catalog("timetz"),
            Catalog("interval"),
            tstzrange,
            tsrange,
            daterange,
            Multi(tstzrange),
            Multi(tsrange),
            Multi(daterange),
            ArrayOf(tstz, "_timestamptz"),
        ];
        var names = types
            .Select((_, index) => "v" + index.ToString(System.Globalization.CultureInfo.InvariantCulture))
            .ToArray();
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: Everything\nSELECT 1 WHERE "
                + string.Join(
                    " AND ",
                    names.Select(name => "@" + name + " IS NOT NULL AND @maybe_" + name + " IS NULL")
                )
                + ";\n"
        );
        SidecarParameter[] parameters =
        [
            .. types.Select(
                (type, index) =>
                    Parameter(names[index], type with { BindName = type.InternalName.TrimStart('_') }, false)
            ),
            .. types.Select(
                (type, index) =>
                    Parameter("maybe_" + names[index], type with { BindName = type.InternalName.TrimStart('_') }, true)
            ),
        ];
        SidecarColumn[] columns =
        [
            .. types.Select((type, index) => Column(names[index], type, false)),
            .. types.Select((type, index) => Column("maybe_" + names[index], type, true)),
        ];

        var run = MethodRuns.Run(
            file,
            [Entry(file, "Everything", parameters, columns)],
            references:
            [
                newest ? NpgsqlReferences.Npgsql10 : NpgsqlReferences.Npgsql8,
                LibraryReferences.NodaTime(nodaTime),
                LibraryReferences.NpgsqlNodaTime(npgsql),
            ]
        );

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(Instant);
        methods.ShouldContain("global::NpgsqlTypes.NpgsqlRange<global::NodaTime.LocalDate>[]");
    }

    // A project's converter between Instant and another type leaves the plugin's row to the driver, with no error.
    [Fact]
    public void Run_PluginRowWithAProjectConverterOfAnotherType_HasNoError()
    {
        const string Source = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate]
                public partial class Sample;

                public static class Moments
                {
                    [SqlSourceConverter]
                    public static global::System.DateTimeOffset ToOffset(global::NodaTime.Instant value) =>
                        value.ToDateTimeOffset();

                    [SqlSourceConverter]
                    public static global::NodaTime.Instant FromOffset(global::System.DateTimeOffset value) =>
                        global::NodaTime.Instant.FromDateTimeOffset(value);
                }
            }
            """;
        var file = new SqlFile(GeneratorHarness.UsersPath, ListEvents);

        var run = MethodRuns.Run(file, [EventsEntry(file)], Source, WithThePlugin);

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("reader.GetFieldValue<" + Instant + ">(0)");
    }

    // NodaTime without its plugin on PostgreSQL: an override to Instant goes to the driver, since NodaTime's edges
    // serve SQL Server alone, and no class of conversions is written.
    [Fact]
    public void Run_InstantOverrideWithoutThePlugin_KeepsTheDriversRead()
    {
        var run = RunEvents(
            ListEvents,
            [NpgsqlReferences.Npgsql10, LibraryReferences.NodaTime("3.3.7")],
            Settings("typeOverrides", new JsonObject { ["timestamptz"] = "NodaTime.Instant" })
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("reader.GetFieldValue<" + Instant + ">(0)");
        run.Sources.Keys.ShouldNotContain(LibraryConverters.HintNameOf(SqlLibrary.NodaTime));
    }

    private const string JsonElement = "global::System.Text.Json.JsonElement";

    // JSON in a query, with Npgsql 10 and the runtime's System.Text.Json: json, jsonb and an array of jsonb as columns
    // and parameters, and the types that are not JSON as the same.
    [Theory]
    [InlineData("json", JsonElement, "the System.Text.Json library")]
    [InlineData("jsonb", JsonElement, "the System.Text.Json library")]
    [InlineData("_jsonb", JsonElement + "[]", null)]
    [InlineData("jsonpath", "string", null)]
    [InlineData("xml", "string", null)]
    public void Type_JsonOn_GivesJsonElement(string internalName, string expected, string? place)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- library-types: json=on\n\n-- name: ListDocs\nSELECT doc FROM docs WHERE doc = @doc;\n"
        );
        var type = internalName == "_jsonb" ? ArrayOf(Catalog("jsonb"), "_jsonb") : Catalog(internalName);
        var entry = Entry(file, "ListDocs", [Parameter("doc", type)], [Column("doc", type)]);

        var run = MethodRuns.Run(file, [entry], references: [NpgsqlReferences.Npgsql10]);

        run.ShouldBeClean();
        var query = QueryOf(run);
        var column = query.Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        var parameter = query.Parameters.ShouldHaveSingleItem();
        column.Text.ShouldBe(expected);
        parameter.Text.ShouldBe(expected);
        column.Library.ShouldBe(expected.StartsWith(JsonElement, StringComparison.Ordinal) ? SqlLibrary.Json : null);
        if (place is not null)
        {
            column.Place.ShouldBe(place);
            parameter.Place.ShouldBe(place);
        }
    }

    [Theory]
    [InlineData("json", JsonElement)]
    [InlineData("nvarchar", "string")]
    public void Type_JsonOnSqlServer_GivesJsonElementForJson(string name, string expected)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- library-types: json=on\n\n-- name: ListDocs\nSELECT doc FROM docs WHERE doc = @doc;\n"
        );
        var type = TestSidecars.SqlServer(name, -1);
        var entry = MethodRuns.SqlServerEntry(
            file,
            "ListDocs",
            [TestSidecars.Parameter(0, "doc", type, nullable: true)],
            [TestSidecars.Column(0, "doc", type, nullable: true)]
        );

        var run = MethodRuns.RunSqlServer(file, [entry], references: [SqlClientReferences.SqlClient7]);

        run.ShouldBeClean();
        var query = QueryOf(run);
        query.Columns.ShouldNotBeNull().ShouldHaveSingleItem().Text.ShouldBe(expected);
        query.Parameters.ShouldHaveSingleItem().Text.ShouldBe(expected);
    }

    // No setting: JSON stays a string, though System.Text.Json is referenced.
    [Fact]
    public void Type_JsonUnset_KeepsString()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListDocs\nSELECT doc FROM docs;\n");
        var postgres = MethodRuns.Run(
            file,
            [Entry(file, "ListDocs", [], [Column("doc", Catalog("jsonb"))])],
            references: [NpgsqlReferences.Npgsql10]
        );
        var sqlServerType = TestSidecars.SqlServer("json", -1);
        var sqlServer = MethodRuns.RunSqlServer(
            file,
            [MethodRuns.SqlServerEntry(file, "ListDocs", [], [TestSidecars.Column(0, "doc", sqlServerType)])],
            references: [SqlClientReferences.SqlClient7]
        );

        postgres.ShouldBeClean();
        sqlServer.ShouldBeClean();
        var columns = new[] { QueryOf(postgres).Columns, QueryOf(sqlServer).Columns };
        foreach (var column in columns)
        {
            var only = column.ShouldNotBeNull().ShouldHaveSingleItem();
            only.Text.ShouldBe("string");
            only.Library.ShouldBeNull();
        }
    }

    // Both libraries on in one query: each column takes its own library's type.
    [Fact]
    public void Type_JsonAndNodaTimeOn_GivesBoth()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- library-types: json=on nodatime=on\n\n-- name: ListEvents\nSELECT at, doc FROM events;\n"
        );
        var entry = Entry(
            file,
            "ListEvents",
            [],
            [Column("at", Timestamptz, origin: EventsAt), Column("doc", Catalog("jsonb"))]
        );

        var run = MethodRuns.Run(file, [entry], references: WithThePlugin);

        run.ShouldBeClean();
        var columns = QueryOf(run).Columns.ShouldNotBeNull();
        columns.Count.ShouldBe(2);
        columns[0].Text.ShouldBe(Instant);
        columns[0].Library.ShouldBe(SqlLibrary.NodaTime);
        columns[1].Text.ShouldBe(JsonElement);
        columns[1].Library.ShouldBe(SqlLibrary.Json);
    }

    // ListEvents of the file, or the query of the name, whose one column, at, is a timestamptz read from
    // public.events.at.
    private static SidecarEntry EventsEntry(SqlFile file, string name = "ListEvents") =>
        Entry(file, name, [], [Column("at", Timestamptz, origin: EventsAt)]);

    // The one query of the run's one file.
    private static QueryTypes QueryOf(GeneratorRun run) =>
        StepOutputs.Single<FileTypes>(run, TrackingNames.FileTypes).Queries.ShouldHaveSingleItem();

    // The settings of the library types that say this of NodaTime.
    private static string NodaTimeIs(string state) => Settings("libraryTypes", new JsonObject { ["nodaTime"] = state });

    // The settings of one key, a JSON object.
    private static string Settings(string key, JsonObject value) =>
        GeneratorHarness.Json(new JsonObject { [key] = value });

    // ListEvents of this text at CodeGen, with these references and the project's settings, and the folder's settings
    // file when it is given.
    private static GeneratorRun RunEvents(
        string text,
        MetadataReference[] references,
        string? settings = null,
        string? folderFile = null
    )
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        return MethodRuns.Run(
            file,
            [EventsEntry(file)],
            references: references,
            settings: settings,
            otherFiles: folderFile is null ? null : [GeneratorHarness.SettingsFile(FolderSettingsPath, folderFile)]
        );
    }

    // ListDates of the file, whose one column, d, is a nullable date.
    private static SidecarEntry DatesEntry(SqlFile file) =>
        MethodRuns.SqlServerEntry(
            file,
            "ListDates",
            [],
            [TestSidecars.Column(0, "d", TestSidecars.SqlServer("date", 3), nullable: true)]
        );

    // ListDates of this text at CodeGen under mssql.
    private static GeneratorRun RunDates(string text, MetadataReference[] references)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        return MethodRuns.RunSqlServer(file, [DatesEntry(file)], references: references);
    }

    // ListOrders under mssql, whose one column, placed_at, is a datetime2, with NodaTime on in the project's settings
    // file.
    private static GeneratorRun RunOrders(MetadataReference[] references)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListOrders\nSELECT placed_at FROM orders;\n");
        var entry = MethodRuns.SqlServerEntry(
            file,
            "ListOrders",
            [],
            [TestSidecars.Column(0, "placed_at", TestSidecars.SqlServer("datetime2", 8), nullable: false)]
        );
        return MethodRuns.RunSqlServer(file, [entry], references: references, settings: NodaTimeIs("on"));
    }
}
