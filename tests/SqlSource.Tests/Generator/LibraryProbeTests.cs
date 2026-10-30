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

        probe.ShouldBe(new LibraryProbe(NodaTime: false, NpgsqlNodaTime: false, NodaTimeVersion: null));
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
                LibraryReferences.Synthetic("3.2.1.0", instant: true, pluginExtensions: false),
                LibraryReferences.Synthetic("3.4.0.0", instant: true, pluginExtensions: false)
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
                LibraryReferences.Synthetic("1.0.0.0", instant: true, pluginExtensions: true, assemblyName: "Merged")
            )
        );

        probe.ShouldBe(new LibraryProbe(NodaTime: true, NpgsqlNodaTime: true, NodaTimeVersion: null));
    }

    [Fact]
    public void DefaultOf_SqlServer_IsOff()
    {
        var probe = new LibraryProbe(NodaTime: true, NpgsqlNodaTime: true, NodaTimeVersion: new Version(3, 3, 0, 0));

        probe.DefaultOf(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBe(LibraryState.Off);
    }

    [Fact]
    public void MissingAssembliesOf_NamesThePluginOnPostgres()
    {
        LibraryProbe.MissingAssembliesOf(SqlLibrary.NodaTime, DatabaseEngine.PostgreSql).ShouldBe("Npgsql.NodaTime");
        LibraryProbe.MissingAssembliesOf(SqlLibrary.NodaTime, DatabaseEngine.SqlServer).ShouldBe("NodaTime");
    }

    private static CSharpCompilation CompilationOf(params MetadataReference[] references) =>
        GeneratorHarness.CreateCompilation([new SourceFile("/app/Sample.cs", Source)], references: references);
}
