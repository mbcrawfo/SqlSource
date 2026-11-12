using System.Collections.Generic;
using NetTopologySuite.Geometries;
using SqlSource.DatabaseTests.SqlServer.SyncAsync.NetTopologySuite;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// NetTopologySuite/Queries/: every column of spatial_values written, by InsertSpatialValues and InsertSpatialOverrides,
// with values and with nulls, and read back, whole, by the Point and the Polygon of the type overrides and as a list,
// from rows that the scenario writes first, each on a copy of the seed, which holds no row of the table.  The values
// are those of NetTopologySuiteQueryTests.  The errors are a duplicate id, a geography of SRID 0, which the server
// refuses, and a geography polygon whose shell is clockwise, which NetTopologySuite's writer refuses before sending it.
internal static class NetTopologySuiteScenarios
{
    private const int RowId = 301;

    // Two rows, the first with a value in every column, a geometry with its Z and M among them, and the second with
    // none, written by the server from their text.
    private const string InsertRows =
        "INSERT INTO dbo.spatial_values (id, geography_value, geometry_value, point_value, polygon_value) VALUES "
        + "(301, geography::STGeomFromText('POINT(-71.06 42.36)', 4326), "
        + "geometry::STGeomFromText('POINT(5 6 7 8)', 0), geography::Point(20, 10, 4326), "
        + "geometry::STGeomFromText('POLYGON((0 0, 4 0, 4 4, 0 4, 0 0))', 3857)), (302, NULL, NULL, NULL, NULL)";

    private static readonly GeometryFactory Wgs84 = new(new PrecisionModel(), 4326);

    private static readonly GeometryFactory Planar = new(new PrecisionModel(), 0);

    private static readonly GeometryFactory WebMercator = new(new PrecisionModel(), 3857);

    // Each column as the server stores it, with its SRID and its Z and M.
    private static readonly Setup WritesOpen = Writes(
        "SELECT id, CAST(geography_value AS varbinary(max)) AS geography_value, "
            + "CAST(geometry_value AS varbinary(max)) AS geometry_value, "
            + "CAST(point_value AS varbinary(max)) AS point_value, "
            + "CAST(polygon_value AS varbinary(max)) AS polygon_value FROM dbo.spatial_values ORDER BY id"
    );

    private static readonly Setup WritesClosed = WritesOpen with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = WritesOpen with { Transaction = CallerTransaction.Begun };

    private static readonly Setup WithRows = WritesOpen with { Prepare = InsertRows };

    private static readonly InsertSpatialValuesArgs PointAndSquare = new(
        RowId,
        Wgs84.CreatePoint(new Coordinate(-71.06, 42.36)),
        SquareOf(Planar)
    );

    private static readonly InsertSpatialValuesArgs SquareAndLine = new(
        RowId,
        SquareOf(Wgs84),
        WebMercator.CreateLineString([new Coordinate(0, 0), new Coordinate(1_000, 2_000)])
    );

    private static readonly InsertSpatialValuesArgs PointsZM = new(
        RowId,
        Wgs84.CreatePoint(new CoordinateZM(1, 2, 3, 4)),
        Planar.CreatePoint(new CoordinateZM(5, 6, 7, 8))
    );

