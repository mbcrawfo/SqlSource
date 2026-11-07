// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Parsing;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The probe of the Microsoft.Data.SqlClient a project references, against the drivers themselves and against
// assemblies made to look like one, and the diagnostics of the driver.  Each run is of a query at Models in an mssql
// file whose entry is current, unless it says otherwise; the tests of IsNewerThanKnown, IsBelowTheFloor, Column and
// Findings read a probe alone.  The tests ask for the ids of the driver alone.
public sealed class SqlClientProbeTests
{
    private static readonly SqlFile Orders = new("/app/Repo/Orders.sql", "-- name: GetOrder\nSELECT 2;\n");

    private static readonly SqlFile[] UsersWithSidecar =
    [
        GeneratorHarness.UsersFile,
        CurrentSidecar(GeneratorHarness.UsersFile, "GetUser"),
    ];

    // Users as the postgres engine reads it, with the sidecar of its entry.
    private static readonly SqlFile[] PostgresUsers =
    [
        GeneratorHarness.UsersFile,
        Sidecars.Current(GeneratorHarness.UsersFile, SqlDialect.PostgreSql, "GetUser"),
    ];

    private static readonly SqlFile[] BothWithSidecars =
    [
        .. UsersWithSidecar,
        Orders,
        CurrentSidecar(Orders, "GetOrder"),
    ];

    [Fact]
    public void Read_NoDriver_GivesItsFlags() =>
        ShouldReadTheFlags([], new SqlClientProbe(false, false, false, null), SqlClientColumn.None);

    [Fact]
    public void Read_SystemDataSqlClient_GivesItsFlags() =>
        ShouldReadTheFlags(
            [SqlClientReferences.SystemDataSqlClient],
            new SqlClientProbe(false, false, false, null),
            SqlClientColumn.None
        );

    [Fact]
    public void Read_SqlClient60_GivesItsFlags() =>
        ShouldReadTheFlags(
            [SqlClientReferences.SqlClient60],
            new SqlClientProbe(true, false, false, "6.0.5"),
            SqlClientColumn.OlderThan61
        );

    [Fact]
    public void Read_SqlClient61_GivesItsFlags() =>
        ShouldReadTheFlags(
            [SqlClientReferences.SqlClient61],
            new SqlClientProbe(true, true, false, "6.1.7"),
            SqlClientColumn.SqlClient61
        );

    [Fact]
    public void Read_SqlClient7_GivesItsFlags() =>
        ShouldReadTheFlags(
            [SqlClientReferences.SqlClient7],
            new SqlClientProbe(true, true, true, "7.1.1"),
            SqlClientColumn.SqlClient7
        );

    [Fact]
    public void Run_SqlClientBelowTheFloor_IsSqlsrc267Once()
    {
        const string Expected =
            "SQLSRC267 (1,1)-(1,1): This project references Microsoft.Data.SqlClient 6.0.5, and SqlSource needs "
            + "Microsoft.Data.SqlClient 6.1 or later, so no query method is generated.  Update the package.";

        var oneFile = GeneratorHarness.RunSample(
            OutputKind.Models,
            UsersWithSidecar,
            references: [SqlClientReferences.SqlClient60],
            dialect: "mssql"
        );
        var twoFiles = GeneratorHarness.RunSample(
            OutputKind.Models,
            BothWithSidecars,
            references: [SqlClientReferences.SqlClient60],
            dialect: "mssql"
        );

        DriverDiagnostics(oneFile).ShouldBe([Expected]);
        oneFile.SeverityOf("SQLSRC267").ShouldBe(DiagnosticSeverity.Error);
        DriverDiagnostics(twoFiles).ShouldBe([Expected]);
    }

    // A vector column below 6.1 has no C# type, and the project compiles beside its two errors: the project's, and the
    // query's, and no model is written.  On 6.1 the same entry runs clean and writes the query's Dto model alone.
    [Fact]
    public void Run_VectorColumnBelowSqlClient61_IsSqlsrc270AndCompiles()
    {
        const string Expected =
            "SQLSRC270 /app/Repo/Users.sql(1,10)-(1,17): The column 'embedding' has the type 'vector(3)', whose C# "
            + "type needs Microsoft.Data.SqlClient 6.1 or later, and this project references Microsoft.Data.SqlClient "
            + "6.0.5.  Update the package.";
        var entry = TestSidecars.Entry(
            "GetUser",
            Sidecars.HashOf(GeneratorHarness.UsersFile, "GetUser", SqlDialect.SqlServer),
            "mssql",
            columns: [TestSidecars.Column(0, "embedding", TestSidecars.SqlServer("vector(3)", 20, 0))]
        );
        SqlFile[] files = [GeneratorHarness.UsersFile, Sidecars.Of(GeneratorHarness.UsersFile, entry)];

        var older = GeneratorHarness.RunSample(
            OutputKind.Models,
            files,
            references: [SqlClientReferences.SqlClient60],
            dialect: "mssql"
        );
        var current = GeneratorHarness.RunSample(
            OutputKind.Models,
            files,
            references: [SqlClientReferences.SqlClient61],
            dialect: "mssql"
        );

        older
            .Problems()
            .Where(diagnostic => diagnostic.StartsWith("SQLSRC270 ", StringComparison.Ordinal))
            .ShouldBe([Expected]);
        DriverDiagnostics(older).ShouldHaveSingleItem().ShouldStartWith("SQLSRC267 ");
        ModelRuns.ModelFiles(older).ShouldBeEmpty();
        current.ShouldBeClean();
        ModelRuns.ModelFiles(current).ShouldBe([ModelRuns.GetUserDtoFile]);
    }

