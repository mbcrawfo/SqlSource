using System.Collections.Generic;
using Microsoft.SqlServer.Types;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/ClrTypes.sql: a hierarchyid, a geography and a geometry written and read back, and nulls of each, and the
// descendants of a node, some and none.  Their rows are a table variable's, so nothing is written to the database.
internal static class ClrTypesScenarios
{
    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(ClrTypeQueriesExtensions);
            return
            [
                .. OpenAndClosed(
                    host,
                    "WriteClrTypes",
                    new WriteClrTypesArgs(
                        SqlHierarchyId.Parse("/1/2/"),
                        SqlGeography.Point(47.6, -122.3, 4326),
                        SqlGeometry.Point(1, 2, 0)
                    )
                ),
                Of(
                    host,
                    "WriteClrTypes",
                    ScenarioTag.Other,
                    "nulls, in a transaction",
                    InTransaction,
                    new WriteClrTypesArgs(null, null, null)
                ),
                .. OpenAndClosed(host, "ListDescendants", new ListDescendantsArgs(SqlHierarchyId.Parse("/1/"))),
                Of(
                    host,
                    "ListDescendants",
                    ScenarioTag.Other,
                    "no node, closed",
                    Closed,
                    new ListDescendantsArgs(SqlHierarchyId.Parse("/3/"))
                ),
            ];
        }
    }
}
