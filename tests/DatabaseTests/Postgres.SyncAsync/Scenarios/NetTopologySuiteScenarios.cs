using System.Collections.Generic;
using NetTopologySuite.Geometries;
using SqlSource.DatabaseTests.Postgres.SyncAsync.NetTopologySuite;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// NetTopologySuite/Queries/NetTopologySuite.sql, on the database gis and the data source with NetTopologySuite: the
// seed's row read whole and by the Point and the Polygon of the type overrides, and every column of spatial_values
// written, with values and with nulls, on a copy of the seed.  The errors are a duplicate id and a polygon in the
// column of points, which the server refuses.
internal static class NetTopologySuiteScenarios
{
    // The id of the row an insert adds, past the seed's one.
    private const int InsertedId = 101;

    private static readonly GeometryFactory Wgs84 = new(new PrecisionModel(), 4326);

    private static readonly GeometryFactory WebMercator = new(new PrecisionModel(), 3857);

    private static readonly Polygon Square = Wgs84.CreatePolygon([
        new Coordinate(0, 0),
        new Coordinate(4, 0),
        new Coordinate(4, 4),
        new Coordinate(0, 4),
        new Coordinate(0, 0),
    ]);

    // A value in every column, with a NULL element in the array and a line of SRID 3857 after it.
    private static readonly InsertSpatialValuesArgs EveryValue = new(
        Id: InsertedId,
        GeometryValue: Wgs84.CreatePoint(new CoordinateZM(1, 2, 3, 4)),
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

    private static readonly InsertSpatialValuesArgs Nulls = new(InsertedId, null, null, null, null, null, null);

    private static readonly Setup Gis = new(Source: "gis");

    // The rows as text, the hex of each geometry with its SRID and its Z and M.
    private static readonly Setup Writes = Gis with
    {
        Writes = true,
        Tables = ["SELECT spatial_values::text FROM spatial_values ORDER BY id"],
    };

    private static readonly Setup WritesClosed = Writes with { Connection = ConnectionStart.Closed };

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(NetTopologySuiteQueriesExtensions);
            var closed = Gis with { Connection = ConnectionStart.Closed };
            var inTransaction = Gis with { Transaction = CallerTransaction.Begun };
            return
            [
                Of(host, "GetSpatialValues", ScenarioTag.Ordinary, "open", Gis, new GetSpatialValuesArgs(1)),
                Of(host, "GetSpatialValues", ScenarioTag.Ordinary, "closed", closed, new GetSpatialValuesArgs(1)),
                Of(
                    host,
                    "GetSpatialValues",
                    ScenarioTag.Ordinary,
                    "in a transaction",
                    inTransaction,
                    new GetSpatialValuesArgs(1)
                ),
                Of(host, "GetSpatialValues", ScenarioTag.NoRow, "no row, closed", closed, new GetSpatialValuesArgs(99)),
                Of(host, "GetPointValue", ScenarioTag.Ordinary, "open", Gis, new GetPointValueArgs(1)),
                Of(host, "GetPointValue", ScenarioTag.Ordinary, "closed", closed, new GetPointValueArgs(1)),
                Of(host, "GetPointValue", ScenarioTag.NoRow, "no row, open", Gis, new GetPointValueArgs(99)),
                Of(host, "GetPolygonValue", ScenarioTag.Ordinary, "open", Gis, new GetPolygonValueArgs(1)),
                Of(host, "GetPolygonValue", ScenarioTag.Ordinary, "closed", closed, new GetPolygonValueArgs(1)),
                Of(
                    host,
                    "GetPolygonValue",
                    ScenarioTag.NoRow,
                    "no row, in a transaction",
                    inTransaction,
                    new GetPolygonValueArgs(99)
                ),
                Of(host, "InsertSpatialValues", ScenarioTag.Ordinary, "every value, open", Writes, EveryValue),
                Of(host, "InsertSpatialValues", ScenarioTag.Ordinary, "every value, closed", WritesClosed, EveryValue),
                Of(host, "InsertSpatialValues", ScenarioTag.Ordinary, "nulls, closed", WritesClosed, Nulls),
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.DatabaseError,
                    "a duplicate id, closed",
                    WritesClosed,
                    Nulls with
                    {
                        Id = 1,
                    }
                ),
                Of(
                    host,
                    "InsertSpatialValues",
                    ScenarioTag.DatabaseError,
                    "a polygon in the column of points, in a transaction",
                    Writes with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    Nulls with
                    {
                        PointValue = Square,
                    }
                ),
            ];
        }
    }
}
