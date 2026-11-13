using System.Collections.Generic;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/ExternalRefs.sql: the alias type over bigint read and sent as ExternalId, an id with no row, and the insert
// of a row, on a copy of the seed, with a key that is taken, which the server refuses.
internal static class ExternalRefsScenarios
{
    private const string ExternalRefs = "SELECT id, account_id, external_id FROM dbo.external_refs ORDER BY id";

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(ExternalRefQueriesExtensions);
            var writes = Writes(ExternalRefs);
            return
            [
                .. OpenAndClosed(host, "GetExternalRef", new GetExternalRefArgs(new ExternalId(5_000_000_002))),
                Of(
                    host,
                    "GetExternalRef",
                    ScenarioTag.NoRow,
                    "no row, closed",
                    Closed,
                    new GetExternalRefArgs(new ExternalId(5_000_000_009))
                ),
                .. OpenAndClosed(host, "ListAccountExternalIds"),
                Of(host, "AddExternalRef", ScenarioTag.Ordinary, "open", writes, Ref(3)),
                Of(
                    host,
                    "AddExternalRef",
                    ScenarioTag.Ordinary,
                    "closed",
                    writes with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    Ref(3)
                ),
                Of(
                    host,
                    "AddExternalRef",
                    ScenarioTag.DatabaseError,
                    "a taken id, closed",
                    writes with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    Ref(1)
                ),
                Of(
                    host,
                    "AddExternalRef",
                    ScenarioTag.DatabaseError,
                    "a taken id, in a transaction",
                    writes with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    Ref(1)
                ),
            ];
        }
    }

    // A row of carol's, by the id given.
    private static AddExternalRefArgs Ref(int id) => new(id, 3, new ExternalId(5_000_000_003));
}
