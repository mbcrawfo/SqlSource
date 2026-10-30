// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
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

    // What NodaTime's row gives it, with the base map's type beside it.
    private static readonly PropertyType InstantOfTheLibrary = new(
        Instant,
        true,
        true,
        EquatableArray<string>.Empty,
        "timestamptz",
        null,
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
            references: [LibraryReferences.Synthetic("3.3.0.0", instant: true, pluginExtensions: true)]
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

    // SQL Server has no rows of NodaTime yet, so the library being on changes no type.
    [Fact]
    public void SqlServer_NodaTimeOn_KeepsTheBaseTypes()
    {
        var run = RunOrders([SqlClientReferences.SqlClient7, LibraryReferences.NodaTime("3.3.7")]);

        run.ShouldBeClean();
        var query = QueryOf(run);
        var column = query.Columns.ShouldNotBeNull().ShouldHaveSingleItem();
        column.Text.ShouldBe(DateTime);
        column.Library.ShouldBeNull();
        column.WithoutLibrary.ShouldBeNull();
        query.UnavailableLibraries.ShouldBeEmpty();
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
