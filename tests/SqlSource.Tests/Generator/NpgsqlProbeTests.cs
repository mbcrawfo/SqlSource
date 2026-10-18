// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Testing.Generator;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The probe of the Npgsql a project references, against the drivers themselves and against assemblies made to look
// like one, and the diagnostics of the driver.  Each run is of a query at Models in a postgres file whose entry is
// current and has no columns.
public sealed class NpgsqlProbeTests
{
    private const string Source = """
        using SqlSource;

        namespace App;

        [SqlSourceGenerate]
        public partial class Sample;
        """;

    private static readonly SqlFile Users = new("/app/Repo/Users.sql", "-- name: GetUser\nSELECT 1;\n");

    private static readonly SqlFile Orders = new("/app/Repo/Orders.sql", "-- name: GetOrder\nSELECT 2;\n");

    private static readonly SqlFile[] UsersWithSidecar = [Users, CurrentSidecar(Users, "GetUser")];

    private static readonly SqlFile[] BothWithSidecars =
    [
        .. UsersWithSidecar,
        Orders,
        CurrentSidecar(Orders, "GetOrder"),
    ];

    [Theory]
    [InlineData("none")]
    [InlineData("8")]
    [InlineData("9")]
    [InlineData("10")]
    public void Read_EachDriver_GivesItsFlags(string driver)
    {
        var (reference, expected, column) = driver switch
        {
            "8" => (
                NpgsqlReferences.Npgsql8,
                new NpgsqlProbe(true, true, false, new Version(8, 0, 3, 0)),
                DriverColumn.Npgsql8
            ),
            "9" => (
                NpgsqlReferences.Npgsql9,
                new NpgsqlProbe(true, true, false, new Version(9, 0, 3, 0)),
                DriverColumn.Npgsql8
            ),
            "10" => (
                NpgsqlReferences.Npgsql10,
                new NpgsqlProbe(true, true, true, new Version(10, 0, 3, 0)),
                DriverColumn.Npgsql10
            ),
            "none" => (null, new NpgsqlProbe(false, false, false, null), DriverColumn.None),
            _ => throw new ArgumentOutOfRangeException(nameof(driver), driver, "No such driver."),
        };

        var run = Run(OutputKind.Models, reference is null ? [] : [reference], UsersWithSidecar);

        run.Diagnostics.ShouldBeEmpty();
        var probe = ProbeOf(run);
        probe.ShouldBe(expected);
        probe.Column.ShouldBe(column);
    }

    [Fact]
    public void Run_NpgsqlBelowTheFloor_IsSqlsrc247Once()
    {
        const string Expected =
            "SQLSRC247 (1,1)-(1,1): This project references Npgsql 7.0.0, and SqlSource needs Npgsql 8.0 or later.  "
            + "Update the package.";
        var driver = NpgsqlReferences.Synthetic("7.0.0", slimBuilder: false, cube: false);

        var oneFile = Run(OutputKind.Models, [driver], UsersWithSidecar);
        var twoFiles = Run(OutputKind.Models, [driver], BothWithSidecars);

        oneFile.Diagnostics.ShouldBe([Expected]);
        SeverityOf(oneFile, "SQLSRC247").ShouldBe(DiagnosticSeverity.Error);
        ProbeOf(oneFile).Column.ShouldBe(DriverColumn.Npgsql8);
        twoFiles.Diagnostics.ShouldBe([Expected]);
    }

    [Fact]
    public void Run_NpgsqlNewerThanTheGeneratorKnows_IsSqlsrc248()
    {
        var driver = NpgsqlReferences.Synthetic("11.0.0", slimBuilder: true, cube: true);

        var run = Run(OutputKind.Models, [driver], BothWithSidecars);

        run.Diagnostics.ShouldBe([
            "SQLSRC248 (1,1)-(1,1): This project references Npgsql 11.0.0, which is newer than this version of "
                + "SqlSource knows.  Its models use the types of Npgsql 10.",
        ]);
        SeverityOf(run, "SQLSRC248").ShouldBe(DiagnosticSeverity.Info);
        ProbeOf(run).Column.ShouldBe(DriverColumn.Npgsql10);
    }

