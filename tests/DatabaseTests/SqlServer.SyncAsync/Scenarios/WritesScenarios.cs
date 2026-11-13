using System;
using System.Collections.Generic;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Writes.sql: the writes that return rows through OUTPUT, each on a copy of the seed, an id with no row, a key
// that no account has and a value too long for its column, which the server refuses.
internal static class WritesScenarios
{
    private static readonly DateTime WrittenAt = new(2024, 5, 1, 12, 0, 0, DateTimeKind.Unspecified);

    private static readonly Setup WritesOpen = Writes(Accounts, Notes);

    private static readonly Setup WritesClosed = WritesOpen with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = WritesOpen with { Transaction = CallerTransaction.Begun };

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(WriteQueriesExtensions);
            return
            [
                Of(host, "AddNote", ScenarioTag.Ordinary, "open", WritesOpen, Note(3)),
                Of(host, "AddNote", ScenarioTag.Ordinary, "closed", WritesClosed, Note(3)),
                Of(host, "AddNote", ScenarioTag.Ordinary, "in a transaction", WritesInTransaction, Note(3)),
                Of(host, "AddNote", ScenarioTag.DatabaseError, "no such account, closed", WritesClosed, Note(99)),
                Of(
                    host,
                    "AddNote",
                    ScenarioTag.DatabaseError,
                    "no such account, in a transaction",
                    WritesInTransaction,
                    Note(99)
                ),
                Of(host, "RenameAccount", ScenarioTag.Ordinary, "open", WritesOpen, new RenameAccountArgs("robert", 2)),
                Of(
                    host,
                    "RenameAccount",
                    ScenarioTag.Ordinary,
                    "closed",
                    WritesClosed,
                    new RenameAccountArgs("robert", 2)
                ),
                Of(
                    host,
                    "RenameAccount",
                    ScenarioTag.Other,
                    "no account, closed",
                    WritesClosed,
                    new RenameAccountArgs("nobody", 99)
                ),
                Of(
                    host,
                    "RenameAccount",
                    ScenarioTag.DatabaseError,
                    "a name too long, in a transaction",
                    WritesInTransaction,
                    new RenameAccountArgs(new string('r', 101), 2)
                ),
                Of(
                    host,
                    "MergeAccountStatus",
                    ScenarioTag.Ordinary,
                    "open",
                    WritesOpen,
                    new MergeAccountStatusArgs(3, "active")
                ),
                Of(
                    host,
                    "MergeAccountStatus",
                    ScenarioTag.Ordinary,
                    "closed",
                    WritesClosed,
                    new MergeAccountStatusArgs(3, "active")
                ),
                Of(
                    host,
                    "MergeAccountStatus",
                    ScenarioTag.DatabaseError,
                    "a status too long, closed",
                    WritesClosed,
                    new MergeAccountStatusArgs(3, new string('s', 21))
                ),
            ];
        }
    }

    private static AddNoteArgs Note(int accountId) => new(accountId, "call", "Called back.", WrittenAt);
}
