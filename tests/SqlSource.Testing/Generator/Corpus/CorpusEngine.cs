using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Testing.Generator.Corpus;

// One engine of the golden corpus of query methods: what a scenario needs to write its queries' entries and run them
// for this engine, and the scenarios only it has.  A shared scenario is written once against these, so that a new
// engine is a new row.
//   Name: the engine's folder of golden files and the trait of its cases.
//   Dialect: the dialect of the project's settings file.
//   Driver: the references of a run that gives none.
//   Int, Text: an integer and a string of the engine, as a sidecar describes them.
//   IntOverrideKey, TextOverrideKey: the names of Int and Text in a `-- type-override:` marker.
// The members after the record's own give the engine's own scenarios, and what a shared scenario needs of the engine
// beyond these.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal sealed record CorpusEngine(
    string Name,
    string Dialect,
    MetadataReference[] Driver,
    SidecarType Int,
    SidecarType Text,
    string IntOverrideKey,
    string TextOverrideKey
)
{
    // jsonb, the JSON document of PostgreSQL, bound by its name.
    private static readonly PostgresType PostgresJsonb = ModelRuns.Catalog("jsonb") with { BindName = "jsonb" };

    public static readonly CorpusEngine Postgres = new(
        "postgres",
        "postgres",
        [NpgsqlReferences.Npgsql10],
        ModelRuns.Int4,
        ModelRuns.Text,
        "int4",
        "text"
    )
    {
        EntryOf = (file, name, parameters, columns) => ModelRuns.Entry(file, name, parameters, columns),
        NodaTimeReferences =
        [
            NpgsqlReferences.Npgsql10,
            LibraryReferences.NodaTime("3.3.7"),
            LibraryReferences.NpgsqlNodaTime("10.0.3"),
        ],
        NodaTimeTypes =
        [
            ("at", ModelRuns.Catalog("timestamptz") with { Name = "timestamp with time zone" }),
            ("day", ModelRuns.Catalog("date")),
        ],
        Json = PostgresJsonb,
        BoundInt = ModelRuns.Int4 with { BindName = "integer" },
        BoundText = ModelRuns.Text with { BindName = "text" },
        WholeDecimal = ModelRuns.Catalog("numeric") with { BindName = "numeric" },
        UnboundedText = ModelRuns.Catalog("text") with { BindName = "text" },
        JsonArray = ModelRuns.ArrayOf(PostgresJsonb, "_jsonb") with { BindName = "jsonb[]" },
        OwnScenarios =
        [
            new("driver-npgsql8", engine => QueryMethodCorpus.Driver(engine, NpgsqlReferences.Npgsql8)),
            new("driver-npgsql10", engine => QueryMethodCorpus.Driver(engine, NpgsqlReferences.Npgsql10)),
            new("binding", QueryMethodCorpus.PostgresBinding),
            new("by-element", QueryMethodCorpus.ByElement),
        ],
    };

    public static readonly CorpusEngine SqlServer = new(
        "sqlserver",
        "mssql",
        [SqlClientReferences.SqlClient7],
        TestSidecars.SqlServer(),
        TestSidecars.SqlServer("nvarchar(100)", 200, 0),
        "int",
        "nvarchar"
    )
    {
        EntryOf = MethodRuns.SqlServerEntry,
        NodaTimeReferences = [SqlClientReferences.SqlClient7, LibraryReferences.NodaTime("3.3.7")],
        NodaTimeSettings = /*lang=json,strict*/
            """{"libraryTypes": {"nodaTime": "on"}}""",
        NodaTimeTypes =
        [
            ("day", TestSidecars.SqlServer("date", 3, 10)),
            ("stamp", TestSidecars.SqlServer("datetime2", 8, 27, 7)),
            ("clock", TestSidecars.SqlServer("time", 5, 16, 7)),
            ("moment", TestSidecars.SqlServer("datetimeoffset", 10, 34, 7)),
        ],
        Json = TestSidecars.SqlServer("json", -1, 0),
        BoundInt = TestSidecars.SqlServer(),
        BoundText = TestSidecars.SqlServer("nvarchar(100)", 200, 0),
        WholeDecimal = TestSidecars.SqlServer("decimal(10,0)", 9, 10, 0),
        UnboundedText = TestSidecars.SqlServer("nvarchar(max)", -1, 0),
        OwnScenarios =
        [
            new("binding", QueryMethodCorpus.SqlServerBinding),
            new("vector", QueryMethodCorpus.Vector),
            new("spatial", QueryMethodCorpus.Spatial),
        ],
    };

    public static IReadOnlyList<CorpusEngine> All { get; } = [Postgres, SqlServer];

    // The scenarios that only this engine runs, after the shared ones.
    public IReadOnlyList<CorpusScenario> OwnScenarios { get; init; } = [];

    // The references of nodatime: the driver and NodaTime, with the plugin that teaches the driver NodaTime's types
    // when the engine has one.
    public MetadataReference[] NodaTimeReferences { get; init; } = [];

    // The settings of nodatime, a JSON object over the project's settings file: what turns NodaTime's types on, when
    // the engine's driver does not read them itself.
    public string? NodaTimeSettings { get; init; }

    // The types of the engine that NodaTime's types read and write, each with the name of its columns and parameters.
    public IReadOnlyList<(string Name, SidecarType Type)> NodaTimeTypes { get; init; } = [];

    // A JSON document of the engine, which a class is read from and written to whole.
    public SidecarType Json { get; init; } = null!;

    // Int and Text as the tool describes a parameter of them, with the name a PostgreSQL binding sets as its
    // DataTypeName.
    public SidecarType BoundInt { get; init; } = null!;

    public SidecarType BoundText { get; init; } = null!;

    // A decimal of the engine as the tool describes a parameter of it, of scale 0 where the engine gives one, which a
    // SQL Server binding rounds to a whole number.
    public SidecarType WholeDecimal { get; init; } = null!;

    // A string of the engine with no limit on its length, which a column marker with `json` reads as JSON.
    public SidecarType UnboundedText { get; init; } = null!;

    // An array of Json, whose elements a class is read from and written to one by one, or null when the engine has
    // no arrays.
    public SidecarType? JsonArray { get; init; }

    // The engine's entry of a query for its current hash: with rows when it has columns, and without when they are
    // null.  The parameters and columns are numbered by their place.
    private Func<SqlFile, string, SidecarParameter[], SidecarColumn[]?, SidecarEntry> EntryOf { get; init; } = null!;

    public SidecarEntry Entry(SqlFile file, string name, SidecarParameter[] parameters, SidecarColumn[]? columns) =>
        EntryOf(file, name, parameters, columns);

    // A parameter of the name and type, with its nullability unsaid unless given.  It and Column are static, since
    // both are the same for every engine: a scenario passes the engine's Int or Text.
    public static SidecarParameter Parameter(string name, SidecarType type, bool? nullable = null) =>
        TestSidecars.Parameter(0, name, type, nullable);

    // A column of the name and type, not null unless given.
    public static SidecarColumn Column(string name, SidecarType type, bool nullable = false) =>
        TestSidecars.Column(0, name, type, nullable);

    // The sources and files at Methods, unless another output is given, under the engine's dialect, at C# 12, on the
    // engine's driver unless the references are given.  The settings, a JSON object, go over the project's settings
    // file, and the properties are what MSBuild tells the generator.
    public GeneratorRun Run(
        SourceFile[] sources,
        SqlFile[] files,
        MetadataReference[]? references = null,
        string? settings = null,
        IReadOnlyDictionary<string, string?>? properties = null,
        OutputKind output = OutputKind.Methods
    ) =>
        GeneratorHarness.Run(
            output,
            sources,
            files,
            languageVersion: LanguageVersion.CSharp12,
            references: references ?? Driver,
            dialect: Dialect,
            properties: properties,
            settings: settings
        );
}

// A scenario of the corpus: its name, the second part of a case's name, and the run it makes for an engine.
internal sealed record CorpusScenario(string Name, Func<CorpusEngine, GeneratorRun> Run);
