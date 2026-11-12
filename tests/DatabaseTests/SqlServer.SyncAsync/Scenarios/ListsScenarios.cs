using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Lists.sql: each list with values and empty, a null list, the most values SQL Server takes and one more, which
// the method refuses before it sends, and the update whose OUTPUT tests the list, on a copy of the seed.  No input of
// a list makes the server raise an error.
internal static class ListsScenarios
{
    // The most parameters a query sends SQL Server: it takes 2100 in one request, and SqlClient sends two of them, the
    // statement and its declarations, to sp_executesql.
    private const int Limit = 2098;

    private static readonly string[] Statuses = ["active", "suspended"];

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(ListQueriesExtensions);
            return
            [
                .. OpenAndClosed(host, "ListAccountsByIds", new ListAccountsByIdsArgs([1, 3])),
                Of(host, "ListAccountsByIds", ScenarioTag.Other, "empty, open", Open, new ListAccountsByIdsArgs([])),
                Of(
                    host,
                    "ListAccountsByIds",
                    ScenarioTag.Other,
                    "at the limit, closed",
                    Closed,
                    new ListAccountsByIdsArgs(ListArguments.IdsOf(Limit))
                ),
                Of(
                    host,
                    "ListAccountsByIds",
                    ScenarioTag.Other,
                    "over the limit, closed",
                    Closed,
                    new ListAccountsByIdsArgs(ListArguments.IdsOf(Limit + 1))
                ),
                Of(
                    host,
                    "ListAccountsByIds",
                    ScenarioTag.Ordinary,
                    "in a transaction",
                    InTransaction,
                    new ListAccountsByIdsArgs([2])
                ),
                .. OpenAndClosed(host, "ListAccountsExcept", new ListAccountsExceptArgs([2])),
                Of(host, "ListAccountsExcept", ScenarioTag.Other, "empty, open", Open, new ListAccountsExceptArgs([])),
                .. OpenAndClosed(host, "ListAccountsOptional", new ListAccountsOptionalArgs([1, 2])),
                Of(
                    host,
                    "ListAccountsOptional",
                    ScenarioTag.Other,
                    "null, open",
                    Open,
                    new ListAccountsOptionalArgs(null)
                ),
                Of(
                    host,
                    "ListAccountsOptional",
                    ScenarioTag.Other,
                    "empty, closed",
                    Closed,
                    new ListAccountsOptionalArgs([])
                ),
                .. OpenAndClosed(
                    host,
                    "ListAccountsByIdsAndStatuses",
                    new ListAccountsByIdsAndStatusesArgs([1, 2, 3], Statuses)
                ),
                Of(
                    host,
                    "ListAccountsByIdsAndStatuses",
                    ScenarioTag.Other,
                    "over the limit together, closed",
                    Closed,
                    new ListAccountsByIdsAndStatusesArgs(ListArguments.IdsOf(Limit - 1), Statuses)
                ),
                .. OpenAndClosed(host, "ListAccountsUsedTwice", new ListAccountsUsedTwiceArgs([2])),
                .. OpenAndClosed(host, "ListAccountsByIdsNoPadding", new ListAccountsByIdsNoPaddingArgs([1, 2, 3])),
                .. OpenAndClosed(host, "CountAccountsWithStatuses", new CountAccountsWithStatusesArgs(Statuses)),
                Of(
                    host,
                    "CountAccountsWithStatuses",
                    ScenarioTag.Other,
                    "empty, open",
                    Open,
                    new CountAccountsWithStatusesArgs([])
                ),
                .. OpenAndClosed(
                    host,
                    "CountAccountsGroupedByStatuses",
                    new CountAccountsGroupedByStatusesArgs(["closed"])
                ),
                .. OpenAndClosed(host, "CountAccountsExceptAndBefore", new CountAccountsExceptAndBeforeArgs([2])),
                .. OpenAndClosed(
                    host,
                    "CountAccountsWithoutStatusesTestedFirst",
                    new CountAccountsWithoutStatusesTestedFirstArgs(["active"])
                ),
                Of(
                    host,
                    "CountAccountsWithoutStatusesTestedFirst",
                    ScenarioTag.Other,
                    "empty, closed",
                    Closed,
                    new CountAccountsWithoutStatusesTestedFirstArgs([])
                ),
                Of(
                    host,
                    "MarkAccountsWithIds",
                    ScenarioTag.Ordinary,
                    "open",
                    Writes(Accounts),
                    new MarkAccountsWithIdsArgs([1, 3])
                ),
                Of(
                    host,
                    "MarkAccountsWithIds",
                    ScenarioTag.Ordinary,
                    "closed",
                    Writes(Accounts) with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    new MarkAccountsWithIdsArgs([1, 3])
                ),
                Of(
                    host,
                    "MarkAccountsWithIds",
                    ScenarioTag.Ordinary,
                    "in a transaction",
                    Writes(Accounts) with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    new MarkAccountsWithIdsArgs([])
                ),
            ];
        }
    }
}
