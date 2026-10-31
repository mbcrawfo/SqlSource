using System;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using NetTopologySuite.Geometries;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The queries of the linked EndToEnd/NetTopologySuite/Queries/ on SqlClient 6.1, the oldest driver that is supported,
// NetTopologySuite 2.5.0 and NetTopologySuite.IO.SqlServerBytes 2.1.0, the oldest versions that SqlSource supports:
// every column of spatial_values written by InsertSpatialValues or InsertSpatialOverrides and read back by
// GetSpatialValues or GetSpatialOverrides, in a transaction that is rolled back, and a read through a connection that
// wraps SqlClient's, with a copy of the rows and tests of the end-to-end project's NetTopologySuiteQueryTests, which
// proves the same on SqlClient 7 and NetTopologySuite 2.6.0.
[Collection(UsesApp.Name)]
public sealed class NetTopologySuiteQueryTests(AppContainer app)
{
    // The error of a value that is not a valid instance of its type, which the server reports for the parameter.
    private const int NotAValidInstance = 8023;

    private static readonly GeometryFactory Wgs84 = new(new PrecisionModel(), 4326);
    private static readonly GeometryFactory Planar = new(new PrecisionModel(), 0);
    private static readonly GeometryFactory WebMercator = new(new PrecisionModel(), 3857);

    // A geography of SRID 4326 and a geometry of SRID 0, NetTopologySuite's default, and of SRID 3857.
    private static readonly InsertSpatialValuesArgs[] Rows =
    [
        new(Id: 301, GeographyValue: Wgs84.CreatePoint(new Coordinate(-71.06, 42.36)), GeometryValue: SquareOf(Planar)),
        new(
            Id: 302,
            GeographyValue: SquareOf(Wgs84),
            GeometryValue: WebMercator.CreateLineString([new Coordinate(0, 0), new Coordinate(1_000, 2_000)])
        ),
    ];

    [Fact]
    public async Task InsertSpatialValues_GeographyAndGeometry_RoundTripWithTheirSrids()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(app.ConnectionString);

