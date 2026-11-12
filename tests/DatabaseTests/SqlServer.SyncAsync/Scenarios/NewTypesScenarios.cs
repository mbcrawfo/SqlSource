using System.Collections.Generic;
using Microsoft.Data.SqlTypes;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/NewTypes.sql, on SQL Server 2025's json and vector: the seed's two rows, the insert of a row, on a copy of
// the seed, with values, with nulls and with a taken id, a NULL in a vector the marker says is never null, an id with
// no row, and a probe of the wrong dimension, which the server refuses.
internal static class NewTypesScenarios
{
    // The rows of new_types, each value as its text, which SqlClient gives json and vector as.
    private const string NewTypes =
        "SELECT id, CAST(json_value AS nvarchar(max)) AS json_value, "
        + "CAST(vector_value AS nvarchar(max)) AS vector_value FROM dbo.new_types ORDER BY id";

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(NewTypesQueriesExtensions);
            var writes = Writes(NewTypes);
            return
            [
                .. OpenAndClosed(host, "ListNewTypes"),
                Of(
                    host,
                    "WriteNewTypes",
                    ScenarioTag.Ordinary,
                    "open",
                    writes,
                    new WriteNewTypesArgs(
                        3, /*lang=json,strict*/
                        """{"name":"Zoë","tags":["a","b"]}""",
                        Vector(1.5f, -2.25f, 0.5f)
                    )
                ),
                Of(
                    host,
                    "WriteNewTypes",
                    ScenarioTag.Ordinary,
                    "nulls, closed",
                    writes with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new WriteNewTypesArgs(3, null, null)
                ),
                Of(
                    host,
                    "WriteNewTypes",
                    ScenarioTag.DatabaseError,
                    "a taken id, in a transaction",
                    writes with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new WriteNewTypesArgs(1, null, null)
                ),
                .. OpenAndClosed(host, "GetNewType", new GetNewTypeArgs(1)),
                Of(host, "GetNewType", ScenarioTag.Other, "a NULL vector, closed", Closed, new GetNewTypeArgs(2)),
                Of(host, "GetNewType", ScenarioTag.NoRow, "no row, open", Open, new GetNewTypeArgs(99)),
                .. OpenAndClosed(host, "MeasureVectorDistance", new MeasureVectorDistanceArgs(Vector(1f, 0f, 0f), 1)),
                Of(
                    host,
                    "MeasureVectorDistance",
                    ScenarioTag.Other,
                    "a null probe, open",
                    Open,
                    new MeasureVectorDistanceArgs(null, 1)
                ),
                Of(
                    host,
                    "MeasureVectorDistance",
                    ScenarioTag.DatabaseError,
                    "a probe of two dimensions, closed",
                    Closed,
                    new MeasureVectorDistanceArgs(Vector(1f, 0f), 1)
                ),
            ];
        }
    }

    private static SqlVector<float> Vector(params float[] values) => new(values);
}
