using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Testing.Generator;

// One engine of the golden corpus of query methods: what a scenario needs to write its queries' entries and run them
// for this engine, and the scenarios only it has.  A shared scenario is written once against these, so that a new
// engine is a new row.
//   Name: the engine's folder of golden files and the trait of its cases.
//   Dialect: the dialect of the project's settings file.
//   Driver: the references of a run that gives none.
//   Int, Text: an integer and a string of the engine, as a sidecar describes them.
//   IntOverrideKey, TextOverrideKey: the names of Int and Text in a `-- type-override:` marker.
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
    };

    public static IReadOnlyList<CorpusEngine> All { get; } = [Postgres, SqlServer];

    // The scenarios that only this engine runs, after the shared ones.
    public IReadOnlyList<CorpusScenario> OwnScenarios { get; init; } = [];

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

    // The sources and files at CodeGen under the engine's dialect, at C# 12, on the engine's driver unless the
    // references are given.  The settings, a JSON object, go over the project's settings file, and the properties are
    // what MSBuild tells the generator.
    public GeneratorRun Run(
        SourceFile[] sources,
        SqlFile[] files,
        MetadataReference[]? references = null,
        string? settings = null,
        IReadOnlyDictionary<string, string?>? properties = null
    ) =>
        GeneratorHarness.Run(
            OutputKind.CodeGen,
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
