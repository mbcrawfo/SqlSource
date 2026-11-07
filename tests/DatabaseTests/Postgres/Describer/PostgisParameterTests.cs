using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// A -- param: type with a modifier of words, as a column definition writes PostGIS's, is sent to the server, which
// checks the words and gives the parameter no modifier.
[Collection(UsesPostgis.Name)]
public sealed class PostgisParameterTests(PostgisDatabases postgis)
{
    private const string Query = "SELECT id, @place, @shapes FROM spatial_values WHERE point_value = @point";

    private string Connection => postgis.ConnectionStringOf(PostgisDatabases.GisDatabase);

    private static readonly PostgresType Geometry = TestSidecars.Postgres(
        "public.geometry",
        SidecarValues.Kind.Base,
        "public",
        "geometry"
    );

    private static readonly PostgresType Geography = TestSidecars.Postgres(
        "public.geography",
        SidecarValues.Kind.Base,
        "public",
        "geography"
    );

    private static readonly PostgresType GeometryArray = TestSidecars.Postgres(
        "public.geometry[]",
        SidecarValues.Kind.Array,
        "public",
        internalName: "_geometry",
        element: Geometry
    );

    private static DescribedParameter Declared(string name, PostgresType type) =>
        new(name, type with { BindName = type.Name }, SidecarValues.TypeSource.Declared);

    [Fact]
    public async Task DescribeAsync_DeclaredGeometryWithAModifierOfWords_IsTheTypeWithoutIt()
    {
        var described = await Sessions.ShouldDescribeAsync(
            Connection,
            DescribeRequests.Of(
                Query,
                ("point", "geometry(Point,4326)"),
                ("place", "geography(PointZM, 4326)"),
                ("shapes", "public.geometry(Polygon,4326)[]")
            )
        );

        described.Parameters.ShouldBe([
            Declared("place", Geography),
            Declared("shapes", GeometryArray),
            Declared("point", Geometry),
        ]);
    }

    [Fact]
    public async Task DescribeAsync_DeclaredGeometryWithAWordPostgisDoesNotKnow_IsSqlsrc228WithTheServersLine()
    {
        var described = await Sessions.DescribeAsync(
            Connection,
            DescribeRequests.Of(
                Query,
                ("point", "geometry(Nonsense,4326)"),
                ("place", "geography"),
                ("shapes", "geometry[]")
            )
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe([
            "geometry(Nonsense,4326)",
            "@point",
            DescribeRequests.Query,
            DescribeFailures.NotATypeName,
        ]);
        failure.ServerLines[0].ShouldBe("22023: Invalid geometry type modifier: nonsense");
    }
}
