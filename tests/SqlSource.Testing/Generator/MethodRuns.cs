using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Testing.Generator;

// The harness of the method tests of tests/SqlSource.Tests/Generator/: a run at CodeGen, under postgres on Npgsql 10 or
// under mssql on SqlClient 7, the entries written for a query's current hash, and the methods the generator gave.
//
// Like the other files of this folder, it stays within Roslyn 4.8.0, since tests/SqlSource.Tests.RoslynFloor
// references this library.
internal static class MethodRuns
{
    // The source most method tests run on: one public type that claims the queries, whose methods go into the
    // extension class SampleExtensions.
    public const string Source = ModelRuns.Source;

    // The name of the file of Source's methods.
    public const string MethodsFile = "App.Sample-Methods.g.cs";

    // The source and the file with its sidecar of these entries, at CodeGen under postgres, on Npgsql 10 unless the
    // references are given.
    public static GeneratorRun Run(
        SqlFile file,
        SidecarEntry[] entries,
        string source = Source,
        MetadataReference[]? references = null,
        string dialect = "postgres",
        IReadOnlyDictionary<string, string?>? properties = null,
        LanguageVersion languageVersion = LanguageVersion.CSharp12
    ) =>
        GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [file, Sidecars.Of(file, entries)],
            languageVersion: languageVersion,
            references: references ?? [NpgsqlReferences.Npgsql10],
            dialect: dialect,
            properties: properties
        );

    // The source and these files, each sidecar among them, at CodeGen under postgres on Npgsql 10: the run of a test
    // with more than one file.
    public static GeneratorRun Run(string source, params SqlFile[] files) =>
        GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            files,
            references: [NpgsqlReferences.Npgsql10],
            dialect: "postgres"
        );

    // The same under mssql, on SqlClient 7 unless the references are given.
    public static GeneratorRun RunSqlServer(
        SqlFile file,
        SidecarEntry[] entries,
        string source = Source,
        MetadataReference[]? references = null,
        IReadOnlyDictionary<string, string?>? properties = null
    ) => Run(file, entries, source, references ?? [SqlClientReferences.SqlClient7], "mssql", properties);

    // An entry of an mssql query for its current hash: with rows when it has columns, and without when they are null.
    // The parameters and columns are numbered by their place.
    public static SidecarEntry SqlServerEntry(
        SqlFile file,
        string name,
        SidecarParameter[] parameters,
        SidecarColumn[]? columns
    ) =>
        TestSidecars.Entry(
            name,
            Sidecars.HashOf(file, name, SqlDialect.SqlServer),
            SidecarValues.Engine.SqlServer,
            "main",
            "17.0",
            columns is null ? SidecarResultKind.None : SidecarResultKind.Rows,
            parameters: [.. parameters.Select((parameter, ordinal) => parameter with { Ordinal = ordinal })],
            columns: columns?.Select((column, ordinal) => column with { Ordinal = ordinal }).ToArray()
        );

    // The source of RunTokenNamed: App.Sample, and App.UserId with its converters, which int4 is read and bound as.
    private const string TokenSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;

            public readonly record struct UserId(int Value)
            {
                [SqlSourceConverter]
                public static UserId FromDatabase(int value) => new(value);

                [SqlSourceConverter]
                public static int ToDatabase(UserId id) => id.Value;
            }
        }
        """;

    // Two queries whose token has the name, beside a parameter that can be null and an array, each bound through a
    // converter: Probe reads many rows and ProbeOne one, each into a model of a column read through a converter and
    // an array read through it element by element, so that the token sits beside every local and every lambda's
    // parameter a method can have.
    public static GeneratorRun RunTokenNamed(string token, LanguageVersion languageVersion)
    {
        var body =
            "-- token: {{"
            + token
            + ":users}}\nSELECT id, ids FROM {{"
            + token
            + "}} WHERE id = @id OR id = ANY(@ids);\n";
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- type-override: int4=App.UserId\n-- name: Probe\n" + body + "-- name: ProbeOne -> one\n" + body
        );
        var ids = ModelRuns.ArrayOf(ModelRuns.Int4, "_int4");
        SidecarParameter[] parameters =
        [
            ModelRuns.Parameter("id", nullable: true),
            ModelRuns.Parameter("ids", ids, nullable: false),
        ];
        SidecarColumn[] columns = [ModelRuns.Column("id"), ModelRuns.Column("ids", ids)];
        return Run(
            file,
            [
                ModelRuns.Entry(file, "Probe", parameters, columns),
                ModelRuns.Entry(file, "ProbeOne", parameters, columns),
            ],
            TokenSource,
            languageVersion: languageVersion
        );
    }

    // The source of App.Sample with the attribute's arguments, such as MethodLocation = MethodLocation.Public.
    public static string SampleSource(string arguments) =>
        "using SqlSource;\n\nnamespace App\n{\n    [SqlSourceGenerate("
        + arguments
        + ")]\n    public partial class Sample;\n}\n";
}