    private static readonly InsertSpatialOverridesArgs PointAndPolygon = new(
        RowId,
        Wgs84.CreatePoint(new Coordinate(10, 20)),
        SquareOf(WebMercator)
    );

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(NetTopologySuiteQueriesExtensions);
            return
            [
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.Ordinary,
                    "a point and a square, open",
                    WritesOpen,
                    PointAndSquare
                ),
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.Ordinary,
                    "a square and a line, closed",
                    WritesClosed,
                    SquareAndLine
                ),
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.Ordinary,
                    "points with Z and M, in a transaction",
                    WritesInTransaction,
                    PointsZM
                ),
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.Ordinary,
                    "nulls, closed",
                    WritesClosed,
                    new InsertSpatialValuesArgs(RowId, null, null)
                ),
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WithRows with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    PointAndSquare
                ),
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.DatabaseError,
                    "a geography of SRID 0, closed",
                    WritesClosed,
                    new InsertSpatialValuesArgs(RowId, Planar.CreatePoint(new Coordinate(1, 2)), null)
                ),
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.DatabaseError,
                    "a geography of SRID 0, in a transaction",
                    WritesInTransaction,
                    new InsertSpatialValuesArgs(RowId, Planar.CreatePoint(new Coordinate(1, 2)), null)
                ),
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.Other,
                    "a clockwise geography shell, closed",
                    WritesClosed,
                    new InsertSpatialValuesArgs(RowId, (Polygon)SquareOf(Wgs84).Reverse(), null)
                ),
                Of(
                    host,
                    "GetSpatialValues",
                    ScenarioTag.Ordinary,
                    "a row, open",
                    WithRows,
                    new GetSpatialValuesArgs(RowId)
                ),
                Of(
                    host,
                    "GetSpatialValues",
                    ScenarioTag.Ordinary,
                    "a row, closed",
                    WithRows with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new GetSpatialValuesArgs(RowId)
                ),
                Of(
                    host,
                    "GetSpatialValues",
                    ScenarioTag.Ordinary,
                    "a row of nulls, in a transaction",
                    WithRows with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new GetSpatialValuesArgs(RowId + 1)
                ),
                Of(
                    host,
                    "GetSpatialValues",
                    ScenarioTag.NoRow,
                    "no row, closed",
                    Closed,
                    new GetSpatialValuesArgs(RowId)
                ),
                Of(host, "ListGeographyValues", ScenarioTag.Ordinary, "two rows, open", WithRows),
                Of(
                    host,
                    "ListGeographyValues",
                    ScenarioTag.Ordinary,
                    "two rows, in a transaction",
                    WithRows with
                    {
                        Transaction = CallerTransaction.Begun,
                    }
                ),
                Of(host, "ListGeographyValues", ScenarioTag.Other, "no rows, closed", Closed),
                Of(
                    host,
                    "InsertSpatialOverrides",
                    ScenarioTag.Ordinary,
                    "a point and a polygon, open",
                    WritesOpen,
                    PointAndPolygon
                ),
                Of(
                    host,
                    "InsertSpatialOverrides",
                    ScenarioTag.Ordinary,
                    "a point and a polygon, closed",
                    WritesClosed,
                    PointAndPolygon
                ),
                Of(
                    host,
                    "InsertSpatialOverrides",
                    ScenarioTag.Ordinary,
                    "nulls, in a transaction",
                    WritesInTransaction,
                    new InsertSpatialOverridesArgs(RowId, null, null)
                ),
                Of(
                    host,
                    "InsertSpatialOverrides",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WithRows with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    PointAndPolygon
                ),
                Of(
                    host,
                    "GetSpatialOverrides",
                    ScenarioTag.Ordinary,
                    "a row, open",
                    WithRows,
                    new GetSpatialOverridesArgs(RowId)
                ),
                Of(
                    host,
                    "GetSpatialOverrides",
                    ScenarioTag.Ordinary,
                    "a row of nulls, closed",
                    WithRows with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new GetSpatialOverridesArgs(RowId + 1)
                ),
                Of(
                    host,
                    "GetSpatialOverrides",
                    ScenarioTag.NoRow,
                    "no row, in a transaction",
                    InTransaction,
                    new GetSpatialOverridesArgs(RowId)
                ),
            ];
        }
    }

    // A square whose shell is counter-clockwise, as geography needs it, in the factory's SRID.
    private static Polygon SquareOf(GeometryFactory factory) =>
        factory.CreatePolygon([
            new Coordinate(0, 0),
            new Coordinate(4, 0),
            new Coordinate(4, 4),
            new Coordinate(0, 4),
            new Coordinate(0, 0),
        ]);
}
