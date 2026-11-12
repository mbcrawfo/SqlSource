using System.Collections.Generic;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Shapes;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Shared;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Shapes.sql: the shapes of models, with methods on the type.
internal static class ShapesScenarios
{
    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(ShapeQueries);
            return
            [
                .. OpenAndClosed(host, "SearchAccounts", new SearchAccountsFilter { AccountId = 3, Name = "a%" }),
                Of(
                    host,
                    "SearchAccounts",
                    ScenarioTag.Other,
                    "no account, closed",
                    Closed,
                    new SearchAccountsFilter { AccountId = 99, Name = "z%" }
                ),
                .. OpenAndClosed(host, "ListAccountColumns"),
                Of(host, "ListAccountColumns", ScenarioTag.Ordinary, "in a transaction", InTransaction),
                .. OpenAndClosed(host, "GetHeadline", new GetHeadlineFilter { AccountId = 1 }),
                .. OpenAndClosed(host, "LookUpAccount", new AccountLookup { Email = "carol@example.com" }),
            ];
        }
    }
}
