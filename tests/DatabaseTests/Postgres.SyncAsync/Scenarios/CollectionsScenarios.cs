using System.Collections.Generic;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Collections.sql: one query by each collection type.
internal static class CollectionsScenarios
{
    private static readonly string[] Methods =
    [
        "ListAsArray",
        "ListAsIEnumerable",
        "ListAsIReadOnlyCollection",
        "ListAsIReadOnlyList",
        "ListAsICollection",
        "ListAsIList",
        "ListAsList",
        "ListAsImmutableArray",
        "ListAsImmutableList",
        "ListAsIImmutableList",
    ];

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(CollectionQueriesExtensions);
            return
            [
                .. Methods.SelectMany(method => OpenAndClosed(host, method)),
                Of(host, "ListAsImmutableArray", ScenarioTag.Ordinary, "in a transaction", InTransaction),
            ];
        }
    }
}
