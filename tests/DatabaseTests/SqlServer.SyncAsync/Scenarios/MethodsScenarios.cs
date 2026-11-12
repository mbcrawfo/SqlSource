using System;
using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Methods.sql: each shape with the rows it allows and the counts it refuses, the writes, which run on a copy of
// the seed, a token, the errors after the rows and what they leave of a caller's transaction, with XACT_ABORT off and
// on, a transaction that was not passed, and one that has completed.
internal static class MethodsScenarios
{
    private static readonly Setup WritesOpen = Writes(Accounts, Notes);

    private static readonly Setup WritesClosed = WritesOpen with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = WritesOpen with { Transaction = CallerTransaction.Begun };

    // A session of its own with XACT_ABORT ON, under which nearly every error rolls the transaction back; the pool
    // would hand the setting to the next scenario.
    private static readonly Setup XactAbortInTransaction = InTransaction with
    {
        Source = "unpooled",
        Prepare = "SET XACT_ABORT ON",
    };

    private static readonly DateTime WrittenAt = new(2024, 5, 1, 12, 0, 0, DateTimeKind.Unspecified);

    // The errors that come after the rows or the row, each of which every call throws.
    private static readonly string[] ThenRaise =
    [
        "ListThenRaise",
        "GetThenRaise",
        "FindThenRaise",
        "FindNoneThenRaise",
        "CountThenRaise",
        "RunThenRaise",
    ];

    // The rows, then a failed conversion, an overflow or a THROW, which with XACT_ABORT off rolls a transaction back or
    // leaves it open, some after a refusal or an error at a row.
    private static readonly string[] ThenFail =
    [
        "ListThenFailConversion",
        "GetTwoThenFailConversion",
        "GetTwoThenRaise",
        "ListNullAtThirdThenFailConversion",
        "ListFailingAtSecondThenFailConversion",
        "ListFailingAtSecondThenOverflow",
        "ListThenOverflowThenFailConversion",
        "GetFailingAtSecondThenFailConversion",
        "ListFailingBeforeTheRowsThenFailConversion",
    ];

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(MethodQueriesExtensions);

            // rowcount and none: the writes, each on a copy of the seed.
            yield return Of(host, "TouchAccount", ScenarioTag.Ordinary, "open", WritesOpen, new TouchAccountArgs(1));
            yield return Of(
                host,
                "TouchAccount",
                ScenarioTag.Ordinary,
                "closed",
                WritesClosed,
                new TouchAccountArgs(1)
            );
            yield return Of(
                host,
                "TouchAccount",
                ScenarioTag.Other,
                "no row, closed",
                WritesClosed,
                new TouchAccountArgs(99)
            );
            yield return Of(
                host,
                "TouchAccount",
                ScenarioTag.Ordinary,
                "in a transaction",
                WritesInTransaction,
                new TouchAccountArgs(1)
            );
            yield return Of(
                host,
                "TouchAccount",
                ScenarioTag.Other,
                "a transaction not passed",
                TransactionNotPassed,
                new TouchAccountArgs(1)
            );
            yield return Of(
                host,
                "SetAccountStatus",
                ScenarioTag.Ordinary,
                "open",
                WritesOpen,
                new SetAccountStatusArgs("suspended", 1)
            );
            yield return Of(
                host,
                "SetAccountStatus",
                ScenarioTag.Ordinary,
                "closed",
                WritesClosed,
                new SetAccountStatusArgs("suspended", 1)
            );
            yield return Of(
                host,
                "SetAccountStatus",
                ScenarioTag.Ordinary,
                "in a transaction",
                WritesInTransaction,
                new SetAccountStatusArgs("suspended", 1)
            );
            yield return Of(
                host,
                "SetAccountStatus",
                ScenarioTag.DatabaseError,
                "a status too long, in a transaction",
                WritesInTransaction,
                new SetAccountStatusArgs(new string('x', 21), 1)
            );
            yield return Of(
                host,
                "SetAccountStatus",
                ScenarioTag.Other,
                "a completed transaction",
                CompletedTransaction,
                new SetAccountStatusArgs("suspended", 1)
            );
            yield return Of(
                host,
                "AddTwoNotes",
                ScenarioTag.Ordinary,
                "open",
                WritesOpen,
                new AddTwoNotesArgs(3, "call", "Called back.", WrittenAt)
            );
            yield return Of(
                host,
                "AddTwoNotes",
                ScenarioTag.Ordinary,
                "closed",
                WritesClosed,
                new AddTwoNotesArgs(3, "call", "Called back.", WrittenAt)
            );
            yield return Of(
                host,
                "AddTwoNotes",
                ScenarioTag.Ordinary,
                "in a transaction",
                WritesInTransaction,
                new AddTwoNotesArgs(3, "call", "Called back.", WrittenAt)
            );
            yield return Of(
                host,
                "AddTwoNotes",
                ScenarioTag.DatabaseError,
                "no such account, closed",
                WritesClosed,
                new AddTwoNotesArgs(99, "call", "Lost.", WrittenAt)
            );
            yield return Of(
                host,
                "AddTwoNotes",
                ScenarioTag.DatabaseError,
                "no such account, in a transaction",
                WritesInTransaction,
                new AddTwoNotesArgs(99, "call", "Lost.", WrittenAt)
            );
            foreach (var scenario in OpenAndClosed(host, "CountReadAccounts"))
            {
                yield return scenario;
            }

