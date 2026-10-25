// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Generation;
using SqlSource.Generation.Types;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;
using OutputKind = SqlSource.Settings.OutputKind;

namespace SqlSource.Tests.Generator;

// The probe of the Npgsql a project references, against the drivers themselves and against assemblies made to look
// like one, and the diagnostics of the driver.  Each test but one runs a query at Models in a postgres file whose
// entry is current, with no columns but in the tests of the types a project without Npgsql can have; that one shows
// that a query at Sql or under mssql reads nothing of the driver.
public sealed class NpgsqlProbeTests
{
    private const string Source = """
        using SqlSource;

        namespace App;

        [SqlSourceGenerate]
        public partial class Sample;
        """;

    // A project with a type of its own for the override to name.
    private const string SourceWithDateSpan = """
        using SqlSource;

        namespace App
        {
            [SqlSourceGenerate]
            public partial class Sample;
        }

        namespace MyApp
        {
            public readonly struct DateSpan;
        }
        """;

    // The warning ids of a C# type whose use gives none.
    private static readonly EquatableArray<string> NoWarnings = EquatableArray<string>.Empty;

    // Each type whose C# type differs between Npgsql 8 and 9 and Npgsql 10, or comes from one that does, by name.
    private static readonly Dictionary<string, PostgresType> Differing = new()
    {
        ["date"] = Catalog("date"),
        ["time"] = Catalog("time"),
        ["cidr"] = Catalog("cidr"),
        ["daterange"] = Catalog("daterange", SidecarValues.Kind.Range, subtype: Catalog("date")),
        ["datemultirange"] = Catalog(
            "datemultirange",
            SidecarValues.Kind.Multirange,
            subtype: Catalog("daterange", SidecarValues.Kind.Range, subtype: Catalog("date"))
        ),
        ["array of date"] = TestSidecars.Postgres(
            name: "date[]",
            kind: SidecarValues.Kind.Array,
            internalName: "_date",
            element: Catalog("date")
        ),
        ["domain over time"] = TestSidecars.Postgres(
            name: "clock",
            kind: SidecarValues.Kind.Domain,
            schema: "public",
            internalName: "clock",
            baseType: Catalog("time")
        ),
    };

    // The case of Differing, and the help: the type override's entry for each driver whose C# type is not of
    // NpgsqlTypes, which cannot be named without Npgsql, and for daterange and datemultirange, whose are on every
    // driver, Npgsql alone.
    public static TheoryData<string, string> DifferingHelp =>
        new()
        {
            { "date", OrMap("date=System.DateTime on Npgsql 8 and 9, date=System.DateOnly on Npgsql 10") },
            { "time", OrMap("time=System.TimeSpan on Npgsql 8 and 9, time=System.TimeOnly on Npgsql 10") },
            { "cidr", OrMap("cidr=System.Net.IPNetwork on Npgsql 10") },
            { "daterange", "Reference Npgsql." },
            { "datemultirange", "Reference Npgsql." },
            { "array of date", OrMap("date=System.DateTime on Npgsql 8 and 9, date=System.DateOnly on Npgsql 10") },
            { "domain over time", OrMap("time=System.TimeSpan on Npgsql 8 and 9, time=System.TimeOnly on Npgsql 10") },
        };

    // The case of Differing, the override, and the C# type it gives with the key of its entry.
    public static TheoryData<string, string, string, bool, string> DifferingOverridden =>
        new()
        {
            { "date", "date=System.DateOnly", "global::System.DateOnly", true, "date" },
            { "time", "time=System.TimeOnly", "global::System.TimeOnly", true, "time" },
            { "cidr", "cidr=System.Net.IPNetwork", "global::System.Net.IPNetwork", true, "cidr" },
            { "daterange", "daterange=MyApp.DateSpan", "global::MyApp.DateSpan", true, "daterange" },
            {
                "datemultirange",
                "datemultirange=MyApp.DateSpan[]",
                "global::MyApp.DateSpan[]",
                false,
                "datemultirange"
            },
            { "array of date", "date=System.DateOnly", "global::System.DateOnly[]", false, "date" },
            { "domain over time", "time=System.TimeOnly", "global::System.TimeOnly", true, "time" },
        };

