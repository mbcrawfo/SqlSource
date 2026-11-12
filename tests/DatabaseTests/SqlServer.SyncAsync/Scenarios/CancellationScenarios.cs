using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Cancellation.sql: the ids read through CancelOnRead, whose converter cancels nothing here, since no scenario
// sets its source, and a synchronous method takes no token.
internal static class CancellationScenarios
{
    public static IEnumerable<Scenario> All =>
        [
            .. OpenAndClosed(typeof(CancellationQueriesExtensions), "ListAccountIdsCancellingOnRead"),
            Of(
                typeof(CancellationQueriesExtensions),
                "ListAccountIdsCancellingOnRead",
                ScenarioTag.Ordinary,
                "in a transaction",
                InTransaction
            ),
        ];
}
