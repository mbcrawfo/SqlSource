// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.8.0.  See AGENTS.md beside it.

using System.Linq;
using System.Text.Json.Nodes;
using Microsoft.CodeAnalysis;
using Shouldly;
using SqlSource.Generation.Converters;
using SqlSource.Snapshot;
using SqlSource.Testing.Generator;
using SqlSource.Testing.Snapshot;
using Xunit;

namespace SqlSource.Tests.Generator;

// SQL Server's spatial read path: a geography or geometry read as one of NetTopologySuite's geometries is read through
// the column's stream and bound as SqlBytes, by the shared instances of SqlSource.NetTopologySuiteConversions, which a
// project gets once, in a file of its own, when a method of a CodeGen query calls one.  Each run is under mssql on
// SqlClient 7 with NetTopologySuite 2.6.0 and NetTopologySuite.IO.SqlServerBytes 2.1.0, unless it says otherwise, with
// an entry written for each query's current hash.
public sealed class SpatialConversionTests
{
    // The file of the class, as the tests name it, not taken from the product.
    private const string ClassFile = "SqlSource-NetTopologySuiteConversions.g.cs";

    private const string Conversions = "global::SqlSource.NetTopologySuiteConversions.";

    private const string Geometry = "global::NetTopologySuite.Geometries.Geometry";

    private const string Point = "global::NetTopologySuite.Geometries.Point";

    private const string SqlBytes = "global::System.Data.SqlTypes.SqlBytes";

    // A query that reads place, a nullable geography, as NetTopologySuite's row gives it, and shape, a geometry, as
    // the override makes it a Point, and finds by both.
    private const string FindPlaces =
        "-- type-override: geometry=NetTopologySuite.Geometries.Point\n\n"
        + "-- name: FindPlaces\nSELECT place, shape FROM places WHERE place = @place AND shape = @shape;\n";

    private static MetadataReference[] WithSqlServerBytes =>
        [
            SqlClientReferences.SqlClient7,
            LibraryReferences.NetTopologySuite("2.6.0"),
            LibraryReferences.SqlServerBytes("2.1.0"),
        ];

    // The settings that turn the library on.
    private static string NetTopologySuiteOn =>
        GeneratorHarness.Json(new JsonObject { ["libraryTypes"] = new JsonObject { ["netTopologySuite"] = "on" } });

    // The read of each column, behind IsDBNull for the nullable one, and the binding of each parameter, as the CLR
    // type it is, with SqlBytes.Null for a null.
    [Fact]
    public void Run_SpatialQuery_ReadsAndBindsThroughTheInstances()
    {
        var run = RunPlaces(FindPlaces, WithSqlServerBytes, NetTopologySuiteOn);

        run.ShouldBeClean();
        var methods = run.Sources[MethodRuns.MethodsFile];
        methods.ShouldContain(
            "rows.Add(new global::App.FindPlacesDto(reader.IsDBNull(0) ? default("
                + Geometry
                + "?) : "
                + Conversions
                + "GeographyReader.Read(reader.GetStream(0)), ("
                + Point
                + ")"
                + Conversions
                + "GeometryReader.Read(reader.GetStream(1))));\n"
        );
        methods.ShouldContain(BindingOf("place", "geography", "Place", "GeographyWriter"));
        methods.ShouldContain(BindingOf("shape", "geometry", "Shape", "GeometryWriter"));
    }

    // Two files' queries call the class, which the project gets once.
    [Fact]
    public void Run_SpatialMethod_EmitsTheClassOnce()
    {
        var other = new SqlFile(
            "/app/Repo/Other.sql",
            FindPlaces.Replace("FindPlaces", "FindMore", System.StringComparison.Ordinal)
        );

        var run = RunPlaces(
            FindPlaces,
            WithSqlServerBytes,
            NetTopologySuiteOn,
            [other, Sidecars.Of(other, PlacesEntry(other, "FindMore"))]
        );

        run.ShouldBeClean();
        run.Sources[ClassFile].ShouldBe(NetTopologySuiteConversions.Source);
        run.Sources.Values.Count(source =>
                source.Contains("class NetTopologySuiteConversions", System.StringComparison.Ordinal)
            )
            .ShouldBe(1);
    }

    // The packages referenced and the library on, and no query reads a spatial type, or only a query at Models: no
    // class.
    [Fact]
    public void Run_NoSpatialUse_EmitsNoClass()
    {
        const string Models = """
            using SqlSource;

            namespace App
            {
                [SqlSourceGenerate(Output = GeneratorOutput.Models)]
                public partial class Sample;
            }
            """;
        var file = new SqlFile(GeneratorHarness.UsersPath, "-- name: ListIds\nSELECT id FROM places;\n");
        var entry = MethodRuns.SqlServerEntry(
            file,
            "ListIds",
            [],
            [TestSidecars.Column(0, "id", TestSidecars.SqlServer())]
        );

        var none = MethodRuns.RunSqlServer(file, [entry], references: WithSqlServerBytes, settings: NetTopologySuiteOn);
        var models = RunPlaces(FindPlaces, WithSqlServerBytes, NetTopologySuiteOn, source: Models);

        none.ShouldBeClean();
        none.Sources.Keys.ShouldNotContain(ClassFile);
        models.Sources.Keys.ShouldNotContain(ClassFile);
    }

