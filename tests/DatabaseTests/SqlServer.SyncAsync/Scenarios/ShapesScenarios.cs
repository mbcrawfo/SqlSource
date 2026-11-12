using System.Collections.Generic;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Shapes;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Shared;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Shapes.sql: models of other shapes and namespaces, with an input model of settable properties, and methods on
// the type.
internal static class ShapesScenarios
{
    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(ShapeQueries);
            return
            [
                .. OpenAndClosed(host, "SearchAccounts", Search("a%", 3)),
                Of(host, "SearchAccounts", ScenarioTag.Other, "no match, open", Open, Search("z%", 99)),
                .. OpenAndClosed(host, "SearchAccountsUnpooled", Search("b%", 1)),
                .. OpenAndClosed(host, "ListAccountColumns"),
                Of(host, "ListAccountColumns", ScenarioTag.Ordinary, "in a transaction", InTransaction),
                .. OpenAndClosed(host, "GetHeadline", new GetHeadlineFilter { AccountId = 2 }),
                .. OpenAndClosed(host, "LookUpAccount", new AccountLookup { Email = "carol@example.com" }),
            ];
        }
    }

    private static SearchAccountsFilter Search(string name, int accountId) =>
        new() { Name = name, AccountId = accountId };
}
