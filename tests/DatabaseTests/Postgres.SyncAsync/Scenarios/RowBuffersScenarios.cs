using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/RowBuffers.sql: each collection type, with the array pool and without, read with a few numbers, none, and
// more than the buffer's first array holds.
internal static class RowBuffersScenarios
{
    private static readonly (string Method, Func<int, object> Args)[] Methods =
    [
        ("NumbersAsArray", static count => new NumbersAsArrayArgs(count)),
        ("NumbersAsIEnumerable", static count => new NumbersAsIEnumerableArgs(count)),
        ("NumbersAsIReadOnlyCollection", static count => new NumbersAsIReadOnlyCollectionArgs(count)),
        ("NumbersAsIReadOnlyList", static count => new NumbersAsIReadOnlyListArgs(count)),
        ("NumbersAsList", static count => new NumbersAsListArgs(count)),
        ("NumbersAsIList", static count => new NumbersAsIListArgs(count)),
        ("NumbersAsICollection", static count => new NumbersAsICollectionArgs(count)),
        ("NumbersAsImmutableArray", static count => new NumbersAsImmutableArrayArgs(count)),
        ("NumbersNoPoolAsArray", static count => new NumbersNoPoolAsArrayArgs(count)),
        ("NumbersNoPoolAsIEnumerable", static count => new NumbersNoPoolAsIEnumerableArgs(count)),
        ("NumbersNoPoolAsIReadOnlyCollection", static count => new NumbersNoPoolAsIReadOnlyCollectionArgs(count)),
        ("NumbersNoPoolAsIReadOnlyList", static count => new NumbersNoPoolAsIReadOnlyListArgs(count)),
        ("NumbersNoPoolAsList", static count => new NumbersNoPoolAsListArgs(count)),
        ("NumbersNoPoolAsIList", static count => new NumbersNoPoolAsIListArgs(count)),
        ("NumbersNoPoolAsICollection", static count => new NumbersNoPoolAsICollectionArgs(count)),
        ("NumbersNoPoolAsImmutableArray", static count => new NumbersNoPoolAsImmutableArrayArgs(count)),
    ];

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(RowBufferQueriesExtensions);
            return
            [
                .. Methods.SelectMany(method =>
                    OpenAndClosed(host, method.Method, method.Args(3))
                        .Append(Of(host, method.Method, ScenarioTag.Other, "no rows, open", Open, method.Args(0)))
                        .Append(
                            Of(host, method.Method, ScenarioTag.Other, "1000 rows, closed", Closed, method.Args(1000))
                        )
                ),
                Of(
                    host,
                    "NumbersAsList",
                    ScenarioTag.Ordinary,
                    "in a transaction",
                    InTransaction,
                    new NumbersAsListArgs(3)
                ),
            ];
        }
    }
}
