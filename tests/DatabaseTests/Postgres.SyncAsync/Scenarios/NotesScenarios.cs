using System.Collections.Generic;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Types;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Notes.sql: the enum and the domains read as types of the project, and the refused counts of its one-row
// queries.  None of its queries can raise an error: an email without "@", which the domain's check refuses in a column,
// finds no note.
internal static class NotesScenarios
{
    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(NoteQueriesExtensions);
            return
            [
                .. OpenAndClosed(host, "ListAccountNotes"),
                Of(host, "ListAccountNotes", ScenarioTag.Ordinary, "in a transaction", InTransaction),
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
                .. OpenAndClosed(host, "ListDisplayNames"),
                .. OpenAndClosed(host, "ListMarkedNames"),
                .. OpenAndClosed(host, "ListNoteKinds"),
                .. OpenAndClosed(host, "GetNoteByRef", new GetNoteByRefArgs(new NoteRef(5000000003))),
                Of(
                    host,
                    "GetNoteByRef",
                    ScenarioTag.NoRow,
                    "no note, open",
                    Open,
                    new GetNoteByRefArgs(new NoteRef(1))
                ),
                .. OpenAndClosed(host, "FindNotesByEmail", new FindNotesByEmailArgs(new Email("alice@example.com"))),
                Of(
                    host,
                    "FindNotesByEmail",
                    ScenarioTag.Other,
                    "an email without @, closed",
                    Closed,
                    new FindNotesByEmailArgs(new Email("alice.example.com"))
                ),
            ];
        }
    }
}
