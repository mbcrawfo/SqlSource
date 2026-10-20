using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using SqlSource.Generation;
using SqlSource.Generation.Models;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Testing.Generator;

// The harness of the model tests of tests/SqlSource.Tests/Generator/: a run at Models under postgres on Npgsql 10, the
// entries written for a query's current hash, and the models the generator gave.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class ModelRuns
{
    // The source most model tests run on: one type that claims the queries.
    public const string Source = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;
        }
        """;

    // A model's file ends in -Model.g.cs, with a hash before .g.cs when its name is made unique.
    private const string ModelMark = "-Model.";

    public static readonly PostgresType Int4 = TestSidecars.Postgres(name: "integer", internalName: "int4");

    public static readonly PostgresType Text = TestSidecars.Postgres(name: "text", internalName: "text");

    // The source and files at Models under postgres, on Npgsql 10 unless the references are given.
    public static GeneratorRun Run(
        string source,
        SqlFile[] files,
        IReadOnlyDictionary<string, string?>? properties = null,
        MetadataReference[]? references = null
    ) =>
        GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            files,
            references: references ?? [NpgsqlReferences.Npgsql10],
            dialect: "postgres",
            properties: properties
        );

    // The file with its sidecar of these entries, run on Source.
    public static GeneratorRun Run(SqlFile file, params SidecarEntry[] entries) => Run(file, entries, null);

    public static GeneratorRun Run(
        SqlFile file,
        SidecarEntry[] entries,
        IReadOnlyDictionary<string, string?>? properties
    ) => Run(Source, [file, Sidecars.Of(file, entries)], properties);

    // An entry of the query for its current hash: with rows when it has columns, and without when they are null.  The
    // parameters and columns are numbered by their place.
    public static SidecarEntry Entry(
        SqlFile file,
        string name,
        SidecarParameter[] parameters,
        SidecarColumn[]? columns,
        SidecarTable? matchesTable = null
    ) =>
        TestSidecars.Entry(
            name,
            Sidecars.HashOf(file, name, SqlDialect.PostgreSql),
            "postgres",
            "main",
            "18.0",
            columns is null ? SidecarResultKind.None : SidecarResultKind.Rows,
            matchesTable: matchesTable,
            parameters: [.. parameters.Select((parameter, ordinal) => parameter with { Ordinal = ordinal })],
            columns: columns?.Select((column, ordinal) => column with { Ordinal = ordinal }).ToArray()
        );

    public static SidecarParameter Parameter(string name, PostgresType? type = null, bool? nullable = null) =>
        TestSidecars.Parameter(0, name, type ?? Int4, nullable);

    public static SidecarColumn Column(
        string name,
        PostgresType? type = null,
        bool? nullable = false,
        SidecarOrigin? origin = null,
        bool? identity = null,
        bool? computed = null
    ) => TestSidecars.Column(0, name, type ?? Int4, nullable, origin: origin, identity: identity, computed: computed);

    // A type of pg_catalog, by its internal name, which is also its name.
    public static PostgresType Catalog(string internalName) =>
        TestSidecars.Postgres(name: internalName, internalName: internalName);

    // The files of the models the generator added, by name.
    public static string[] ModelFiles(GeneratorRun run) =>
        [
            .. run
                .Sources.Keys.Where(name => name.Contains(ModelMark, StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

    // What the step of the models gave for the one file of the run.
    public static FileModels ModelsOf(GeneratorRun run) =>
        StepOutputs.Single<FileModels>(run, TrackingNames.FileModels);
}