    [Fact]
    public void Run_SqlClientNewerThanTheGeneratorKnows_IsSqlsrc268()
    {
        var driver = SqlClientReferences.Synthetic("8.0.0.0", true, true, true, "8.0.1");

        var run = GeneratorHarness.RunSample(
            OutputKind.Models,
            BothWithSidecars,
            references: [driver],
            dialect: "mssql"
        );

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

        var sql = GeneratorHarness.RunSample(
            OutputKind.Sql,
            [GeneratorHarness.UsersFile, Orders],
            references: [older],
            dialect: "mssql"
        );
        var postgres = GeneratorHarness.RunSample(
            OutputKind.Models,
            PostgresUsers,
            references: [older],
            dialect: "postgres"
        );
        var postgresNewer = GeneratorHarness.RunSample(
            OutputKind.Models,
            PostgresUsers,
            references: [newer],
            dialect: "postgres"
        );

        DriverDiagnostics(sql).ShouldBeEmpty();
        DriverDiagnostics(postgres).ShouldBeEmpty();
        DriverDiagnostics(postgresNewer).ShouldBeEmpty();
    }

    // Each driver is reported for the engine of the files that need its types, and only for that one.
    [Fact]
    public void Run_BothDriversBelowTheirFloors_ReportsEachForItsEngine()
    {
        const string Npgsql =
            "SQLSRC247 (1,1)-(1,1): This project references Npgsql 7.0.0, and SqlSource needs Npgsql 8.0 or later, so "
            + "no query method is generated.  Update the package.";
        const string SqlClient =
            "SQLSRC267 (1,1)-(1,1): This project references Microsoft.Data.SqlClient 6.0.5, and SqlSource needs "
            + "Microsoft.Data.SqlClient 6.1 or later, so no query method is generated.  Update the package.";
        MetadataReference[] drivers =
        [
            NpgsqlReferences.Synthetic("7.0.0", slimBuilder: false, cube: false),
            SqlClientReferences.SqlClient60,
        ];
        var sqlServer = Orders with { Text = "-- dialect: mssql\n" + Orders.Text };
        SqlFile[] sqlServerFiles = [sqlServer, CurrentSidecar(sqlServer, "GetOrder")];

        var both = GeneratorHarness.RunSample(
            OutputKind.Models,
            [.. PostgresUsers, .. sqlServerFiles],
            references: drivers,
            dialect: "postgres"
        );
        var postgresOnly = GeneratorHarness.RunSample(
            OutputKind.Models,
            PostgresUsers,
            references: drivers,
            dialect: "postgres"
        );
        var sqlServerOnly = GeneratorHarness.RunSample(
            OutputKind.Models,
            sqlServerFiles,
            references: drivers,
            dialect: "postgres"
        );

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

        var run = GeneratorHarness.RunSample(
            OutputKind.Models,
            UsersWithSidecar,
            references: [driver],
            dialect: "mssql"
        );

        StepOutputs.Single<SqlClientProbe>(run, TrackingNames.SqlClientProbe).Version.ShouldBe(expected);
    }

    // An informational version attribute of the assembly's own, whose argument is an array, is not text: the probe
    // falls back to the major, and nothing throws.
    [Fact]
    public void Run_InformationalVersionAttributeTakingAnArray_GivesTheMajor()
    {
        var driver = SqlClientReferences.Synthetic("6.0.0.0", true, true, false, arrayInformationalVersion: true);

        var run = GeneratorHarness.RunSample(
            OutputKind.Models,
            UsersWithSidecar,
            references: [driver],
            dialect: "mssql"
        );

        StepOutputs
            .Single<SqlClientProbe>(run, TrackingNames.SqlClientProbe)
            .ShouldBe(new SqlClientProbe(true, true, false, "6"));
    }

