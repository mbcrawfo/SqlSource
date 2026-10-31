using System;
using System.Linq;
using System.Threading.Tasks;
using NetTopologySuite.Geometries;
using Npgsql;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The queries of the linked NetTopologySuite.sql on Npgsql 8, the oldest driver that is supported, through
// Npgsql.NetTopologySuite 8 and NetTopologySuite 2.5.0: every column of spatial_values written by InsertSpatialValues
// and read back by GetSpatialValues, in a transaction that is rolled back, and a Point and a Polygon read by a type
// override, with a copy of the rows and tests of the end-to-end project's NetTopologySuiteQueryTests, which proves the
// same on Npgsql 10.
[Collection(UsesGis.Name)]
public sealed class NetTopologySuiteQueryTests(GisContainer gis)
{
    private static readonly GeometryFactory Wgs84 = new(new PrecisionModel(), 4326);
    private static readonly GeometryFactory WebMercator = new(new PrecisionModel(), 3857);

    private static readonly Polygon Square = Wgs84.CreatePolygon([
        new Coordinate(0, 0),
        new Coordinate(4, 0),
        new Coordinate(4, 4),
        new Coordinate(0, 4),
        new Coordinate(0, 0),
    ]);

    // A value in every column, each of SRID 4326, with a NULL element in the array and a line of SRID 3857 after it.
    private static readonly InsertSpatialValuesArgs EveryValue = new(
        Id: 101,
        GeometryValue: Square,
        GeographyValue: Wgs84.CreatePoint(new Coordinate(-71.06, 42.36)),
        PointValue: Wgs84.CreatePoint(new Coordinate(10, 20)),
        PolygonValue: Square,
        GeometryArrayValue:
        [
            Wgs84.CreatePoint(new Coordinate(1, 1)),
            null!,
            WebMercator.CreateLineString([new Coordinate(0, 0), new Coordinate(1, 1)]),
        ],
        ShapeValue: Wgs84.CreateLineString([new Coordinate(0, 0), new Coordinate(2, 2)])
    );

    // A geometry of another SRID, 3857, which geometry keeps as it is.
    private static readonly InsertSpatialValuesArgs OtherSrid = Nulls(102) with
    {
        GeometryValue = WebMercator.CreatePoint(new Coordinate(-7_910_000, 5_215_000)),
        ShapeValue = WebMercator.CreatePoint(new Coordinate(1, 2)),
    };

    [Fact]
    public async Task EveryRow_RoundTrips()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NetTopologySuiteSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        foreach (var row in new[] { EveryValue, OtherSrid })
        {
            var inserted = await connection.InsertSpatialValues(row, transaction, cancellationToken: token);
            var read = await connection.GetSpatialValues(
                new GetSpatialValuesArgs(row.Id),
                transaction,
                cancellationToken: token
            );

            inserted.ShouldBe(1);
            ModelReflection.ShouldHoldTheValuesOf(read, row);
            SridsOf(read).ShouldBe(SridsOf(row));
        }

