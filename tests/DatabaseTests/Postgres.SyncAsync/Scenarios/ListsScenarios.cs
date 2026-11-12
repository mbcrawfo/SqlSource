using System.Collections.Generic;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Lists.sql: lists of values, empty, null and long, and a value of an enum that the server refuses.
internal static class ListsScenarios
{
    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(ListQueriesExtensions);
            return
            [
                .. OpenAndClosed(host, "ListAccountsByIds", new ListAccountsByIdsArgs([1, 3])),
                Of(
                    host,
                    "ListAccountsByIds",
                    ScenarioTag.Other,
                    "empty list, closed",
                    Closed,
                    new ListAccountsByIdsArgs([])
                ),
                Of(
                    host,
                    "ListAccountsByIds",
                    ScenarioTag.Other,
                    "a hundred ids, open",
                    Open,
                    new ListAccountsByIdsArgs([.. Enumerable.Range(1, 100)])
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
                Of(
                    host,
                    "ListAccountsExcept",
                    ScenarioTag.Other,
                    "empty list, open",
                    Open,
                    new ListAccountsExceptArgs([])
                ),
                Of(
                    host,
                    "ListAccountsOptional",
                    ScenarioTag.Ordinary,
                    "open",
                    Open,
                    new ListAccountsOptionalArgs(null)
                ),
                Of(
                    host,
                    "ListAccountsOptional",
                    ScenarioTag.Ordinary,
                    "closed",
                    Closed,
                    new ListAccountsOptionalArgs([1, 2])
                ),
                .. OpenAndClosed(
                    host,
                    "ListAccountsByIdsAndStatuses",
                    new ListAccountsByIdsAndStatusesArgs([1, 2, 3], ["active", "closed"])
                ),
                Of(
                    host,
                    "ListAccountsByIdsAndStatuses",
                    ScenarioTag.DatabaseError,
                    "no such status, closed",
                    Closed,
                    new ListAccountsByIdsAndStatusesArgs([1], ["deleted"])
                ),
                Of(
                    host,
                    "ListAccountsByIdsAndStatuses",
                    ScenarioTag.DatabaseError,
                    "no such status, in a transaction",
                    InTransaction,
                    new ListAccountsByIdsAndStatusesArgs([1], ["deleted"])
                ),
                .. OpenAndClosed(host, "ListAccountsUsedTwice", new ListAccountsUsedTwiceArgs([2])),
                .. OpenAndClosed(host, "ListAccountsByIdsNoPadding", new ListAccountsByIdsNoPaddingArgs([1, 2, 3])),
                .. OpenAndClosed(host, "ListAccountNotesExcept", new ListAccountNotesExceptArgs([1])),
                Of(
                    host,
                    "ListAccountNotesExcept",
                    ScenarioTag.Other,
                    "empty list, closed",
                    Closed,
                    new ListAccountNotesExceptArgs([])
                ),
                .. OpenAndClosed(host, "ListAccountNotesNotListed", new ListAccountNotesNotListedArgs([1])),
                Of(
                    host,
                    "ListAccountNotesNotListed",
                    ScenarioTag.Other,
                    "empty list, closed",
                    Closed,
                    new ListAccountNotesNotListedArgs([])
                ),
                Of(
                    host,
                    "ListAccountNotesOptional",
                    ScenarioTag.Ordinary,
                    "open",
                    Open,
                    new ListAccountNotesOptionalArgs([3])
                ),
                Of(
                    host,
                    "ListAccountNotesOptional",
                    ScenarioTag.Ordinary,
                    "closed",
                    Closed,
                    new ListAccountNotesOptionalArgs(null)
                ),
            ];
        }
    }
}