    // Without NetTopologySuite.IO.SqlServerBytes the library cannot be on, and an override to Geometry is read and
    // bound as the driver reads and binds it, with no class.
    [Fact]
    public void Run_OverrideWithoutSqlServerBytes_IsTheDriversWithNoClass()
    {
        var run = RunPlaces(
            "-- type-override: geography=NetTopologySuite.Geometries.Geometry\n" + FindPlaces,
            [SqlClientReferences.SqlClient7, LibraryReferences.NetTopologySuite("2.6.0")]
        );

        run.ShouldBeClean();
        run.Sources[MethodRuns.MethodsFile].ShouldContain("reader.GetFieldValue<" + Geometry + ">(0)");
        run.Sources.Keys.ShouldNotContain(ClassFile);
    }

    // The methods and the class, read and bound, nullable and not, compile with no error and no warning in what is
    // generated, against SqlClient 6.1 and 7 and the oldest NetTopologySuite and the newest.
    [Theory]
    [InlineData(false, "2.5.0")]
    [InlineData(false, "2.6.0")]
    [InlineData(true, "2.5.0")]
    [InlineData(true, "2.6.0")]
    public void Run_GeneratedCode_CompilesWithoutWarnings(bool sqlClient7, string netTopologySuite)
    {
        var run = RunPlaces(
            FindPlaces,
            [
                sqlClient7 ? SqlClientReferences.SqlClient7 : SqlClientReferences.SqlClient61,
                LibraryReferences.NetTopologySuite(netTopologySuite),
                LibraryReferences.SqlServerBytes("2.1.0"),
            ],
            NetTopologySuiteOn
        );

        run.ShouldBeClean();
        run.Sources.Keys.ShouldContain(ClassFile);
    }

    // The lines that bind the parameter of the name and CLR type from the property through the writer, in the method
    // of an extension class.
    private static string BindingOf(string name, string udtTypeName, string property, string writer)
    {
        const string Indent = "                    ";
        return Indent
            + "parameter.ParameterName = \""
            + name
            + "\";\n"
            + Indent
            + "parameter.SqlDbType = global::System.Data.SqlDbType.Udt;\n"
            + Indent
            + "parameter.UdtTypeName = \""
            + udtTypeName
            + "\";\n"
            + Indent
            + "parameter.Value = parameters."
            + property
            + " is { } value ? (object)new "
            + SqlBytes
            + "("
            + Conversions
            + writer
            + ".Write(value)) : "
            + SqlBytes
            + ".Null;\n"
            + Indent
            + "command.Parameters.Add(parameter);\n";
    }

    // The entry of FindPlaces, or the query of the name, in the file: place a nullable geography and shape a not-null
    // geometry, as parameters and columns.
    private static SidecarEntry PlacesEntry(SqlFile file, string name = "FindPlaces") =>
        MethodRuns.SqlServerEntry(
            file,
            name,
            [
                TestSidecars.Parameter(0, "place", SqlServerClr("geography"), true),
                TestSidecars.Parameter(0, "shape", SqlServerClr("geometry"), false),
            ],
            [
                TestSidecars.Column(0, "place", SqlServerClr("geography"), nullable: true),
                TestSidecars.Column(0, "shape", SqlServerClr("geometry"), nullable: false),
            ]
        );

    // The JSON opt-in on a geography, which neither holds JSON nor is read as a string, is SQLSRC283 at the query, in
    // place of the spatial read path, and the query gets no method, whether a column's marker or a parameter's says it.
    [Theory]
    [InlineData(
        "-- column: place -> json NetTopologySuite.Geometries.Geometry",
        "column",
        "a -- column: marker of the query"
    )]
    [InlineData(
        "-- param: @place -> json NetTopologySuite.Geometries.Geometry",
        "parameter",
        "the -- param: marker of the parameter"
    )]
    public void Run_JsonOptInOnGeography_IsSQLSRC283(string marker, string subject, string place)
    {
        var text =
            "-- name: FindPlaces\n"
            + marker
            + "\nSELECT place, shape FROM places WHERE place = @place AND shape = @shape;\n";

        var run = RunPlaces(text, WithSqlServerBytes, NetTopologySuiteOn);

        run.Problems()
            .ShouldBe([
                "SQLSRC283 "
                    + GeneratorHarness.UsersPath
                    + "(1,10)-(1,20): The "
                    + subject
                    + " 'place' is mapped to json 'NetTopologySuite.Geometries.Geometry' in "
                    + place
                    + ", but the database type 'geography' is not read as a string.  Remove json, or use it only to "
                    + "map a column or parameter of a string or JSON type to a class of this project.",
            ]);
        run.Sources.Keys.ShouldNotContain(MethodRuns.MethodsFile);
        run.Sources.Keys.ShouldNotContain(ClassFile);
    }

    // A CLR type of sys, as the tool describes one.
    private static SqlServerType SqlServerClr(string name) =>
        TestSidecars.SqlServer(
            name,
            maxLength: -1,
            precision: 0,
            userType: new SqlServerUserType("sys", name, "Microsoft.SqlServer.Types, Version=11.0.0.0")
        );

    // FindPlaces of the text at CodeGen under mssql, with these references, settings, other files and source.
    private static GeneratorRun RunPlaces(
        string text,
        MetadataReference[] references,
        string? settings = null,
        SqlFile[]? otherFiles = null,
        string source = MethodRuns.Source
    )
    {
        var file = new SqlFile(GeneratorHarness.UsersPath, text);
        return MethodRuns.RunSqlServer(file, [PlacesEntry(file)], source, references, settings, otherFiles);
    }
}