            // SET NOCOUNT ON of the batch lasts for the session, which the copy's pool ends.
            yield return Of(host, "TouchAccountsQuietly", ScenarioTag.Ordinary, "open", WritesOpen);
            yield return Of(host, "TouchAccountsQuietly", ScenarioTag.Ordinary, "closed", WritesClosed);
            yield return Of(
                host,
                "TouchAccountQuietly",
                ScenarioTag.Ordinary,
                "open",
                WritesOpen,
                new TouchAccountQuietlyArgs(1)
            );
            yield return Of(
                host,
                "TouchAccountQuietly",
                ScenarioTag.Ordinary,
                "closed",
                WritesClosed,
                new TouchAccountQuietlyArgs(1)
            );
            yield return Of(
                host,
                "TouchAndCountAccounts",
                ScenarioTag.Ordinary,
                "open",
                WritesOpen,
                new TouchAndCountAccountsArgs(2, 1)
            );
            yield return Of(
                host,
                "TouchAndCountAccounts",
                ScenarioTag.Ordinary,
                "closed",
                WritesClosed,
                new TouchAndCountAccountsArgs(2, 1)
            );
            yield return Of(
                host,
                "TouchAndGetAccount",
                ScenarioTag.Ordinary,
                "open",
                WritesOpen,
                new TouchAndGetAccountArgs(2, 1)
            );
            yield return Of(
                host,
                "TouchAndGetAccount",
                ScenarioTag.Ordinary,
                "closed",
                WritesClosed,
                new TouchAndGetAccountArgs(2, 1)
            );
            yield return Of(
                host,
                "TouchAndGetAccount",
                ScenarioTag.NoRow,
                "no row after the update, in a transaction",
                WritesInTransaction,
                new TouchAndGetAccountArgs(2, 99)
            );
            yield return Of(
                host,
                "MergeAccountStatuses",
                ScenarioTag.Ordinary,
                "open",
                WritesOpen,
                new MergeAccountStatusesArgs(2, "suspended")
            );
            yield return Of(
                host,
                "MergeAccountStatuses",
                ScenarioTag.Ordinary,
                "closed",
                WritesClosed,
                new MergeAccountStatusesArgs(2, "suspended")
            );
            yield return Of(
                host,
                "MergeAccountStatuses",
                ScenarioTag.Ordinary,
                "in a transaction",
                WritesInTransaction,
                new MergeAccountStatusesArgs(2, "suspended")
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
            yield return Of(
                host,
                "GetAccountInRange",
                ScenarioTag.Other,
                "a transaction not passed",
                TransactionNotPassed,
                new GetAccountInRangeArgs(2, 2)
            );
            foreach (var scenario in OpenAndClosed(host, "GetFirstOfTwoAccounts", new GetFirstOfTwoAccountsArgs(1, 2)))
            {
                yield return scenario;
            }

            yield return Of(
                host,
                "GetFirstOfTwoAccounts",
                ScenarioTag.NoRow,
                "no first row, closed",
                Closed,
                new GetFirstOfTwoAccountsArgs(99, 2)
            );

            // A token: its default and another fragment, which leaves the parameter unused.
            foreach (
                var scenario in OpenAndClosed(
                    host,
                    "ListAccountsWhere",
                    new ListAccountsWhereArgs(2),
                    new Dictionary<string, string> { ["filter"] = "WHERE id <= @lastId" }
                )
            )
            {
                yield return scenario;
            }

            yield return Of(
                host,
                "ListAccountsWhere",
                ScenarioTag.Other,
                "another fragment, open",
                Open,
                new ListAccountsWhereArgs(1),
                new Dictionary<string, string> { ["filter"] = "WHERE status = 'suspended'" }
            );

            // The errors after the rows, which every shape throws, and a THROW, which leaves the transaction open.
            foreach (var method in ThenRaise)
            {
                yield return Of(host, method, ScenarioTag.DatabaseError, "open", Open);
                yield return Of(host, method, ScenarioTag.DatabaseError, "closed", Closed);
            }

            yield return Of(host, "ListThenRaise", ScenarioTag.DatabaseError, "in a transaction", InTransaction);
            yield return Of(
                host,
                "RunThenRaise",
                ScenarioTag.DatabaseError,
                "XACT_ABORT ON, in a transaction",
                XactAbortInTransaction
            );
            yield return Of(host, "RunThenRaise", ScenarioTag.Other, "a completed transaction", CompletedTransaction);

            // A failed conversion, an overflow or a THROW after the rows, a refusal or an error at a row before it: on
            // a connection alone, in a transaction with XACT_ABORT off, which some roll back, and with it on.
            foreach (var method in ThenFail)
            {
                yield return Of(host, method, ScenarioTag.DatabaseError, "open", Open);
                yield return Of(host, method, ScenarioTag.DatabaseError, "closed", Closed);
                yield return Of(host, method, ScenarioTag.DatabaseError, "in a transaction", InTransaction);
            }

            yield return Of(
                host,
                "GetTwoThenRaise",
                ScenarioTag.DatabaseError,
                "XACT_ABORT ON, in a transaction",
                XactAbortInTransaction
            );
            yield return Of(
                host,
                "ListFailingAtSecondThenOverflow",
                ScenarioTag.DatabaseError,
                "XACT_ABORT ON, in a transaction",
                XactAbortInTransaction
            );
        }
    }

    private static object RangeOf(string method, int firstId, int lastId) =>
        method switch
        {
            "GetAccountInRange" => new GetAccountInRangeArgs(firstId, lastId),
            "GetAccountInRangeBoth" => new GetAccountInRangeBothArgs(firstId, lastId),
            _ => new FindAccountInRangeArgs(firstId, lastId),
        };
}