    // A query at Sql needs no entry, and one of a file under mssql needs one that is not of postgres: neither reads a
    // type of Npgsql.
    [Theory]
    [InlineData("7.0.0", false, false)]
    [InlineData("11.0.0", true, true)]
    public void Run_NoQueryNeedsAPostgresEntry_ReportsNothingOfTheDriver(string version, bool slimBuilder, bool cube)
    {
        var driver = NpgsqlReferences.Synthetic(version, slimBuilder, cube);

        var sql = Run(OutputKind.Sql, [driver], Users, Orders);
        var mssql = Run(OutputKind.Models, [driver], "mssql", Users);

        sql.Diagnostics.ShouldBeEmpty();
        mssql.Diagnostics.ShouldBe([
            "SQLSRC243 /app/Repo/Users.sql(1,10)-(1,17): This version of SqlSource has no type map for the dialect "
                + "'mssql', so no query of this file can have models.  Set the output to 'sql'.",
        ]);
    }

    // A driver merged into an assembly of another name: its types are found, and its version is not known.  An old
    // one is still below the floor, and a new one is not known to be newer.
    [Fact]
    public void Run_NpgsqlInAnAssemblyOfAnotherName_HasNoVersion()
    {
        var old = Run(
            OutputKind.Models,
            [NpgsqlReferences.Synthetic("7.0.0", slimBuilder: false, cube: false, assemblyName: "Merged")],
            UsersWithSidecar
        );
        var newer = Run(
            OutputKind.Models,
            [NpgsqlReferences.Synthetic("11.0.0", slimBuilder: true, cube: true, assemblyName: "Merged")],
            UsersWithSidecar
        );

        old.Diagnostics.ShouldBe([
            "SQLSRC247 (1,1)-(1,1): This project references Npgsql of an unknown version, and SqlSource needs Npgsql "
                + "8.0 or later.  Update the package.",
        ]);
        ProbeOf(old).ShouldBe(new NpgsqlProbe(true, false, false, null));
        newer.Diagnostics.ShouldBeEmpty();
        var probe = ProbeOf(newer);
        probe.ShouldBe(new NpgsqlProbe(true, true, true, null));
        probe.IsNewerThanKnown.ShouldBeFalse();
    }

    // Assembly names compare ignoring case, as the runtime compares them.
    [Fact]
    public void Run_AssemblyNamedInAnotherCase_GivesItsVersion()
    {
        var run = Run(
            OutputKind.Models,
            [NpgsqlReferences.Synthetic("11.0.0", slimBuilder: true, cube: true, assemblyName: "npgsql")],
            UsersWithSidecar
        );

        ProbeOf(run).Version.ShouldBe(new Version(11, 0, 0, 0));
        run.Diagnostics.ShouldBe([
            "SQLSRC248 (1,1)-(1,1): This project references Npgsql 11.0.0, which is newer than this version of "
                + "SqlSource knows.  Its models use the types of Npgsql 10.",
        ]);
    }

    // The project at the output, under postgres unless the dialect is given, with these references.
    private static GeneratorRun Run(OutputKind output, MetadataReference[] references, params SqlFile[] files) =>
        Run(output, references, "postgres", files);

    private static GeneratorRun Run(
        OutputKind output,
        MetadataReference[] references,
        string dialect,
        params SqlFile[] files
    ) =>
        GeneratorHarness.Run(
            output,
            [new SourceFile(GeneratorHarness.SourcePath, Source)],
            files,
            references: references,
            dialect: dialect
        );

    // The sidecar of the file with a current entry of the query under postgres.
    private static SqlFile CurrentSidecar(SqlFile file, string queryName) =>
        Sidecars.Current(file, SqlDialect.PostgreSql, queryName);

    // What the step of the probe gave, which runs once for the compilation.
    private static NpgsqlProbe ProbeOf(GeneratorRun run) =>
        (NpgsqlProbe)
            run
                .Result.Results.Single()
                .TrackedSteps[TrackingNames.NpgsqlProbe]
                .SelectMany(step => step.Outputs)
                .ShouldHaveSingleItem()
                .Value;

    // The one severity of every diagnostic of the id.
    private static DiagnosticSeverity SeverityOf(GeneratorRun run, string id) =>
        run
            .Result.Diagnostics.Where(diagnostic => diagnostic.Id == id)
            .Select(diagnostic => diagnostic.Severity)
            .Distinct()
            .Single();
}
