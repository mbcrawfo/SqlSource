// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
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
//
// SQLSRC282: a library older than its floor that a codegen query uses is reported once for the project, with no
// position, and the queries that use it get no method, while their models keep the library's types.
public sealed class LibraryDiagnosticsTests
{
    private const string BelowTheFloor =
        "SQLSRC282 (1,1)-(1,1): This project references NodaTime 3.1.8, and SqlSource needs NodaTime 3.1.9 or later "
        + "for the query methods that use it, so they are not generated.  Update the package.";

    private const string FindDates = "-- name: FindDates\nSELECT d FROM dates WHERE d = @d;\n";

    private const string LocalDate = "global::NodaTime.LocalDate";

    // The file of the class of NodaTime's conversions, as the tests name it.
    private const string ConversionsFile = "SqlSource-NodaTimeConversions.g.cs";

    private static readonly SqlServerType Date = TestSidecars.SqlServer("date", 3, 10);

    // SqlClient 7 and an assembly of NodaTime 3.1.8, below its floor, with the types its rows name.
    private static MetadataReference[] BelowTheFloorOnSqlServer =>
        [
            SqlClientReferences.SqlClient7,
            LibraryReferences.Synthetic("3.1.8.0", nodaTimeTypes: true, pluginExtensions: false),
        ];

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

    // Two types claim two files, each of one SQL Server query that reads and finds by a date, which NodaTime, on for
    // the project and below its floor, makes a LocalDate: one SQLSRC282, no method, and models of LocalDate.
    [Fact]
    public void Run_NodaTimeBelowTheFloorAndACodeGenUse_Reports282Once()
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
        var users = new SqlFile(GeneratorHarness.UsersPath, FindDates);
        var events = new SqlFile("/app/Repo/Events.sql", "-- name: FindEvents\nSELECT d FROM events WHERE d = @d;\n");

        var run = GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, TwoTypes)],
            [
                users,
                Sidecars.Of(users, DatesEntry(users, "FindDates")),
                events,
                Sidecars.Of(events, DatesEntry(events, "FindEvents")),
            ],
            references: BelowTheFloorOnSqlServer,
            dialect: "mssql",
            settings: NodaTimeIs("on")
        );

        run.Problems().ShouldBe([BelowTheFloor]);
        MethodSources(run).ShouldBeEmpty();
        ModelSources(run).ShouldNotBeEmpty();
        ModelSources(run).ShouldAllBe(model => model.Contains(LocalDate));
        run.Sources.Keys.ShouldNotContain(ConversionsFile);
    }

    // The library's rows are for codegen queries: at models a query has the base map's types, and uses nothing.
    [Fact]
    public void Run_NodaTimeBelowTheFloorAndAModelsUseOnly_ReportsNothing()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, FindDates);

        var run = GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            [file, Sidecars.Of(file, DatesEntry(file, "FindDates"))],
            references: BelowTheFloorOnSqlServer,
            dialect: "mssql",
            settings: NodaTimeIs("on")
        );

        run.ShouldBeClean();
    }

    // Of two queries, one reads a date, which NodaTime makes a LocalDate, and the other an int: the first gets no
    // method, and the second keeps its own.
    [Fact]
    public void Run_NodaTimeBelowTheFloorAndAQueryThatDoesNotUseIt_KeepsItsMethod()
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            FindDates + "\n-- name: CountDates\nSELECT n FROM counts;\n"
        );

        var run = MethodRuns.RunSqlServer(
            file,
            [
                DatesEntry(file, "FindDates"),
                MethodRuns.SqlServerEntry(
                    file,
                    "CountDates",
                    [],
                    [TestSidecars.Column(0, "n", TestSidecars.SqlServer())]
                ),
            ],
            references: BelowTheFloorOnSqlServer,
            settings: NodaTimeIs("on")
        );

        run.Problems().ShouldBe([BelowTheFloor]);
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(" CountDates(");
        methods.ShouldNotContain("FindDates");
        methods.ShouldNotContain("NodaTime");
        run.Sources["App.FindDatesDto-Model.g.cs"].ShouldContain("(" + LocalDate + " D);");
    }

    // A query that uses NodaTime below its floor and has a problem of its own, the shape one on a statement without
    // rows, reports both, so that updating NodaTime leaves nothing new to fix, and has no method.
    [Fact]
    public void Run_NodaTimeBelowTheFloorAndAShapeProblem_ReportsBoth()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: SetDate -> one\nUPDATE dates SET d = @d;\n");

        var run = MethodRuns.RunSqlServer(
            file,
            [MethodRuns.SqlServerEntry(file, "SetDate", [TestSidecars.Parameter(0, "d", Date, false)], null)],
            references: BelowTheFloorOnSqlServer,
            settings: NodaTimeIs("on")
        );

        run.Problems()
            .ShouldBe(
                [
                    BelowTheFloor,
                    "SQLSRC272 /app/Repo/Users.sql(1,10)-(1,17): The query 'SetDate' has the shape 'one', and its "
                        + "statement returns no rows.  Remove the shape, or write 'rowcount' or 'none'.",
                ],
                ignoreOrder: true
            );
        MethodSources(run).ShouldBeEmpty();
        run.Sources["App.SetDateParams-Model.g.cs"].ShouldContain("(" + LocalDate + " D);");
    }

    // On PostgreSQL, the plugin's row of a timestamptz is NodaTime's Instant, and NodaTime below its floor reports
    // SQLSRC282 there too.
    [Fact]
    public void Run_PostgresPluginRowBelowTheFloor_Reports282()
    {
        var run = RunEvents(
            ListEvents,
            [
                NpgsqlReferences.Npgsql10,
                LibraryReferences.Synthetic("3.1.8.0", nodaTimeTypes: true, pluginExtensions: true),
            ]
        );

        run.Problems().ShouldBe([BelowTheFloor]);
        MethodSources(run).ShouldBeEmpty();
        run.Sources["App.ListEventsDto-Model.g.cs"].ShouldContain("(global::NodaTime.Instant At);");
    }

    // The same project on NodaTime 3.1.9, the floor, reports nothing and has its methods.
    [Fact]
    public void Run_NodaTimeAtTheFloor_ReportsNothing()
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, FindDates);

        var run = MethodRuns.RunSqlServer(
            file,
            [DatesEntry(file, "FindDates")],
            references: [SqlClientReferences.SqlClient7, LibraryReferences.NodaTime("3.1.9")],
            settings: NodaTimeIs("on")
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain(" FindDates(");
    }

    // The query of the name, whose one column, at, is a timestamptz read from public.events.at.
    private static SidecarEntry EventsEntry(SqlFile file, string name = "ListEvents") =>
        Entry(file, name, [], [Column("at", Timestamptz, origin: new SidecarOrigin("public", "events", "at"))]);

    // The query of the name under mssql, whose column d is a date, and which finds by a date d.
    private static SidecarEntry DatesEntry(SqlFile file, string name) =>
        MethodRuns.SqlServerEntry(
            file,
            name,
            [TestSidecars.Parameter(0, "d", Date, false)],
            [TestSidecars.Column(0, "d", Date)]
        );

    // The files of methods that a run generated.
    private static string[] MethodSources(GeneratorRun run) =>
        [
            .. run
                .Sources.Where(source => source.Key.EndsWith("-Methods.g.cs", StringComparison.Ordinal))
                .Select(source => source.Value),
        ];

    // The files of models that a run generated.
    private static string[] ModelSources(GeneratorRun run) =>
        [
            .. run
                .Sources.Where(source => source.Key.EndsWith("-Model.g.cs", StringComparison.Ordinal))
                .Select(source => source.Value),
        ];

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