    private static readonly SqlFile Orders = new("/app/Repo/Orders.sql", "-- name: GetOrder\nSELECT 2;\n");

    private static readonly SqlFile[] UsersWithSidecar =
    [
        GeneratorHarness.UsersFile,
        CurrentSidecar(GeneratorHarness.UsersFile, "GetUser"),
    ];

    private static readonly SqlFile[] BothWithSidecars =
    [
        .. UsersWithSidecar,
        Orders,
        CurrentSidecar(Orders, "GetOrder"),
    ];

    [Fact]
    public void Read_NoDriver_GivesItsFlags() =>
        ShouldReadTheFlags([], new NpgsqlProbe(false, false, false, null), NpgsqlColumn.None);

    [Fact]
    public void Read_Npgsql8_GivesItsFlags() =>
        ShouldReadTheFlags(
            [NpgsqlReferences.Npgsql8],
            new NpgsqlProbe(true, true, false, new Version(8, 0, 3, 0)),
            NpgsqlColumn.Npgsql8
        );

    [Fact]
    public void Read_Npgsql9_GivesItsFlags() =>
        ShouldReadTheFlags(
            [NpgsqlReferences.Npgsql9],
            new NpgsqlProbe(true, true, false, new Version(9, 0, 3, 0)),
            NpgsqlColumn.Npgsql8
        );

    [Fact]
    public void Read_Npgsql10_GivesItsFlags() =>
        ShouldReadTheFlags(
            [NpgsqlReferences.Npgsql10],
            new NpgsqlProbe(true, true, true, new Version(10, 0, 3, 0)),
            NpgsqlColumn.Npgsql10
        );

    [Fact]
    public void Findings_ProbeBelowTheFloor_IsSqlsrc247WithTheVersion() =>
        new NpgsqlProbe(true, false, false, new Version(7, 0, 4, 0)).Findings.ShouldBe([
            new DriverFinding(SqlDiagnostics.NpgsqlTooOld, "7.0.4"),
        ]);

    [Fact]
    public void Findings_ProbeNewerThanKnown_IsSqlsrc248WithTheVersion() =>
        new NpgsqlProbe(true, true, true, new Version(11, 0, 1, 0)).Findings.ShouldBe([
            new DriverFinding(SqlDiagnostics.NpgsqlNewerThanKnown, "11.0.1"),
        ]);

    [Fact]
    public void Findings_ProbeBelowTheFloorAndNewerThanKnown_IsTooOldThenNewer() =>
        new NpgsqlProbe(true, false, false, new Version(11, 0, 0, 0)).Findings.ShouldBe([
            new DriverFinding(SqlDiagnostics.NpgsqlTooOld, "11.0.0"),
            new DriverFinding(SqlDiagnostics.NpgsqlNewerThanKnown, "11.0.0"),
        ]);

    [Fact]
    public void Findings_ReferencedProbeWithoutAVersion_NamesTheVersionAsUnknown() =>
        new NpgsqlProbe(true, false, false, null).Findings.ShouldBe([
            new DriverFinding(SqlDiagnostics.NpgsqlTooOld, "of an unknown version"),
        ]);

    [Theory]
    [InlineData(false, false, false, null)]
    [InlineData(true, true, false, "8.0.3")]
    [InlineData(true, true, true, "10.0.3")]
    public void Findings_ProbeOfAKnownVersion_IsEmpty(
        bool referenced,
        bool atLeast8,
        bool atLeast10,
        string? version
    ) =>
        new NpgsqlProbe(
            referenced,
            atLeast8,
            atLeast10,
            version is null ? null : Version.Parse(version)
        ).Findings.ShouldBeEmpty();

