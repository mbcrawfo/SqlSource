// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using static SqlSource.Testing.Generator.ModelRuns;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// SQLSRC281: a library that the switch turns on for a codegen query, whose assemblies the project does not reference
// for the query's engine, is reported at the place that says on, once for each place and engine, whatever the number
// of queries, files and types it reaches.  The queries keep their methods, with the base map's types.  Each run is
// under postgres on Npgsql 10 unless it says otherwise.
public sealed class LibraryDiagnosticsTests
{
    private const string ListEvents = "-- name: ListEvents\nSELECT at FROM events;\n";

    private const string OnForPostgres =
        "NodaTime is on for the PostgreSQL query methods it reaches here, but the project does not reference "
        + "Npgsql.NodaTime.  Reference Npgsql.NodaTime, or turn NodaTime off for these queries.";

    private const string OnForSqlServer =
        "NodaTime is on for the SQL Server query methods it reaches here, but the project does not reference "
        + "NodaTime.  Reference NodaTime, or turn NodaTime off for these queries.";

    // The member "nodaTime": "on" of the project's settings file, which the harness writes indented: the fifth line,
    // after the output, the dialect and the key libraryTypes.
    private const string AtTheProjectsMember = "SQLSRC281 /sqlsource.json(5,5)-(5,21): ";

    private static readonly PostgresType Timestamptz = Catalog("timestamptz") with
    {
        Name = "timestamp with time zone",
    };

    [Fact]
    public void On_InSettingsFile_WithoutPlugin_IsReportedAtTheMember()
    {
        var run = RunEvents(ListEvents, [NpgsqlReferences.Npgsql10], NodaTimeIs("on"));

        run.Problems().ShouldBe([AtTheProjectsMember + OnForPostgres]);
    }

    [Fact]
    public void On_InMarker_IsReportedAtTheMarkersLine()
    {
        var run = RunEvents("-- library-types: nodatime=on\n\n" + ListEvents, [NpgsqlReferences.Npgsql10]);

        run.Problems().ShouldBe(["SQLSRC281 /app/Repo/Users.sql(1,1)-(1,30): " + OnForPostgres]);
    }

    // Two types claim two files, each of one query that the project's settings file reaches: one error.
    [Fact]
    public void On_ReachingTwoTypesAndFiles_IsReportedOnce()
    {
        const string TwoTypes = """
            using SqlSource;

            namespace First
            {
                [SqlSourceGenerate]
                public partial class Repo;
            }

            namespace Second
            {
                [SqlSourceGenerate]
                public partial class Repo;
            }
            """;
        var users = new SqlFile(GeneratorHarness.UsersPath, ListEvents);
        var events = new SqlFile("/app/Repo/Events.sql", "-- name: ListOthers\nSELECT at FROM events;\n");

        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, TwoTypes)],
            [
                users,
                Sidecars.Of(users, EventsEntry(users, "ListEvents")),
                events,
                Sidecars.Of(events, EventsEntry(events, "ListOthers")),
            ],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres",
            settings: NodaTimeIs("on")
        );

        run.Problems().ShouldBe([AtTheProjectsMember + OnForPostgres]);
    }

    // One place that reaches a query of each engine is one error for each, naming the assemblies that engine needs.
    [Fact]
    public void On_ReachingBothEngines_IsReportedOncePerEngine()
    {
        var users = new SqlFile(GeneratorHarness.UsersPath, ListEvents);
        var orders = new SqlFile(
            "/app/Repo/Orders.sql",
            "-- dialect: mssql\n-- name: ListOrders\nSELECT placed_at FROM orders;\n"
        );
        var ordersEntry = MethodRuns.SqlServerEntry(
            orders,
            "ListOrders",
            [],
            [TestSidecars.Column(0, "placed_at", TestSidecars.SqlServer("datetime2", 8), nullable: false)]
        );

        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, MethodRuns.Source)],
            [users, Sidecars.Of(users, EventsEntry(users)), orders, Sidecars.Of(orders, ordersEntry)],
            references: [NpgsqlReferences.Npgsql10, SqlClientReferences.SqlClient7],
            dialect: "postgres",
            settings: NodaTimeIs("on")
        );

        run.Problems().ShouldBe([AtTheProjectsMember + OnForPostgres, AtTheProjectsMember + OnForSqlServer]);
    }

    // The switch is for codegen queries: a query at models has the base map's types, whatever it says.
    [Fact]
    public void On_ModelsClaimOnly_IsNotReported()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, ListEvents);

        var run = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file, Sidecars.Of(file, EventsEntry(file))],
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres",
            settings: NodaTimeIs("on")
        );

        run.ShouldBeClean();
    }

    [Fact]
    public void On_WithPlugin_IsNotReported()
    {
        var run = RunEvents(
            ListEvents,
            [
                NpgsqlReferences.Npgsql10,
                LibraryReferences.NodaTime("3.3.7"),
                LibraryReferences.NpgsqlNodaTime("10.0.3"),
            ],
            NodaTimeIs("on")
        );

        run.ShouldBeClean();
    }

    [Fact]
    public void Off_WithoutPlugin_IsNotReported()
    {
        var run = RunEvents(ListEvents, [NpgsqlReferences.Npgsql10], NodaTimeIs("off"));

        run.ShouldBeClean();
    }

    [Fact]
    public void On_QueriesKeepTheirMethods_WithBaseTypes()
    {
        var run = RunEvents(ListEvents, [NpgsqlReferences.Npgsql10], NodaTimeIs("on"));

        run.Problems().ShouldBe([AtTheProjectsMember + OnForPostgres]);
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain("public static ");
        methods.ShouldContain(" ListEvents");
        methods.ShouldContain("reader.GetFieldValue<global::System.DateTime>(0)");
        methods.ShouldNotContain("NodaTime");
        run.Sources["App.ListEventsDto-Model.g.cs"].ShouldContain("(global::System.DateTime At);");
    }

    // The query of the name, whose one column, at, is a timestamptz read from public.events.at.
    private static SidecarEntry EventsEntry(SqlFile file, string name = "ListEvents") =>
        Entry(file, name, [], [Column("at", Timestamptz, origin: new SidecarOrigin("public", "events", "at"))]);

    // The settings of the library types that say this of NodaTime.
    private static string NodaTimeIs(string state) =>
        GeneratorHarness.Json(new JsonObject { ["libraryTypes"] = new JsonObject { ["nodaTime"] = state } });

    // ListEvents of this text at CodeGen, with these references and the project's settings.
    private static GeneratorRun RunEvents(string text, MetadataReference[] references, string? settings = null)
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        return MethodRuns.Run(file, [EventsEntry(file)], references: references, settings: settings);
    }
}
