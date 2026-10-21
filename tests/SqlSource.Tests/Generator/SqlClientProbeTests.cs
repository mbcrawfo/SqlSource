// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
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

// The probe of the Microsoft.Data.SqlClient a project references, against the drivers themselves and against
// assemblies made to look like one, and the diagnostics of the driver.  Each run is of a query at Models in an mssql
// file whose entry is current.  The probe and the report of the driver do not depend on there being a type map for
// mssql, and until there is one the file also reports SQLSRC243, which these tests leave out by asking for the ids of
// the driver alone.
public sealed class SqlClientProbeTests
{
    private const string Source = """
        using SqlSource;

        namespace App;

        [SqlSourceGenerate]
        public partial class Sample;
        """;

    private static readonly SqlFile Users = new(GeneratorHarness.UsersPath, "-- name: GetUser\nSELECT 1;\n");

    private static readonly SqlFile Orders = new("/app/Repo/Orders.sql", "-- name: GetOrder\nSELECT 2;\n");

    private static readonly SqlFile[] UsersWithSidecar = [Users, CurrentSidecar(Users, "GetUser")];

    // Users as the postgres engine reads it, with the sidecar of its entry.
    private static readonly SqlFile[] PostgresUsers =
    [
        Users,
        Sidecars.Current(Users, SqlDialect.PostgreSql, "GetUser"),
    ];

    private static readonly SqlFile[] BothWithSidecars =
    [
        .. UsersWithSidecar,
        Orders,
        CurrentSidecar(Orders, "GetOrder"),
    ];

    [Theory]
    [InlineData("none")]
    [InlineData("System.Data.SqlClient")]
    [InlineData("6.0")]
    [InlineData("6.1")]
    [InlineData("7")]
    public void Read_EachDriver_GivesItsFlags(string driver)
    {
        var (reference, expected, column) = driver switch
        {
            "6.0" => (
                SqlClientReferences.SqlClient60,
                new SqlClientProbe(true, false, false, "6.0.5"),
                SqlClientColumn.SqlClient61
            ),
            "6.1" => (
                SqlClientReferences.SqlClient61,
                new SqlClientProbe(true, true, false, "6.1.7"),
                SqlClientColumn.SqlClient61
            ),
            "7" => (
                SqlClientReferences.SqlClient7,
                new SqlClientProbe(true, true, true, "7.1.1"),
                SqlClientColumn.SqlClient7
            ),
            "System.Data.SqlClient" => (
                SqlClientReferences.SystemDataSqlClient,
                new SqlClientProbe(false, false, false, null),
                SqlClientColumn.None
            ),
            "none" => (null, new SqlClientProbe(false, false, false, null), SqlClientColumn.None),
            _ => throw new ArgumentOutOfRangeException(nameof(driver), driver, "No such driver."),
        };

        var run = Run(OutputKind.Models, reference is null ? [] : [reference], UsersWithSidecar);

        var probe = StepOutputs.Single<SqlClientProbe>(run, TrackingNames.SqlClientProbe);
        probe.ShouldBe(expected);
        probe.Column.ShouldBe(column);
    }

    [Fact]
    public void Run_SqlClientBelowTheFloor_IsSqlsrc267Once()
    {
        const string Expected =
            "SQLSRC267 (1,1)-(1,1): This project references Microsoft.Data.SqlClient 6.0.5, and SqlSource needs "
            + "Microsoft.Data.SqlClient 6.1 or later.  Update the package.";

        var oneFile = Run(OutputKind.Models, [SqlClientReferences.SqlClient60], UsersWithSidecar);
        var twoFiles = Run(OutputKind.Models, [SqlClientReferences.SqlClient60], BothWithSidecars);

        DriverDiagnostics(oneFile).ShouldBe([Expected]);
        oneFile.SeverityOf("SQLSRC267").ShouldBe(DiagnosticSeverity.Error);
        DriverDiagnostics(twoFiles).ShouldBe([Expected]);
    }

