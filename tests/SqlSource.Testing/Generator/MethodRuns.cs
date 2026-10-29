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
    // references are given.  The output and the dialect are the project's settings file, with the entries of settings,
    // a JSON object, over them, and the other files are the run's beside the file and its sidecar.
    public static GeneratorRun Run(
        SqlFile file,
        SidecarEntry[] entries,
        string source = Source,
        MetadataReference[]? references = null,
        string dialect = "postgres",
        LanguageVersion languageVersion = LanguageVersion.CSharp12,
        string? settings = null,
        SqlFile[]? otherFiles = null
    ) =>
        GeneratorHarness.Run(
            OutputKind.CodeGen,
            [new SourceFile(GeneratorHarness.SourcePath, source)],
            [file, Sidecars.Of(file, entries), .. otherFiles ?? []],
            languageVersion: languageVersion,
            references: references ?? [NpgsqlReferences.Npgsql10],
            dialect: dialect,
            settings: settings
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

    // The first Run under mssql, on SqlClient 7 unless the references are given.
    public static GeneratorRun RunSqlServer(
        SqlFile file,
        SidecarEntry[] entries,
        string source = Source,
        MetadataReference[]? references = null,
        string? settings = null
    ) => Run(file, entries, source, references ?? [SqlClientReferences.SqlClient7], "mssql", settings: settings);

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

    // Three queries whose token has the name, beside a parameter that can be null and an array, each bound through a
    // converter: Probe reads many rows, ProbeOne one and ProbeStream streams its rows, each into a model of a column
    // read through a converter and an array read through it element by element, so that the token sits beside every
    // local and every lambda's parameter a method can have.
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
            "-- type-override: int4=App.UserId\n-- name: Probe\n"
                + body
                + "-- name: ProbeOne -> one\n"
                + body
                + "-- name: ProbeStream\n-- collection-type: IAsyncEnumerable\n"
                + body
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
                ModelRuns.Entry(file, "ProbeStream", parameters, columns),
            ],
            TokenSource,
            languageVersion: languageVersion
        );
    }

    // A streaming query under mssql, on SqlClient 7, whose token has the name, beside a parameter: SQL Server's
    // iterator has locals of its own, the flag of a row being yielded among them, which RunTokenNamed's run has not.
    public static GeneratorRun RunSqlServerTokenNamed(string token, LanguageVersion languageVersion)
    {
        var file = new SqlFile(
            GeneratorHarness.UsersPath,
            "-- name: ProbeStream\n-- collection-type: IAsyncEnumerable\n-- token: {{"
                + token
                + ":users}}\nSELECT id FROM {{"
                + token
                + "}} WHERE id = @id;\n"
        );
        var entry = SqlServerEntry(
            file,
            "ProbeStream",
            [TestSidecars.Parameter(0, "id", TestSidecars.SqlServer())],
            [TestSidecars.Column(0, "id", TestSidecars.SqlServer())]
        );
        return Run(
            file,
            [entry],
            references: [SqlClientReferences.SqlClient7],
            dialect: "mssql",
            languageVersion: languageVersion
        );
    }

    // The source of the runs of ParameterKinds: App.Sample, and App.UserId with its converters of long.
    public const string KindsSource = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;

            public readonly record struct UserId(long Value)
            {
                [SqlSourceConverter]
                public static UserId FromDatabase(long value) => new(value);

                [SqlSourceConverter]
                public static long ToDatabase(UserId id) => id.Value;
            }
        }
        """;

    // The marker that converts the parameter "converted" of ParameterKinds, an int8, through App.UserId.
    public const string KindsOverride = "-- type-override: int8=App.UserId\n";

    // A parameter's type of each kind that PostgreSQL binds, by the name of its kind, with the bindName the tool writes
    // for it: a base type, an array, an enum, a domain, a range, a multirange, and an int8 converted under
    // KindsOverride.
    public static readonly (string Kind, PostgresType Type)[] ParameterKinds =
    [
        ("base", ModelRuns.Int4 with { BindName = "integer" }),
        ("array", ModelRuns.ArrayOf(ModelRuns.Int4, "_int4") with { BindName = "integer[]" }),
        (
            "enum",
            TestSidecars.Postgres(
                name: "mood",
                kind: SidecarValues.Kind.Enum,
                schema: "public",
                internalName: "mood",
                labels: ["happy", "sad"],
                bindName: "public.mood"
            )
        ),
        (
            "domain",
            TestSidecars.Postgres(
                name: "email",
                kind: SidecarValues.Kind.Domain,
                schema: "public",
                internalName: "email",
                baseType: ModelRuns.Text,
                bindName: "public.email"
            )
        ),
        ("range", Int4Range with { BindName = "int4range" }),
        (
            "multirange",
            TestSidecars.Postgres(
                name: "int4multirange",
                kind: SidecarValues.Kind.Multirange,
                internalName: "int4multirange",
                subtype: Int4Range,
                bindName: "int4multirange"
            )
        ),
        ("converted", ModelRuns.Catalog("int8") with { Name = "bigint", BindName = "bigint" }),
    ];

    // The type int4range, as a multirange nests it, without a bindName.
    private static PostgresType Int4Range =>
        TestSidecars.Postgres(
            name: "int4range",
            kind: SidecarValues.Kind.Range,
            internalName: "int4range",
            subtype: ModelRuns.Int4
        );

    // The source of App.Sample with the attribute's arguments, such as MethodLocation = MethodLocation.Public.
    public static string SampleSource(string arguments) =>
        "using SqlSource;\n\nnamespace App\n{\n    [SqlSourceGenerate("
        + arguments
        + ")]\n    public partial class Sample;\n}\n";
}
