using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Locations.sql: the query in each place its markers put a method, the private one among them, and its copy
// under async-method-suffix, whose synchronous method has the query's name.
internal static class LocationsScenarios
{
    private static readonly (Type Host, string Method)[] Places =
    [
        (typeof(LocationQueriesExtensions), "ListAccountNames"),
        (typeof(LocationQueries), "ListAccountNamesPublic"),
        (typeof(LocationQueries), "ListAccountNamesInternal"),
        (typeof(LocationQueries), "ListAccountNamesPrivate"),
        (typeof(LocationQueries.Sql), "ListAccountNamesNested"),
        (typeof(LocationQueriesExtensions), "ListAccountNamesWithSuffix"),
    ];

    public static IEnumerable<Scenario> All =>
        [
            .. Places.SelectMany(static place => OpenAndClosed(place.Host, place.Method)),
            Of(
                typeof(LocationQueries.Sql),
                "ListAccountNamesNested",
                ScenarioTag.Ordinary,
                "in a transaction",
                InTransaction
            ),
            Of(
                typeof(LocationQueries),
                "ListAccountNamesPrivate",
                ScenarioTag.Other,
                "a transaction not passed",
                TransactionNotPassed
            ),
        ];
}
