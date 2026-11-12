using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Locations.sql: the query in each place its markers put a method, the private one among them, each with an
// id that names no account.
internal static class LocationsScenarios
{
    private static readonly (Type Host, string Method)[] Places =
    [
        (typeof(LocationQueriesExtensions), "GetLocatedName"),
        (typeof(LocationQueries), "GetLocatedNameInternal"),
        (typeof(LocationQueries), "GetLocatedNamePrivate"),
        (typeof(LocationQueries.Sql), "GetLocatedNameNested"),
    ];

    public static IEnumerable<Scenario> All =>
        [
            .. Places.SelectMany(place =>
                OpenAndClosed(place.Host, place.Method, new GetLocatedNameArgs(1))
                    .Append(
                        Of(
                            place.Host,
                            place.Method,
                            ScenarioTag.NoRow,
                            "no account, closed",
                            Closed,
                            new GetLocatedNameArgs(99)
                        )
                    )
            ),
            Of(
                typeof(LocationQueriesExtensions),
                "GetLocatedName",
                ScenarioTag.NoRow,
                "no account, in a transaction",
                InTransaction,
                new GetLocatedNameArgs(99)
            ),
        ];
}