        await transaction.RollbackAsync(token);
    }

    // The row of the seed, which the server wrote from its text: every column read, with the point of four dimensions
    // keeping its Z and M and the array its NULL element.
    [Fact]
    public async Task SeededRow_ReadsEveryColumn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NetTopologySuiteSource();
        await using var connection = await source.OpenConnectionAsync(token);

        var read = await connection.GetSpatialValues(new GetSpatialValuesArgs(1), cancellationToken: token);

        var point = read.GeometryValue.ShouldBeOfType<Point>();
        point.SRID.ShouldBe(3857);
        ZAndMOf(point).ShouldBe((1, 2, 3, 4));
        read.GeographyValue.ShouldBe(Wgs84.CreatePoint(new Coordinate(-71.06, 42.36)));
        read.GeographyValue!.SRID.ShouldBe(4326);
        read.PointValue.ShouldBe(EveryValue.PointValue);
        read.PolygonValue.ShouldBe(Square);
        read.GeometryArrayValue.ShouldNotBeNull().Cast<object?>().ShouldBe(EveryValue.GeometryArrayValue!);
        read.ShapeValue.ShouldBe(EveryValue.ShapeValue);
    }

    // A point of four dimensions written to geometry and to geography comes back with its Z and M.
    [Fact]
    public async Task PointZM_KeepsZAndM()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NetTopologySuiteSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var row = Nulls(103) with
        {
            GeometryValue = Wgs84.CreatePoint(new CoordinateZM(1, 2, 3, 4)),
            GeographyValue = Wgs84.CreatePoint(new CoordinateZM(5, 6, 7, 8)),
        };

        _ = await connection.InsertSpatialValues(row, transaction, cancellationToken: token);
        var read = await connection.GetSpatialValues(
            new GetSpatialValuesArgs(row.Id),
            transaction,
            cancellationToken: token
        );
        await transaction.RollbackAsync(token);

        ZAndMOf(read.GeometryValue.ShouldBeOfType<Point>()).ShouldBe((1, 2, 3, 4));
        ZAndMOf(read.GeographyValue.ShouldBeOfType<Point>()).ShouldBe((5, 6, 7, 8));
    }

    // GetPointValue and GetPolygonValue read the column as the type their query's type override names, which the
    // plugin reads.
    [Fact]
    public async Task PointAndPolygon_ByTypeOverride_ReadAsThoseTypes()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NetTopologySuiteSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var row = EveryValue with { Id = 104 };

        _ = await connection.InsertSpatialValues(row, transaction, cancellationToken: token);
        var point = await connection.GetPointValue(
            new GetPointValueArgs(row.Id),
            transaction,
            cancellationToken: token
        );
        var polygon = await connection.GetPolygonValue(
            new GetPolygonValueArgs(row.Id),
            transaction,
            cancellationToken: token
        );
        await transaction.RollbackAsync(token);

        point.PointValue.ShouldBe(row.PointValue);
        point.PointValue!.SRID.ShouldBe(4326);
        polygon.PolygonValue.ShouldBe(Square);
        polygon.PolygonValue!.SRID.ShouldBe(4326);
    }

    [Fact]
    public async Task Nulls_RoundTrip()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NetTopologySuiteSource();
        await using var connection = await source.OpenConnectionAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        var row = Nulls(105);

        var inserted = await connection.InsertSpatialValues(row, transaction, cancellationToken: token);
        var read = await connection.GetSpatialValues(
            new GetSpatialValuesArgs(row.Id),
            transaction,
            cancellationToken: token
        );
        var point = await connection.GetPointValue(
            new GetPointValueArgs(row.Id),
            transaction,
            cancellationToken: token
        );
        var polygon = await connection.GetPolygonValue(
            new GetPolygonValueArgs(row.Id),
            transaction,
            cancellationToken: token
        );
        await transaction.RollbackAsync(token);

        inserted.ShouldBe(1);
        ModelReflection.ShouldHoldTheValuesOf(read, row);
        foreach (var property in ModelReflection.PropertiesOf(read.GetType()))
        {
            if (property.Name != nameof(read.Id))
            {
                property.GetValue(read).ShouldBeNull(property.Name);
            }
        }

        point.PointValue.ShouldBeNull();
        polygon.PolygonValue.ShouldBeNull();
    }

    [Fact]
    public void MethodTypes_AreGeometries()
    {
        (string Name, Type Type, bool Nullable)[] properties =
        [
            ("Id", typeof(int), false),
            ("GeometryValue", typeof(Geometry), true),
            ("GeographyValue", typeof(Geometry), true),
            ("PointValue", typeof(Geometry), true),
            ("PolygonValue", typeof(Geometry), true),
            ("GeometryArrayValue", typeof(Geometry[]), true),
            ("ShapeValue", typeof(Geometry), true),
        ];

        ModelReflection.TypedPropertiesOf(typeof(SpatialValuesDto)).ShouldBe(properties);
        ModelReflection.TypedPropertiesOf(typeof(InsertSpatialValuesArgs)).ShouldBe(properties);
        ModelReflection.TypedPropertiesOf(typeof(GetPointValueDto)).ShouldBe([("PointValue", typeof(Point), true)]);
        ModelReflection
            .TypedPropertiesOf(typeof(GetPolygonValueDto))
            .ShouldBe([("PolygonValue", typeof(Polygon), true)]);
    }

    // The SRID of each geometry of a row or of its arguments, by the name of its property and the index of an array's
    // element, -1 for a value of its own, since Equals does not compare it.  A NULL element has none.
    private static (string Property, int Index, int? Srid)[] SridsOf(object model) =>
        [
            .. ModelReflection
                .PropertiesOf(model.GetType())
                .SelectMany(property =>
                    property.GetValue(model) switch
                    {
                        Geometry geometry => [(property.Name, -1, geometry.SRID)],
                        Geometry[] array => array.Select((element, index) => (property.Name, index, element?.SRID)),
                        _ => [],
                    }
                ),
        ];

    // The coordinate of the point in its four ordinates, which NetTopologySuite's AsText() does not show all of.
    private static (double X, double Y, double Z, double M) ZAndMOf(Point point) =>
        (point.Coordinate.X, point.Coordinate.Y, point.Coordinate.Z, point.Coordinate.M);

    // A data source of "gis" with the plugin, as an application builds one for these methods.
    private NpgsqlDataSource NetTopologySuiteSource()
    {
        var builder = new NpgsqlDataSourceBuilder(gis.ConnectionString);
        _ = builder.UseNetTopologySuite();
        return builder.Build();
    }

    // A row with NULL in every column but id.
    private static InsertSpatialValuesArgs Nulls(int id) =>
        new(
            Id: id,
            GeometryValue: null,
            GeographyValue: null,
            PointValue: null,
            PolygonValue: null,
            GeometryArrayValue: null,
            ShapeValue: null
        );
}
