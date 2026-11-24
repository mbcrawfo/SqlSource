using System.Collections.Generic;
using NpgsqlTypes;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Ranges.sql, on the data source with EnableUnmappedTypes(): a range of a user's own sent, and the seed's
// ranges and multiranges read back, a NULL multirange among them.
internal static class RangesScenarios
{
    private static readonly Setup Unmapped = new(Source: "unmapped");

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(RangeQueriesExtensions);
            var every = new ListReadingsOverlappingArgs(new NpgsqlRange<double>(0, 100));
            return
            [
                Of(host, "ListReadingsOverlapping", ScenarioTag.Ordinary, "open", Unmapped, every),
                Of(
                    host,
                    "ListReadingsOverlapping",
                    ScenarioTag.Ordinary,
                    "closed",
                    Unmapped with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    every
                ),
                Of(
                    host,
                    "ListReadingsOverlapping",
                    ScenarioTag.Other,
                    "no reading, in a transaction",
                    Unmapped with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new ListReadingsOverlappingArgs(new NpgsqlRange<double>(1000, 2000))
                ),
            ];
        }
    }
}
