// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation.Types.Drivers;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The probe of the libraries a project references, against the packages themselves and against assemblies made to
// look like one.  The probe is read from a compilation alone: nothing of the generator's run is needed.
public sealed class LibraryProbeTests
{
    private const string Source = """
        namespace App;

        public class Sample;
        """;

    [Fact]
    public void Read_NoReferences_IsUnavailableEverywhere()
    {
        var probe = LibraryProbe.Read(CompilationOf());

        // The runtime's references carry System.Text.Json.
        probe.ShouldBe(new LibraryProbe(NodaTime: false, NpgsqlNodaTime: false, NodaTimeVersion: null, Json: true));
        probe.IsAvailable(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBeFalse();
        probe.IsAvailable(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBeFalse();
        probe.DefaultOf(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBe(LibraryState.Off);
        probe.DefaultOf(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBe(LibraryState.Off);
    }

    [Fact]
    public void Read_NodaTimeAlone_IsAvailableOnSqlServerOnly()
    {
        var probe = LibraryProbe.Read(CompilationOf(LibraryReferences.NodaTime("3.3.7")));

        probe.NodaTime.ShouldBeTrue();
        probe.NpgsqlNodaTime.ShouldBeFalse();
        probe.IsAvailable(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBeTrue();
        probe.IsAvailable(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBeFalse();
        probe.DefaultOf(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBe(LibraryState.Off);
    }

    // The plugin without NodaTime itself is not enough: its types are the ones a column's value needs.
    [Fact]
    public void Read_PluginAlone_IsUnavailableEverywhere()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(NpgsqlReferences.Npgsql10, LibraryReferences.NpgsqlNodaTime("10.0.3"))
        );

        probe.NodaTime.ShouldBeFalse();
        probe.NpgsqlNodaTime.ShouldBeTrue();
        probe.IsAvailable(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBeFalse();
        probe.IsAvailable(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBeFalse();
    }

    [Fact]
    public void Read_NodaTimeAndPlugin_IsOnByDefaultOnPostgres()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(
                NpgsqlReferences.Npgsql10,
                LibraryReferences.NodaTime("3.3.7"),
                LibraryReferences.NpgsqlNodaTime("10.0.3")
            )
        );

        probe.NodaTime.ShouldBeTrue();
        probe.NpgsqlNodaTime.ShouldBeTrue();
        probe.IsAvailable(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBeTrue();
        probe.IsAvailable(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBeTrue();
        probe.DefaultOf(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBe(LibraryState.On);
    }

    [Fact]
    public void Read_Floor_IsOnByDefaultOnPostgres()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(
                NpgsqlReferences.Npgsql8,
                LibraryReferences.NodaTime("3.1.9"),
                LibraryReferences.NpgsqlNodaTime("8.0.3")
            )
        );

        probe.NodaTime.ShouldBeTrue();
        probe.NpgsqlNodaTime.ShouldBeTrue();
        probe.DefaultOf(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBe(LibraryState.On);
        probe.NodaTimeVersion.ShouldNotBeNull().Major.ShouldBe(3);
    }

    // Of two references named NodaTime, the newer gives the version.
    [Fact]
    public void Read_RecordsNodaTimesVersion()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(
                LibraryReferences.Synthetic("3.2.1.0", nodaTimeTypes: true, pluginExtensions: false),
                LibraryReferences.Synthetic("3.4.0.0", nodaTimeTypes: true, pluginExtensions: false)
            )
        );

        probe.NodaTimeVersion.ShouldBe(new Version(3, 4, 0, 0));
    }

    // An assembly of another name that defines the types, as a library merged into one does, is found, and gives no
    // version.
    [Fact]
    public void Read_MergedAssembly_IsFoundWithoutAVersion()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(
                LibraryReferences.Synthetic(
                    "1.0.0.0",
                    nodaTimeTypes: true,
                    pluginExtensions: true,
                    assemblyName: "Merged"
                )
            )
        );

        probe.ShouldBe(new LibraryProbe(NodaTime: true, NpgsqlNodaTime: true, NodaTimeVersion: null, Json: true));
    }

    [Fact]
    public void DefaultOf_SqlServer_IsOff()
    {
        var probe = new LibraryProbe(NodaTime: true, NpgsqlNodaTime: true, NodaTimeVersion: new Version(3, 3, 0, 0));

        probe.DefaultOf(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBe(LibraryState.Off);
    }

    // NodaTime's floor is 3.1.9, told by the version of the assembly named NodaTime.
    [Fact]
    public void IsBelowTheFloor_NodaTime318_IsTrue()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(LibraryReferences.Synthetic("3.1.8.0", nodaTimeTypes: true, pluginExtensions: false))
        );

        probe.IsBelowTheFloor(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBeTrue();
        probe.IsBelowTheFloor(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBeTrue();
        probe
            .PackagesBelowTheFloor(SqlLibrary.NodaTime, DatabaseEngine.SqlServer)
            .ShouldBe([("NodaTime", "3.1.8", "3.1.9")]);
    }

    [Fact]
    public void IsBelowTheFloor_NodaTime319_IsFalse()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(LibraryReferences.Synthetic("3.1.9.0", nodaTimeTypes: true, pluginExtensions: false))
        );

        probe.IsBelowTheFloor(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBeFalse();
    }

    // No NodaTime, or its types in an assembly of another name, which gives no version: nothing is below the floor.
    [Fact]
    public void IsBelowTheFloor_NoNodaTime_IsFalse()
    {
        var none = LibraryProbe.Read(CompilationOf());
        var merged = LibraryProbe.Read(
            CompilationOf(
                LibraryReferences.Synthetic(
                    "1.0.0.0",
                    nodaTimeTypes: true,
                    pluginExtensions: false,
                    assemblyName: "Merged"
                )
            )
        );

        none.IsBelowTheFloor(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBeFalse();
        merged.IsBelowTheFloor(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBeFalse();
    }

    // An assembly named NodaTime without its types is not NodaTime, whatever its version.
    [Fact]
    public void IsBelowTheFloor_AssemblyWithoutTheTypes_IsFalse()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(LibraryReferences.Synthetic("2.0.0.0", nodaTimeTypes: false, pluginExtensions: false))
        );

        probe.IsBelowTheFloor(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBeFalse();
    }

    [Fact]
    public void MissingAssembliesOf_NamesThePluginOnPostgres()
    {
        LibraryProbe.MissingAssembliesOf(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBe("Npgsql.NodaTime");
        LibraryProbe.MissingAssembliesOf(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBe("NodaTime");
    }

    // System.Text.Json is part of the runtime; a compilation that does not reference it has no JsonElement.
    [Fact]
    public void Read_SystemTextJson_FindsJson()
    {
        LibraryProbe.Read(CompilationOf()).Json.ShouldBeTrue();
        LibraryProbe
            .Read(
                GeneratorHarness.CreateCompilation(
                    [new SourceFile("/app/Sample.cs", Source)],
                    supportedFramework: false
                )
            )
            .Json.ShouldBeFalse();
    }

    [Fact]
    public void DefaultOf_Json_IsOffOnBothEngines()
    {
        var probe = LibraryProbe.Read(CompilationOf());
        var without = new LibraryProbe(NodaTime: false, NpgsqlNodaTime: false, NodaTimeVersion: null);

        probe.Json.ShouldBeTrue();
        foreach (var engine in new[] { DatabaseEngine.PostgreSql, DatabaseEngine.SqlServer })
        {
            probe.DefaultOf(SqlLibrary.Json, engine).ShouldBe(LibraryState.Off);
            probe.IsAvailable(SqlLibrary.Json, engine).ShouldBeTrue();
            without.IsAvailable(SqlLibrary.Json, engine).ShouldBeFalse();
            without.DefaultOf(SqlLibrary.Json, engine).ShouldBe(LibraryState.Off);
        }

        probe.IsBelowTheFloor(SqlLibrary.Json, DatabaseEngine.PostgreSql).ShouldBeFalse();
        probe.IsBelowTheFloor(SqlLibrary.Json, DatabaseEngine.SqlServer).ShouldBeFalse();
    }

    [Fact]
    public void MissingAssembliesOf_NamesSystemTextJson()
    {
        LibraryProbe.MissingAssembliesOf(SqlLibrary.Json, DatabaseEngine.PostgreSql).ShouldBe("System.Text.Json");
        LibraryProbe.MissingAssembliesOf(SqlLibrary.Json, DatabaseEngine.SqlServer).ShouldBe("System.Text.Json");
    }

    [Fact]
    public void Read_NetTopologySuite260WithPlugin_SetsEveryFlag()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(
                NpgsqlReferences.Npgsql10,
                LibraryReferences.NetTopologySuite("2.6.0"),
                LibraryReferences.NpgsqlNetTopologySuite("10.0.3")
            )
        );

        probe.NetTopologySuite.ShouldBeTrue();
        probe.NetTopologySuiteAtFloor.ShouldBeTrue();
        probe.NpgsqlNetTopologySuite.ShouldBeTrue();
        probe.NetTopologySuiteVersion.ShouldBe("2.6.0");
        probe.SqlServerBytes.ShouldBeFalse();
        probe.SqlServerBytesVersion.ShouldBeNull();
    }

    // The floor's package: Npgsql.NetTopologySuite 8.0.9 brings no NetTopologySuite older than it.
    [Fact]
    public void Read_NetTopologySuite250WithPlugin8_IsAtTheFloor()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(
                NpgsqlReferences.Npgsql8,
                LibraryReferences.NetTopologySuite("2.5.0"),
                LibraryReferences.NpgsqlNetTopologySuite("8.0.9")
            )
        );

        probe.NetTopologySuiteAtFloor.ShouldBeTrue();
        probe.NpgsqlNetTopologySuite.ShouldBeTrue();
        probe.NetTopologySuiteVersion.ShouldBe("2.5.0");
    }

    // 2.0.0 and 2.1.0 have the same identity, so the informational version alone tells them apart.
    [Fact]
    public void Read_SqlServerBytes210_SetsItsVersion()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(LibraryReferences.NetTopologySuite("2.6.0"), LibraryReferences.SqlServerBytes("2.1.0"))
        );

        probe.SqlServerBytes.ShouldBeTrue();
        probe.SqlServerBytesVersion.ShouldBe("2.1.0");
        probe.NpgsqlNetTopologySuite.ShouldBeFalse();
    }

