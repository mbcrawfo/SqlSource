using System;
using System.Collections.Generic;
using SqlSource.DatabaseTests.SqlServer.EndToEnd.Types;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Notes.sql: the reads through the converters of the alias type, AccountId and NoteCount, an id with no
// account, an email with none, and the insert of an account, on a copy of the seed, with a name too long for its
// column, which the server refuses.
internal static class NotesScenarios
{
    private static readonly DateOnly OpenedOn = new(2024, 4, 1);

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(NoteQueriesExtensions);
            var writes = Writes(Accounts);
            return
            [
                .. OpenAndClosed(host, "ListAccountNotes"),
                .. OpenAndClosed(host, "CountAccountNotes"),
                .. OpenAndClosed(host, "GetAccountNoteCount", new GetAccountNoteCountArgs(new AccountId(1))),
                Of(
                    host,
                    "GetAccountNoteCount",
                    ScenarioTag.NoRow,
                    "no account, closed",
                    Closed,
                    new GetAccountNoteCountArgs(new AccountId(99))
                ),
                Of(
                    host,
                    "GetAccountNoteCount",
                    ScenarioTag.NoRow,
                    "no account, in a transaction",
                    InTransaction,
                    new GetAccountNoteCountArgs(new AccountId(99))
                ),
                .. OpenAndClosed(host, "ListAccountMonths"),
                .. OpenAndClosed(host, "ListMarkedNames"),
                .. OpenAndClosed(host, "ListNoteKinds"),
                .. OpenAndClosed(host, "ListReminders"),
                .. OpenAndClosed(
                    host,
                    "FindAccountByEmail",
                    new FindAccountByEmailArgs(new Email("alice@example.com"))
                ),
                Of(
                    host,
                    "FindAccountByEmail",
                    ScenarioTag.Other,
                    "no account, closed",
                    Closed,
                    new FindAccountByEmailArgs(new Email("nobody@example.com"))
                ),
                Of(host, "AddAccount", ScenarioTag.Ordinary, "open", writes, Account("dave")),
                Of(
                    host,
                    "AddAccount",
                    ScenarioTag.Ordinary,
                    "closed",
                    writes with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    Account("dave")
                ),
                Of(
                    host,
                    "AddAccount",
                    ScenarioTag.Ordinary,
                    "in a transaction",
                    writes with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    Account("dave")
                ),
                Of(
                    host,
                    "AddAccount",
                    ScenarioTag.DatabaseError,
                    "a name too long, closed",
                    writes with
                    {
                        Connection = ConnectionStart.Closed,
                    },
                    Account(new string('d', 101))
                ),
                Of(
                    host,
                    "AddAccount",
                    ScenarioTag.DatabaseError,
                    "a name too long, in a transaction",
                    writes with
                    {
                        Transaction = CallerTransaction.Begun,
                    },
                    Account(new string('d', 101))
                ),
            ];
        }
    }

    private static AddAccountArgs Account(string name) => new(name, new Email(name + "@example.com"), OpenedOn);
}