    [Fact]
    public void Run_SqlClientNewerThanTheGeneratorKnows_IsSqlsrc268()
    {
        var driver = SqlClientReferences.Synthetic("8.0.0.0", true, true, true, "8.0.1");

        var run = Run(OutputKind.Models, [driver], BothWithSidecars);

        DriverDiagnostics(run)
            .ShouldBe([
                "SQLSRC268 (1,1)-(1,1): This project references Microsoft.Data.SqlClient 8.0.1, which is newer than "
                    + "this version of SqlSource knows.  Its models use the types of Microsoft.Data.SqlClient 7.",
            ]);
        run.SeverityOf("SQLSRC268").ShouldBe(DiagnosticSeverity.Info);
        StepOutputs
            .Single<SqlClientProbe>(run, TrackingNames.SqlClientProbe)
            .Column.ShouldBe(SqlClientColumn.SqlClient7);
    }

    // A query at Sql needs no entry, and one of a file under postgres needs one that is not of mssql: neither reads a
    // type of the driver.
    [Fact]
    public void Run_NoQueryNeedsAnSqlServerEntry_ReportsNothingOfTheDriver()
    {
        var older = SqlClientReferences.SqlClient60;
        var newer = SqlClientReferences.Synthetic("8.0.0.0", true, true, true, "8.0.1");

        var sql = Run(OutputKind.Sql, [older], Users, Orders);
        var postgres = Run(OutputKind.Models, [older], "postgres", PostgresUsers);
        var postgresNewer = Run(OutputKind.Models, [newer], "postgres", PostgresUsers);

        DriverDiagnostics(sql).ShouldBeEmpty();
        DriverDiagnostics(postgres).ShouldBeEmpty();
        DriverDiagnostics(postgresNewer).ShouldBeEmpty();
    }

    // Each driver is reported for the engine of the files that need its types, and only for that one.
    [Fact]
    public void Run_BothDriversBelowTheirFloors_ReportsEachForItsEngine()
    {
        const string Npgsql =
            "SQLSRC247 (1,1)-(1,1): This project references Npgsql 7.0.0, and SqlSource needs Npgsql 8.0 or later.  "
            + "Update the package.";
        const string SqlClient =
            "SQLSRC267 (1,1)-(1,1): This project references Microsoft.Data.SqlClient 6.0.5, and SqlSource needs "
            + "Microsoft.Data.SqlClient 6.1 or later.  Update the package.";
        MetadataReference[] drivers =
        [
            NpgsqlReferences.Synthetic("7.0.0", slimBuilder: false, cube: false),
            SqlClientReferences.SqlClient60,
        ];
        var sqlServer = Orders with { Dialect = "mssql" };
        SqlFile[] sqlServerFiles = [sqlServer, CurrentSidecar(sqlServer, "GetOrder")];

        var both = Run(OutputKind.Models, drivers, "postgres", [.. PostgresUsers, .. sqlServerFiles]);
        var postgresOnly = Run(OutputKind.Models, drivers, "postgres", PostgresUsers);
        var sqlServerOnly = Run(OutputKind.Models, drivers, "postgres", sqlServerFiles);

        DriverDiagnostics(both).ShouldBe([Npgsql, SqlClient], ignoreOrder: true);
        DriverDiagnostics(postgresOnly).ShouldBe([Npgsql]);
        DriverDiagnostics(sqlServerOnly).ShouldBe([SqlClient]);
    }

    // The version comes from the package's informational version without the commit, or from the major of the
    // identity when the assembly has none.
    [Theory]
    [InlineData("6.0.0.0", null, "6")]
    [InlineData("6.0.0.0", "6.1.7+efca28ca", "6.1.7")]
    [InlineData("6.0.0.0", "6.1.7", "6.1.7")]
    [InlineData("7.0.0.0", "7.2.0-preview1.25281.1+abc", "7.2.0-preview1.25281.1")]
    [InlineData("7.0.0.0", "+abc", "7")]
    [InlineData("7.0.0.0", "", "7")]
    public void Read_InformationalVersion_GivesThePackageVersionOrTheMajor(
        string identity,
        string? informational,
        string expected
    )
    {
        var driver = SqlClientReferences.Synthetic(identity, true, true, false, informational);

        var run = Run(OutputKind.Models, [driver], UsersWithSidecar);

        StepOutputs.Single<SqlClientProbe>(run, TrackingNames.SqlClientProbe).Version.ShouldBe(expected);
    }