        await RolledBackAsync(
            connection,
            async transaction =>
            {
                foreach (var row in Rows)
                {
                    var inserted = await connection.InsertSpatialValues(row, transaction, cancellationToken: token);
                    var read = await connection.GetSpatialValues(
                        new GetSpatialValuesArgs(row.Id),
                        transaction,
                        cancellationToken: token
                    );

                    inserted.ShouldBe(1);
                    ModelReflection.ShouldHoldTheValuesOf(read, row);
                    read.GeographyValue!.SRID.ShouldBe(row.GeographyValue!.SRID);
                    read.GeometryValue!.SRID.ShouldBe(row.GeometryValue!.SRID);
                }
            },
            token
        );
    }

    // The file header's type overrides read point_value as a Point and polygon_value as a Polygon.
    [Fact]
    public async Task InsertSpatialOverrides_PointAndPolygon_ReadAsThoseTypes()
    {
        var token = TestContext.Current.CancellationToken;
        var row = new InsertSpatialOverridesArgs(
            Id: 303,
            PointValue: Wgs84.CreatePoint(new Coordinate(10, 20)),
            PolygonValue: SquareOf(WebMercator)
        );
        var inserted = 0;
        GetSpatialOverridesDto? read = null;
        await using var connection = new SqlConnection(app.ConnectionString);

        await RolledBackAsync(
            connection,
            async transaction =>
            {
                inserted = await connection.InsertSpatialOverrides(row, transaction, cancellationToken: token);
                read = await connection.GetSpatialOverrides(
                    new GetSpatialOverridesArgs(row.Id),
                    transaction,
                    cancellationToken: token
                );
            },
            token
        );

        inserted.ShouldBe(1);
        ModelReflection.ShouldHoldTheValuesOf(read.ShouldNotBeNull(), row);
        read.PointValue.ShouldBeOfType<Point>().SRID.ShouldBe(4326);
        read.PolygonValue.ShouldBeOfType<Polygon>().SRID.ShouldBe(3857);
    }

    [Fact]
    public async Task InsertSpatialValuesAndOverrides_AllNull_ReadNull()
    {
        var token = TestContext.Current.CancellationToken;
        GetSpatialValuesDto? values = null;
        GetSpatialOverridesDto? overrides = null;
        await using var connection = new SqlConnection(app.ConnectionString);

        await RolledBackAsync(
            connection,
            async transaction =>
            {
                _ = await connection.InsertSpatialValues(
                    new InsertSpatialValuesArgs(304, null, null),
                    transaction,
                    cancellationToken: token
                );
                _ = await connection.InsertSpatialOverrides(
                    new InsertSpatialOverridesArgs(305, null, null),
                    transaction,
                    cancellationToken: token
                );
                values = await connection.GetSpatialValues(
                    new GetSpatialValuesArgs(304),
                    transaction,
                    cancellationToken: token
                );
                overrides = await connection.GetSpatialOverrides(
                    new GetSpatialOverridesArgs(305),
                    transaction,
                    cancellationToken: token
                );
            },
            token
        );

        values.ShouldBe(new GetSpatialValuesDto(304, null, null));
        overrides.ShouldBe(new GetSpatialOverridesDto(305, null, null));
    }

    // A point of four dimensions written to geography and to geometry comes back with its Z and M, which the reader and
    // the writer keep by default.
    [Fact]
    public async Task InsertSpatialValues_PointZM_KeepsZAndM()
    {
        var token = TestContext.Current.CancellationToken;
        var row = new InsertSpatialValuesArgs(
            Id: 306,
            GeographyValue: Wgs84.CreatePoint(new CoordinateZM(1, 2, 3, 4)),
            GeometryValue: Planar.CreatePoint(new CoordinateZM(5, 6, 7, 8))
        );
        GetSpatialValuesDto? read = null;
        await using var connection = new SqlConnection(app.ConnectionString);

        await RolledBackAsync(
            connection,
            async transaction =>
            {
                _ = await connection.InsertSpatialValues(row, transaction, cancellationToken: token);
                read = await connection.GetSpatialValues(
                    new GetSpatialValuesArgs(row.Id),
                    transaction,
                    cancellationToken: token
                );
            },
            token
        );

        OrdinatesOf(read.ShouldNotBeNull().GeographyValue.ShouldBeOfType<Point>()).ShouldBe((1, 2, 3, 4));
        OrdinatesOf(read.GeometryValue.ShouldBeOfType<Point>()).ShouldBe((5, 6, 7, 8));
    }

    // NetTopologySuite's writer refuses a geography polygon whose shell is clockwise, before the command is sent.
    [Fact]
    public async Task InsertSpatialValues_ClockwiseGeographyShell_ThrowsArgumentException()
    {
        var token = TestContext.Current.CancellationToken;
        var clockwise = (Polygon)SquareOf(Wgs84).Reverse();
        await using var connection = new SqlConnection(app.ConnectionString);

        var thrown = await Should.ThrowAsync<ArgumentException>(() =>
            RolledBackAsync(
                connection,
                transaction =>
                    connection.InsertSpatialValues(
                        new InsertSpatialValuesArgs(307, clockwise, null),
                        transaction,
                        cancellationToken: token
                    ),
                token
            )
        );

        thrown.Message.ShouldContain("counter-clockwise");
    }

    // A geography of SRID 0, NetTopologySuite's default, is refused by the server, which geometry is not.
    [Fact]
    public async Task InsertSpatialValues_GeographyOfSrid0_IsRefusedByTheServer()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(app.ConnectionString);

        var thrown = await Should.ThrowAsync<SqlException>(() =>
            RolledBackAsync(
                connection,
                transaction =>
                    connection.InsertSpatialValues(
                        new InsertSpatialValuesArgs(308, Planar.CreatePoint(new Coordinate(1, 2)), null),
                        transaction,
                        cancellationToken: token
                    ),
                token
            )
        );

        thrown.Number.ShouldBe(NotAValidInstance, thrown.Message);
        thrown.Message.ShouldContain("geography");
    }

    // A connection that wraps SqlClient's gives the method a reader of its own, whose GetStream is DbDataReader's: the
    // read of a query with no parameter, which binds nothing, takes the geography through it.  The rows are written on
    // the wrapped connection.
    [Fact]
    public async Task ListGeographyValues_ThroughAWrappingConnection_ReadsTheGeography()
    {
        var token = TestContext.Current.CancellationToken;
        var point = Wgs84.CreatePoint(new Coordinate(-71.06, 42.36));
        await using var inner = new SqlConnection(app.ConnectionString);
        await using var connection = new WrappingConnection(inner);
        await connection.OpenAsync(token);
        await using var transaction = (WrappingTransaction)await connection.BeginTransactionAsync(token);
        _ = await inner.InsertSpatialValues(
            new InsertSpatialValuesArgs(309, point, null),
            transaction.Inner,
            cancellationToken: token
        );
        _ = await inner.InsertSpatialValues(
            new InsertSpatialValuesArgs(310, null, null),
            transaction.Inner,
            cancellationToken: token
        );

        var read = await connection.ListGeographyValues(transaction, cancellationToken: token);

        read.ShouldBe([new ListGeographyValuesDto(309, point), new ListGeographyValuesDto(310, null)]);
        read[0].GeographyValue!.SRID.ShouldBe(4326);
    }

    [Fact]
    public void MethodTypes_AreGeometries()
    {
        (string Name, Type Type, bool Nullable)[] values =
        [
            ("Id", typeof(int), false),
            ("GeographyValue", typeof(Geometry), true),
            ("GeometryValue", typeof(Geometry), true),
        ];
        (string Name, Type Type, bool Nullable)[] overrides =
        [
            ("Id", typeof(int), false),
            ("PointValue", typeof(Point), true),
            ("PolygonValue", typeof(Polygon), true),
        ];

        ModelReflection.TypedPropertiesOf(typeof(GetSpatialValuesDto)).ShouldBe(values);
        ModelReflection.TypedPropertiesOf(typeof(InsertSpatialValuesArgs)).ShouldBe(values);
        ModelReflection.TypedPropertiesOf(typeof(GetSpatialOverridesDto)).ShouldBe(overrides);
        ModelReflection.TypedPropertiesOf(typeof(InsertSpatialOverridesArgs)).ShouldBe(overrides);
    }

    // The coordinate of the point in its four ordinates, which NetTopologySuite's Equals does not compare all of.
    private static (double X, double Y, double Z, double M) OrdinatesOf(Point point) =>
        (point.Coordinate.X, point.Coordinate.Y, point.Coordinate.Z, point.Coordinate.M);

    // A square whose shell is counter-clockwise, as geography needs it, in the factory's SRID.
    private static Polygon SquareOf(GeometryFactory factory) =>
        factory.CreatePolygon([
            new Coordinate(0, 0),
            new Coordinate(4, 0),
            new Coordinate(4, 4),
            new Coordinate(0, 4),
            new Coordinate(0, 0),
        ]);

    // Opens the connection and runs the write in a transaction that is rolled back when it is disposed, given to every
    // call, since SqlClient refuses a command on a connection whose transaction it is not given.
    private static async Task RolledBackAsync(
        SqlConnection connection,
        Func<DbTransaction, Task> write,
        CancellationToken cancellationToken
    )
    {
        await connection.OpenAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await write(transaction);
    }
}