    // A driver merged into an assembly of another name: its types are found, and its version is not known.  An old
    // one is still below the floor, and a new one is not known to be newer.
    [Fact]
    public void Run_SqlClientInAnAssemblyOfAnotherName_HasNoVersion()
    {
        var old = GeneratorHarness.RunSample(
            OutputKind.Models,
            UsersWithSidecar,
            references: [SqlClientReferences.Synthetic("6.0.0.0", true, false, false, "6.0.5", assemblyName: "Merged")],
            dialect: "mssql"
        );
        var newer = GeneratorHarness.RunSample(
            OutputKind.Models,
            UsersWithSidecar,
            references: [SqlClientReferences.Synthetic("8.0.0.0", true, true, true, "8.0.1", assemblyName: "Merged")],
            dialect: "mssql"
        );

        DriverDiagnostics(old)
            .ShouldBe([
                "SQLSRC267 (1,1)-(1,1): This project references Microsoft.Data.SqlClient of an unknown version, and "
                    + "SqlSource needs Microsoft.Data.SqlClient 6.1 or later, so no query method is generated.  Update "
                    + "the package.",
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

        var run = GeneratorHarness.RunSample(
            OutputKind.Models,
            UsersWithSidecar,
            references: [driver],
            dialect: "mssql"
        );

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

    // The column is the newest version the project's driver has, by its flags.
    [Theory]
    [InlineData(false, false, false, nameof(SqlClientColumn.None))]
    [InlineData(true, false, false, nameof(SqlClientColumn.OlderThan61))]
    // SqlVector`1 found without SqlConnection chooses the column of 6.1, as SspiContextProvider without it chooses 7's.
    [InlineData(false, true, false, nameof(SqlClientColumn.SqlClient61))]
    [InlineData(true, true, false, nameof(SqlClientColumn.SqlClient61))]
    [InlineData(true, true, true, nameof(SqlClientColumn.SqlClient7))]
    public void Column_Flags_IsTheNewestVersionTheProjectHas(
        bool referenced,
        bool atLeast61,
        bool atLeast7,
        string expected
    ) => new SqlClientProbe(referenced, atLeast61, atLeast7, null).Column.ToString().ShouldBe(expected);

    [Fact]
    public void Findings_ProbeBelowTheFloor_IsSqlsrc267WithTheVersion() =>
        new SqlClientProbe(true, false, false, "5.2.2").Findings.ShouldBe([
            new DriverFinding(SqlDiagnostics.SqlClientTooOld, "5.2.2"),
        ]);

    [Fact]
    public void Findings_ProbeNewerThanKnown_IsSqlsrc268WithTheVersion() =>
        new SqlClientProbe(true, true, true, "8.0.1").Findings.ShouldBe([
            new DriverFinding(SqlDiagnostics.SqlClientNewerThanKnown, "8.0.1"),
        ]);

    [Fact]
    public void Findings_ProbeBelowTheFloorAndNewerThanKnown_IsTooOldThenNewer() =>
        new SqlClientProbe(true, false, false, "8.0.0").Findings.ShouldBe([
            new DriverFinding(SqlDiagnostics.SqlClientTooOld, "8.0.0"),
            new DriverFinding(SqlDiagnostics.SqlClientNewerThanKnown, "8.0.0"),
        ]);

    [Fact]
    public void Findings_ReferencedProbeWithoutAVersion_NamesTheVersionAsUnknown() =>
        new SqlClientProbe(true, false, false, null).Findings.ShouldBe([
            new DriverFinding(SqlDiagnostics.SqlClientTooOld, "of an unknown version"),
        ]);

    [Theory]
    [InlineData(false, false, false, null)]
    [InlineData(true, true, false, "6.1.0")]
    [InlineData(true, true, true, "7.0.0")]
    public void Findings_ProbeOfAKnownVersion_IsEmpty(
        bool referenced,
        bool atLeast61,
        bool atLeast7,
        string? version
    ) => new SqlClientProbe(referenced, atLeast61, atLeast7, version).Findings.ShouldBeEmpty();

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

    // Users with its sidecar at Models, with the driver's references, gives the probe and column.
    private static void ShouldReadTheFlags(
        MetadataReference[] references,
        SqlClientProbe expected,
        SqlClientColumn column
    )
    {
        var run = GeneratorHarness.RunSample(
            OutputKind.Models,
            UsersWithSidecar,
            references: references,
            dialect: "mssql"
        );

        var probe = StepOutputs.Single<SqlClientProbe>(run, TrackingNames.SqlClientProbe);
        probe.ShouldBe(expected);
        probe.Column.ShouldBe(column);
    }

    // The sidecar of the file with a current entry of the query under mssql.
    private static SqlFile CurrentSidecar(SqlFile file, string queryName) =>
        Sidecars.Current(file, SqlDialect.SqlServer, queryName);
}