    // A driver merged into an assembly of another name: its types are found, and its version is not known.  An old
    // one is still below the floor, and a new one is not known to be newer.
    [Fact]
    public void Run_SqlClientInAnAssemblyOfAnotherName_HasNoVersion()
    {
        var old = Run(
            OutputKind.Models,
            [SqlClientReferences.Synthetic("6.0.0.0", true, false, false, "6.0.5", assemblyName: "Merged")],
            UsersWithSidecar
        );
        var newer = Run(
            OutputKind.Models,
            [SqlClientReferences.Synthetic("8.0.0.0", true, true, true, "8.0.1", assemblyName: "Merged")],
            UsersWithSidecar
        );

        DriverDiagnostics(old)
            .ShouldBe([
                "SQLSRC267 (1,1)-(1,1): This project references Microsoft.Data.SqlClient of an unknown version, and "
                    + "SqlSource needs Microsoft.Data.SqlClient 6.1 or later.  Update the package.",
            ]);
        StepOutputs
            .Single<SqlClientProbe>(old, TrackingNames.SqlClientProbe)
            .ShouldBe(new SqlClientProbe(true, false, false, null));
        DriverDiagnostics(newer).ShouldBeEmpty();
        StepOutputs
            .Single<SqlClientProbe>(newer, TrackingNames.SqlClientProbe)
            .ShouldBe(new SqlClientProbe(true, true, true, null));
    }

    // Assembly names compare ignoring case, as the runtime compares them.
    [Fact]
    public void Run_AssemblyNamedInAnotherCase_GivesItsVersion()
    {
        var driver = SqlClientReferences.Synthetic(
            "8.0.0.0",
            true,
            true,
            true,
            "8.0.1",
            assemblyName: "microsoft.data.sqlclient"
        );

        var run = Run(OutputKind.Models, [driver], UsersWithSidecar);

        StepOutputs.Single<SqlClientProbe>(run, TrackingNames.SqlClientProbe).Version.ShouldBe("8.0.1");
        DriverDiagnostics(run).ShouldHaveSingleItem().ShouldStartWith("SQLSRC268 ");
    }

    [Theory]
    [InlineData("7.1.1", false)]
    [InlineData("7.0.0-preview4", false)]
    [InlineData("8.0.0", true)]
    [InlineData("8.0.1-preview.1", true)]
    [InlineData("10.2.0", true)]
    [InlineData("6", false)]
    [InlineData("preview", false)]
    [InlineData("99999999999.0.0", false)]
    [InlineData(null, false)]
    public void IsNewerThanKnown_Version_IsAMajorAboveSeven(string? version, bool expected) =>
        new SqlClientProbe(true, true, true, version).IsNewerThanKnown.ShouldBe(expected);

    [Fact]
    public void IsNewerThanKnown_NoReference_IsFalse() =>
        new SqlClientProbe(false, false, false, "8.0.0").IsNewerThanKnown.ShouldBeFalse();

    [Theory]
    [InlineData(false, false, false, false)]
    [InlineData(true, false, false, true)]
    [InlineData(true, true, false, false)]
    [InlineData(true, true, true, false)]
    public void IsBelowTheFloor_Flags_IsAReferenceWithoutSqlVector(
        bool referenced,
        bool atLeast61,
        bool atLeast7,
        bool expected
    ) => new SqlClientProbe(referenced, atLeast61, atLeast7, null).IsBelowTheFloor.ShouldBe(expected);

    // The diagnostics of the two drivers, which are the only ones these tests ask about.
    private static IReadOnlyList<string> DriverDiagnostics(GeneratorRun run) =>
        [
            .. run.Diagnostics.Where(diagnostic =>
                diagnostic.StartsWith("SQLSRC247 ", StringComparison.Ordinal)
                || diagnostic.StartsWith("SQLSRC248 ", StringComparison.Ordinal)
                || diagnostic.StartsWith("SQLSRC267 ", StringComparison.Ordinal)
                || diagnostic.StartsWith("SQLSRC268 ", StringComparison.Ordinal)
            ),
        ];

    // The project at the output, under mssql unless the dialect is given, with these references.
    private static GeneratorRun Run(OutputKind output, MetadataReference[] references, params SqlFile[] files) =>
        Run(output, references, "mssql", files);

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

    // The sidecar of the file with a current entry of the query under mssql.
    private static SqlFile CurrentSidecar(SqlFile file, string queryName) =>
        Sidecars.Current(file, SqlDialect.SqlServer, queryName);
}
