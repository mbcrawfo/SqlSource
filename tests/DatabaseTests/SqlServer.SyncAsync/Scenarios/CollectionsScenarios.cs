using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Collections/: one query by each collection type, of the project's settings file, of a file's header and of
// a query's marker, through the three claims.
internal static class CollectionsScenarios
{
    private static readonly (Type Host, string Method)[] Methods =
    [
        (typeof(CollectionQueries), "ListCollectedAccounts"),
        (typeof(CollectionQueries), "ListCollectedAccountsAsIEnumerable"),
        (typeof(CollectionQueries), "ListCollectedAccountsAsICollection"),
        (typeof(CollectionQueries), "ListCollectedAccountsAsIReadOnlyCollection"),
        (typeof(CollectionQueries), "ListCollectedAccountsAsList"),
        (typeof(CollectionQueries), "ListCollectedAccountsAsImmutableList"),
        (typeof(HeaderCollectionQueries), "ListAccountsByMetadata"),
        (typeof(MarkerCollectionQueries), "ListAccountsByHeader"),
        (typeof(MarkerCollectionQueries), "ListAccountsInArray"),
        (typeof(MarkerCollectionQueries), "ListAccountsInImmutableList"),
    ];

    public static IEnumerable<Scenario> All =>
        [
            .. Methods.SelectMany(static method => OpenAndClosed(method.Host, method.Method)),
            Of(
                typeof(HeaderCollectionQueries),
                "ListAccountsByMetadata",
                ScenarioTag.Ordinary,
                "in a transaction",
                InTransaction
            ),
        ];
}