    [Fact]
    public void Run_NpgsqlBelowTheFloor_IsSqlsrc247Once()
    {
        const string Expected =
            "SQLSRC247 (1,1)-(1,1): This project references Npgsql 7.0.0, and SqlSource needs Npgsql 8.0 or later.  "
            + "No query method is generated until the package is updated.";
        var driver = NpgsqlReferences.Synthetic("7.0.0", slimBuilder: false, cube: false);

        var oneFile = Run(OutputKind.Models, [driver], UsersWithSidecar);
        var twoFiles = Run(OutputKind.Models, [driver], BothWithSidecars);

        oneFile.Diagnostics.ShouldBe([Expected]);
        oneFile.SeverityOf("SQLSRC247").ShouldBe(DiagnosticSeverity.Error);
        StepOutputs.Single<NpgsqlProbe>(oneFile, TrackingNames.NpgsqlProbe).Column.ShouldBe(NpgsqlColumn.Npgsql8);
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
        run.SeverityOf("SQLSRC248").ShouldBe(DiagnosticSeverity.Info);
        StepOutputs.Single<NpgsqlProbe>(run, TrackingNames.NpgsqlProbe).Column.ShouldBe(NpgsqlColumn.Npgsql10);
    }

    // A query at Sql needs no entry, and one of a file under mssql needs one that is not of postgres: neither reads a
    // type of Npgsql.
    [Theory]
    [InlineData("7.0.0", false, false)]
    [InlineData("11.0.0", true, true)]
    public void Run_NoQueryNeedsAPostgresEntry_ReportsNothingOfTheDriver(string version, bool slimBuilder, bool cube)
    {
        var driver = NpgsqlReferences.Synthetic(version, slimBuilder, cube);

        var sql = Run(OutputKind.Sql, [driver], GeneratorHarness.UsersFile, Orders);
        var mssql = Run(
            OutputKind.Models,
            [driver],
            "mssql",
            GeneratorHarness.UsersFile,
            Sidecars.Current(GeneratorHarness.UsersFile, SqlDialect.SqlServer, "GetUser")
        );

        sql.Diagnostics.ShouldBeEmpty();
        mssql.Diagnostics.ShouldBeEmpty();
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
                + "8.0 or later.  No query method is generated until the package is updated.",
        ]);
        StepOutputs
            .Single<NpgsqlProbe>(old, TrackingNames.NpgsqlProbe)
            .ShouldBe(new NpgsqlProbe(true, false, false, null));
        newer.Diagnostics.ShouldBeEmpty();
        var probe = StepOutputs.Single<NpgsqlProbe>(newer, TrackingNames.NpgsqlProbe);
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

        StepOutputs.Single<NpgsqlProbe>(run, TrackingNames.NpgsqlProbe).Version.ShouldBe(new Version(11, 0, 0, 0));
        run.Diagnostics.ShouldBe([
            "SQLSRC248 (1,1)-(1,1): This project references Npgsql 11.0.0, which is newer than this version of "
                + "SqlSource knows.  Its models use the types of Npgsql 10.",
        ]);
    }

    [Theory]
    [MemberData(nameof(DifferingHelp))]
    public void Run_TypeThatDiffersWithoutNpgsql_IsSqlsrc249(string name, string help)
    {
        var run = RunWithColumn(Differing[name], overrides: null);

        run.Diagnostics.ShouldBe([
            $"SQLSRC249 {GeneratorHarness.UsersPath}(1,10)-(1,17): The column 'value' has the type "
                + $"'{Differing[name].Name}', whose C# type depends on Npgsql, and this project does not reference "
                + $"it.  {help}",
        ]);
        run.SeverityOf("SQLSRC249").ShouldBe(DiagnosticSeverity.Error);
        StepOutputs.Single<FileTypes>(run, TrackingNames.FileTypes).Queries.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(DifferingOverridden))]
    public void Run_TypeThatDiffersWithoutNpgsql_TakesTheOverridesType(
        string name,
        string overrides,
        string expected,
        bool isValueType,
        string key
    )
    {
        var run = RunWithColumn(Differing[name], overrides);

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs
            .Single<FileTypes>(run, TrackingNames.FileTypes)
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldBe([
                new PropertyType(expected, isValueType, true, NoWarnings, key, "the property SqlSourceTypeOverrides"),
            ]);
    }

    [Fact]
    public void Run_TypeOfNpgsqlTypesWithoutNpgsql_IsSqlsrc249()
    {
        var run = RunWithColumn(Catalog("point"), overrides: null);

        run.Diagnostics.ShouldBe([
            $"SQLSRC249 {GeneratorHarness.UsersPath}(1,10)-(1,17): The column 'value' has the type 'point', whose C# "
                + "type depends on Npgsql, and this project does not reference it.  Reference Npgsql.",
        ]);
    }

    [Theory]
    [InlineData("int4", "int", true)]
    [InlineData("text", "string", false)]
    [InlineData("timestamptz", "global::System.DateTime", true)]
    public void Run_TypeThatIsTheSameOnEveryDriverWithoutNpgsql_IsMapped(
        string internalName,
        string expected,
        bool isValueType
    )
    {
        var run = RunWithColumn(Catalog(internalName), overrides: null);

        run.Diagnostics.ShouldBeEmpty();
        StepOutputs
            .Single<FileTypes>(run, TrackingNames.FileTypes)
            .Queries.ShouldHaveSingleItem()
            .Columns.ShouldBe([new PropertyType(expected, isValueType, true, NoWarnings, internalName, null)]);
    }

    // Users with its sidecar at Models, with the driver's references, gives no diagnostic and the probe and column.
    private static void ShouldReadTheFlags(MetadataReference[] references, NpgsqlProbe expected, NpgsqlColumn column)
    {
        var run = Run(OutputKind.Models, references, UsersWithSidecar);

        run.Diagnostics.ShouldBeEmpty();
        var probe = StepOutputs.Single<NpgsqlProbe>(run, TrackingNames.NpgsqlProbe);
        probe.ShouldBe(expected);
        probe.Column.ShouldBe(column);
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

    // The help of SQLSRC249 that offers the type override's entries.
    private static string OrMap(string entries) =>
        "Reference Npgsql, or map it with the type override for the Npgsql the application runs on: " + entries + ".";

    // A query of Users at Models, with no Npgsql, whose entry has one column of the type, and the property's type
    // override, or none.
    private static GeneratorRun RunWithColumn(PostgresType type, string? overrides)
    {
        var entry = TestSidecars.Entry(
            "GetUser",
            Sidecars.HashOf(GeneratorHarness.UsersFile, "GetUser", SqlDialect.PostgreSql),
            "postgres",
            "main",
            "18.0",
            SidecarResultKind.Rows,
            columns: [TestSidecars.Column(0, "value", type)]
        );
        return GeneratorHarness.Run(
            OutputKind.Models,
            [new SourceFile(GeneratorHarness.SourcePath, SourceWithDateSpan)],
            [GeneratorHarness.UsersFile, Sidecars.Of(GeneratorHarness.UsersFile, entry)],
            dialect: "postgres",
            properties: new Dictionary<string, string?> { ["SqlSourceTypeOverrides"] = overrides }
        );
    }

    private static PostgresType Catalog(
        string internalName,
        string kind = SidecarValues.Kind.Base,
        PostgresType? subtype = null
    ) => TestSidecars.Postgres(name: internalName, kind: kind, internalName: internalName, subtype: subtype);

    // The sidecar of the file with a current entry of the query under postgres.
    private static SqlFile CurrentSidecar(SqlFile file, string queryName) =>
        Sidecars.Current(file, SqlDialect.PostgreSql, queryName);
}