    [Fact]
    public void Read_SqlServerBytes200_SetsItsVersion()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(LibraryReferences.NetTopologySuite("2.6.0"), LibraryReferences.SqlServerBytes("2.0.0"))
        );

        probe.SqlServerBytesVersion.ShouldBe("2.0.0");
    }

    // Every 2.x is 2.0.0.0, so a NetTopologySuite without ConcaveHull, first in 2.5.0, is older than the floor.
    [Fact]
    public void Read_NetTopologySuiteSynthetic240_IsNotAtTheFloor()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(LibraryReferences.SyntheticNetTopologySuite("2.4.0+0123abcd", concaveHull: false))
        );

        probe.NetTopologySuite.ShouldBeTrue();
        probe.NetTopologySuiteAtFloor.ShouldBeFalse();
        probe.NetTopologySuiteVersion.ShouldBe("2.4.0");
    }

    // A version followed by a description, as NetTopologySuite's own are, gives the version alone.
    [Fact]
    public void Read_NetTopologySuiteVersionWithADescription_GivesTheVersionAlone()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(
                LibraryReferences.SyntheticNetTopologySuite("2.5.1 (compatible with JTS 1.19)+0123", concaveHull: true)
            )
        );

        probe.NetTopologySuiteAtFloor.ShouldBeTrue();
        probe.NetTopologySuiteVersion.ShouldBe("2.5.1");
    }

    // NetTopologySuite without ConcaveHull is below its floor on both engines, whatever version it says.
    [Fact]
    public void IsBelowTheFloor_NetTopologySuite240_IsTrueOnBothEngines()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(LibraryReferences.SyntheticNetTopologySuite("2.4.0", concaveHull: false))
        );

        probe.IsBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql).ShouldBeTrue();
        probe.IsBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer).ShouldBeTrue();
        probe
            .PackagesBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql)
            .ShouldBe([("NetTopologySuite", "2.4.0", "2.5.0")]);
    }

    // An assembly that gives no informational version is named as of an unknown version.
    [Fact]
    public void PackagesBelowTheFloor_NetTopologySuiteWithoutAVersion_SaysItIsUnknown()
    {
        var probe = new LibraryProbe(
            NodaTime: false,
            NpgsqlNodaTime: false,
            NodaTimeVersion: null,
            NetTopologySuite: true,
            NetTopologySuiteAtFloor: false
        );

        probe
            .PackagesBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql)
            .ShouldBe([("NetTopologySuite", "of an unknown version", "2.5.0")]);
    }

    // NetTopologySuite.IO.SqlServerBytes below 2.1.0, told by its informational version, is below the floor only for
    // SQL Server's queries, which alone use it.
    [Fact]
    public void IsBelowTheFloor_SqlServerBytes200_IsTrueOnSqlServerOnly()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(LibraryReferences.NetTopologySuite("2.5.0"), LibraryReferences.SqlServerBytes("2.0.0"))
        );

        probe.IsBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql).ShouldBeFalse();
        probe.IsBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer).ShouldBeTrue();
        probe.PackagesBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql).ShouldBeEmpty();
        probe
            .PackagesBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer)
            .ShouldBe([("NetTopologySuite.IO.SqlServerBytes", "2.0.0", "2.1.0")]);
    }

    // The version's leading numbers decide, with a missing build read as 0, and a version that starts with no number
    // decides nothing.
    [Theory]
    [InlineData("2.0.0", true)]
    [InlineData("1.15.0", true)]
    [InlineData("2.0", true)]
    [InlineData("2.1", false)]
    [InlineData("2.1.0", false)]
    [InlineData("2.1.0-beta1", false)]
    [InlineData("2.2.0", false)]
    [InlineData("unknown", false)]
    [InlineData(null, false)]
    public void IsBelowTheFloor_SqlServerBytesVersion_IsBelow210(string? version, bool expected)
    {
        var probe = new LibraryProbe(
            NodaTime: false,
            NpgsqlNodaTime: false,
            NodaTimeVersion: null,
            NetTopologySuite: true,
            NetTopologySuiteAtFloor: true,
            SqlServerBytes: true,
            SqlServerBytesVersion: version
        );

        probe.IsBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer).ShouldBe(expected);
    }

    // Both below on SQL Server: each package once, NetTopologySuite first.
    [Fact]
    public void PackagesBelowTheFloor_BothBelowOnSqlServer_NamesEach()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(
                LibraryReferences.SyntheticNetTopologySuite("2.4.0", concaveHull: false),
                LibraryReferences.SqlServerBytes("2.0.0")
            )
        );

        probe
            .PackagesBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer)
            .ShouldBe([
                ("NetTopologySuite", "2.4.0", "2.5.0"),
                ("NetTopologySuite.IO.SqlServerBytes", "2.0.0", "2.1.0"),
            ]);
    }

    [Fact]
    public void IsBelowTheFloor_NetTopologySuiteAtItsFloors_IsFalse()
    {
        var probe = LibraryProbe.Read(
            CompilationOf(LibraryReferences.NetTopologySuite("2.5.0"), LibraryReferences.SqlServerBytes("2.1.0"))
        );

        probe.IsBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql).ShouldBeFalse();
        probe.IsBelowTheFloor(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer).ShouldBeFalse();
    }

    [Theory]
    [InlineData(true, false, true, false)]
    [InlineData(false, true, false, true)]
    [InlineData(true, true, true, true)]
    [InlineData(false, false, false, false)]
    public void IsAvailable_NetTopologySuite_ByEngine(
        bool plugin,
        bool sqlServerBytes,
        bool onPostgres,
        bool onSqlServer
    )
    {
        var probe = new LibraryProbe(
            NodaTime: false,
            NpgsqlNodaTime: false,
            NodaTimeVersion: null,
            NetTopologySuite: true,
            NetTopologySuiteAtFloor: true,
            NetTopologySuiteVersion: "2.6.0",
            NpgsqlNetTopologySuite: plugin,
            SqlServerBytes: sqlServerBytes,
            SqlServerBytesVersion: sqlServerBytes ? "2.1.0" : null
        );

        probe.IsAvailable(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql).ShouldBe(onPostgres);
        probe.IsAvailable(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer).ShouldBe(onSqlServer);
    }

    // The plugins without NetTopologySuite itself are not enough: its types are the ones a column's value needs.
    [Fact]
    public void IsAvailable_NetTopologySuitePluginsWithoutTheLibrary_IsFalse()
    {
        var probe = new LibraryProbe(
            NodaTime: false,
            NpgsqlNodaTime: false,
            NodaTimeVersion: null,
            NpgsqlNetTopologySuite: true,
            SqlServerBytes: true
        );

        probe.IsAvailable(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql).ShouldBeFalse();
        probe.IsAvailable(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer).ShouldBeFalse();
    }

    [Fact]
    public void DefaultOf_NetTopologySuite_OnOnlyOnPostgresWithThePlugin()
    {
        var withPlugin = LibraryProbe.Read(
            CompilationOf(
                NpgsqlReferences.Npgsql10,
                LibraryReferences.NetTopologySuite("2.6.0"),
                LibraryReferences.NpgsqlNetTopologySuite("10.0.3"),
                LibraryReferences.SqlServerBytes("2.1.0")
            )
        );
        var withoutPlugin = LibraryProbe.Read(
            CompilationOf(LibraryReferences.NetTopologySuite("2.6.0"), LibraryReferences.SqlServerBytes("2.1.0"))
        );

        withPlugin.DefaultOf(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql).ShouldBe(LibraryState.On);
        withPlugin.DefaultOf(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer).ShouldBe(LibraryState.Off);
        withoutPlugin.DefaultOf(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql).ShouldBe(LibraryState.Off);
        withoutPlugin.DefaultOf(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer).ShouldBe(LibraryState.Off);
    }

    [Fact]
    public void MissingAssembliesOf_NetTopologySuite_NamesTheEnginesPackage()
    {
        LibraryProbe
            .MissingAssembliesOf(SqlLibrary.NetTopologySuite, DatabaseEngine.PostgreSql)
            .ShouldBe("Npgsql.NetTopologySuite");
        LibraryProbe
            .MissingAssembliesOf(SqlLibrary.NetTopologySuite, DatabaseEngine.SqlServer)
            .ShouldBe("NetTopologySuite.IO.SqlServerBytes, with NetTopologySuite 2.5.0 or later");
    }

    private static CSharpCompilation CompilationOf(params MetadataReference[] references) =>
        GeneratorHarness.CreateCompilation([new SourceFile("/app/Sample.cs", Source)], references: references);
}
