using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Methods.sql: each shape with the rows it allows and the counts it refuses, the writes, which run on a copy of
// the seed, the errors after the rows, a token, the caller's transaction, and the two calls on a data source with
// multiplexing.
internal static class MethodsScenarios
{
    private static readonly Setup Writes = new(
        Writes: true,
        Tables: [Notes, "SELECT id, name FROM accounts ORDER BY id"]
    );

    private static readonly Setup WritesClosed = Writes with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = Writes with { Transaction = CallerTransaction.Begun };

    private static readonly Dictionary<string, string> ById = new() { ["filter"] = "id = @accountId" };

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(MethodQueriesExtensions);

            // The writes, each on a copy of the seed.
            foreach (var method in new[] { "TouchAccount", "TouchAccountQuietly", "RenameInCte" })
            {
                object parameters = method switch
                {
                    "TouchAccount" => new TouchAccountArgs(1),
                    "TouchAccountQuietly" => new TouchAccountQuietlyArgs(1),
                    _ => new RenameInCteArgs(1),
                };
                yield return Of(host, method, ScenarioTag.Ordinary, "open", Writes, parameters);
                yield return Of(host, method, ScenarioTag.Ordinary, "closed", WritesClosed, parameters);
            }

            yield return Of(
                host,
                "TouchAccount",
                ScenarioTag.Other,
                "no row, closed",
                WritesClosed,
                new TouchAccountArgs(99)
            );
            yield return Of(host, "AddNote", ScenarioTag.Ordinary, "open", Writes, new AddNoteArgs(3, "Called back."));
            yield return Of(
                host,
                "AddNote",
                ScenarioTag.Ordinary,
                "closed",
                WritesClosed,
                new AddNoteArgs(3, "Called back.")
            );
            yield return Of(
                host,
                "AddNote",
                ScenarioTag.Ordinary,
                "in a transaction",
                WritesInTransaction,
                new AddNoteArgs(3, "Called back.")
            );
            yield return Of(
                host,
                "AddNote",
                ScenarioTag.DatabaseError,
                "no such account, closed",
                WritesClosed,
                new AddNoteArgs(99, "Lost.")
            );
            yield return Of(
                host,
                "AddNote",
                ScenarioTag.DatabaseError,
                "no such account, in a transaction",
                WritesInTransaction,
                new AddNoteArgs(99, "Lost.")
            );

            // one and one-optional: a row, none, and two.
            foreach (var method in new[] { "GetAccountInRange", "GetAccountInRangeBoth", "FindAccountInRange" })
            {
                foreach (var scenario in OpenAndClosed(host, method, RangeOf(method, 2, 2)))
                {
                    yield return scenario;
                }

                yield return Of(host, method, ScenarioTag.NoRow, "no row, closed", Closed, RangeOf(method, 4, 9));
                yield return Of(host, method, ScenarioTag.TooManyRows, "two rows, open", Open, RangeOf(method, 1, 3));
                yield return Of(
                    host,
                    method,
                    ScenarioTag.TooManyRows,
                    "two rows, closed",
                    Closed,
                    RangeOf(method, 1, 3)
                );
            }

            yield return Of(
                host,
                "GetAccountInRange",
                ScenarioTag.TooManyRows,
                "two rows, in a transaction",
                InTransaction,
                new GetAccountInRangeArgs(1, 3)
            );

            foreach (var scenario in OpenAndClosed(host, "CountNotesOf", new CountNotesOfArgs(1)))
            {
                yield return scenario;
            }

            foreach (var scenario in OpenAndClosed(host, "ListAccountsWhere", new ListAccountsWhereArgs(2), ById))
            {
                yield return scenario;
            }

            yield return Of(
                host,
                "ListAccountsWhere",
                ScenarioTag.Other,
                "every row, by another filter",
                Open,
                new ListAccountsWhereArgs(2),
                new Dictionary<string, string> { ["filter"] = "true" }
            );

            foreach (var scenario in OpenAndClosed(host, "ListAccountsAsRowCount"))
            {
                yield return scenario;
            }

            // A lock that nothing holds, so the last row is not held.
            foreach (var scenario in OpenAndClosed(host, "ListThenWait", new ListThenWaitArgs(5, 0x5359_4E43)))
            {
                yield return scenario;
            }

            // The errors after the rows, which every shape throws.
            foreach (
                var method in new[]
                {
                    "ListThenRaise",
                    "GetThenRaise",
                    "FindThenRaise",
                    "RunThenRaise",
                    "CountThenRaise",
                }
            )
            {
                yield return Of(host, method, ScenarioTag.DatabaseError, "open", Open);
                yield return Of(host, method, ScenarioTag.DatabaseError, "closed", Closed);
            }

            yield return Of(host, "ListThenRaise", ScenarioTag.DatabaseError, "in a transaction", InTransaction);

            // Npgsql refuses a synchronous command on a data source with multiplexing outside a transaction, and runs
            // one in a transaction that the caller began.
            yield return Of(
                host,
                "GetAccountInRange",
                ScenarioTag.Ordinary,
                "multiplexing, closed",
                Closed with
                {
                    Source = "multiplexing",
                },
                new GetAccountInRangeArgs(2, 2)
            );
            yield return Of(
                host,
                "GetAccountInRange",
                ScenarioTag.Ordinary,
                "multiplexing, in a transaction",
                InTransaction with
                {
                    Source = "multiplexing",
                },
                new GetAccountInRangeArgs(2, 2)
            );
        }
    }

    private static object RangeOf(string method, int fromId, int toId) =>
        method switch
        {
            "GetAccountInRange" => new GetAccountInRangeArgs(fromId, toId),
            "GetAccountInRangeBoth" => new GetAccountInRangeBothArgs(fromId, toId),
            _ => new FindAccountInRangeArgs(fromId, toId),
        };
}
