using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using SqlSource.Snapshot;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Testing.Generator;

// The harness of the repository tests of tests/SqlSource.Tests/Generator/: a run at Repository, under postgres on
// Npgsql 10, with the entries written for a query's current hash, as MethodRuns runs at Methods.
//
// Like the other files of this folder, it stays within Roslyn 4.14.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class RepositoryRuns
{
    // The source most repository tests run on: one public type that claims the queries, whose primary constructor
    // takes the data source that gives its methods their connection.
    public const string Source = """
        using Npgsql;
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate(Source = ".")]
            public partial class Sample(NpgsqlDataSource dataSource);
        }
        """;

    // The source and the file with its sidecar of these entries, at Repository under postgres, on Npgsql 10 unless the
    // references are given.  The output and the dialect are the project's settings file, with the entries of settings,
    // a JSON object, over them.  The properties are what MSBuild tells the generator.
    public static GeneratorRun Run(
        SqlFile file,
        SidecarEntry[] entries,
        string source = Source,
        MetadataReference[]? references = null,
        string dialect = "postgres",
        string? settings = null,
        IReadOnlyDictionary<string, string?>? properties = null
    ) =>
        GeneratorHarness.Run(
            OutputKind.Repository,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [file, Sidecars.Of(file, entries)],
            references: references ?? [NpgsqlReferences.Npgsql10],
            dialect: dialect,
            properties: properties,
            settings: settings
        );
}
