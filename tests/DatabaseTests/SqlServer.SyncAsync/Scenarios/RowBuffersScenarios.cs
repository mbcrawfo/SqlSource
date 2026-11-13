using System.Collections.Generic;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/RowBuffers.sql: each collection type, with the array pool and without, read with a few numbers, none, and
// more than the buffer's first array holds.
internal static class RowBuffersScenarios
{
    private static readonly string[] Methods =
    [
        "NumbersAsArray",
        "NumbersAsIEnumerable",
        "NumbersAsIReadOnlyCollection",
        "NumbersAsIReadOnlyList",
        "NumbersAsList",
        "NumbersAsIList",
        "NumbersAsICollection",
        "NumbersAsImmutableArray",
        "NumbersNoPoolAsArray",
        "NumbersNoPoolAsIEnumerable",
        "NumbersNoPoolAsIReadOnlyCollection",
        "NumbersNoPoolAsIReadOnlyList",
        "NumbersNoPoolAsList",
        "NumbersNoPoolAsIList",
        "NumbersNoPoolAsICollection",
        "NumbersNoPoolAsImmutableArray",
    ];

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(RowBufferQueriesExtensions);
            return
            [
                .. Methods.SelectMany(method =>
                    OpenAndClosed(host, method, new NumbersCount(3))
                        .Append(Of(host, method, ScenarioTag.Other, "no rows, open", Open, new NumbersCount(0)))
                        .Append(
                            Of(host, method, ScenarioTag.Other, "1000 rows, closed", Closed, new NumbersCount(1000))
                        )
                ),
                Of(host, "NumbersAsList", ScenarioTag.Ordinary, "in a transaction", InTransaction, new NumbersCount(3)),
            ];
        }
    }
}
