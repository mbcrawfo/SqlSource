using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync.Scenarios;

// Queries/Batches.sql, claimed by BatchQueries: the query's method and the collection method of each query, the inserts
// on a copy of the seed and the reads on the seed.  A collection method runs with no items, with one full chunk, with a
// second chunk of one item, with a failure in its second chunk, and in the caller's transaction; an insert's failure is
// item 1,500 repeating the first item's id, and a read's, which no input can make fail on the server, a null item.  The
// repository's methods, InsertNoteInRepository, are BatchQueries' own, on the connection the scenario gives.
internal static class BatchesScenarios
{
    // The size of a chunk, the generator's constant.
    private const int ChunkSize = 1000;

    // The number of items of a failure in the second chunk, the last of them the one that fails.
    private const int LaterFailureCount = 1500;

    // The id of the first note an insert adds, past the seed's.
    private const int FirstId = 101;

    // The advisory lock that InsertNoteAfterLock takes, which no connection holds.
    private const long FreeLock = 114;

    private const string BatchNotes = "SELECT id, account_id, body FROM notes ORDER BY id";

    // A unique body, checked at the commit, which the seed's notes keep.
    private const string DeferredUniqueBody =
        "ALTER TABLE notes ADD CONSTRAINT notes_body_key UNIQUE (body) DEFERRABLE INITIALLY DEFERRED";

    private static readonly Setup Writes = new(Writes: true, Tables: [BatchNotes]);

    private static readonly Setup WritesClosed = Writes with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = Writes with { Transaction = CallerTransaction.Begun };

    // The inserts of the connection, the insert of the repository, and the query of each that returns one row.
    private static readonly string[] Inserts =
    [
        "InsertNote",
        "InsertNoteBoth",
        "InsertNoteRowCount",
        "InsertNoteQuietly",
        "InsertNoteSerializable",
        "InsertNoteReadCommitted",
        "InsertNoteRepeatableRead",
        "InsertNoteNoTransaction",
        "InsertNoteAfterLock",
    ];

    private static readonly HashSet<string> NoRowCountOf = ["InsertNoteRowCount", "InsertNoteQuietly"];

    private static readonly Func<DbConnection, DbDataSource, object> Repository = static (connection, _) =>
        new BatchQueries(connection);

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(BatchQueriesExtensions);
            foreach (var method in Inserts)
            {
                foreach (var scenario in InsertScenarios(host, method, null))
                {
                    yield return scenario;
                }
            }

            foreach (var scenario in InsertScenarios(typeof(BatchQueries), "InsertNoteInRepository", Repository))
            {
                yield return scenario;
            }

            // An item with no row, which a one query refuses, rolls back the item before it.
            yield return OfCollection(
                host,
                "InsertNote",
                ScenarioTag.NoRow,
                "an item with no row, closed",
                WritesClosed,
                new NoteToAdd[]
                {
                    new(FirstId, "First.", 1),
                    new(FirstId + 1, "Lost.", 99),
                    new(FirstId + 2, "Third.", 2),
                }
            );
            yield return OfCollection(
                host,
                "InsertNote",
                ScenarioTag.LaterChunkFailure,
                "a duplicate id in the second chunk, in a transaction",
                WritesInTransaction,
                LaterFailure("InsertNote")
            );
            yield return OfCollection(
                host,
                "InsertNote",
                ScenarioTag.LaterChunkFailure,
                "a duplicate id in the second chunk, closed",
                WritesClosed,
                LaterFailure("InsertNote")
            );

            // A constraint checked at the commit fails after every item ran, at the method's commit under on and at the
            // implicit transaction's under off: no item raised it, so the database's error is thrown as it is.
            foreach (var method in new[] { "InsertNote", "InsertNoteNoTransaction" })
            {
                yield return OfCollection(
                    host,
                    method,
                    ScenarioTag.DatabaseError,
                    "a deferred constraint that fails at the commit, open",
                    Writes with
                    {
                        Prepare = DeferredUniqueBody,
                    },
                    new NoteToAdd[]
                    {
                        new(FirstId, "Twice.", 1),
                        new(FirstId + 1, "Once.", 2),
                        new(FirstId + 2, "Twice.", 3),
                    }
                );
            }

            // The reads, on the seed: alice's two notes, bob's one and carol's none.
            foreach (var scenario in OpenAndClosed(host, "FindNotes", new FindNotesArgs(1)))
            {
                yield return scenario;
            }

            yield return Of(host, "FindNotes", ScenarioTag.Other, "no notes, closed", Closed, new FindNotesArgs(3));
            foreach (var scenario in OpenAndClosed(host, "FindNote", new FindNoteArgs(3)))
            {
                yield return scenario;
            }

            yield return Of(host, "FindNote", ScenarioTag.NoRow, "no note, closed", Closed, new FindNoteArgs(99));
            yield return Of(
                host,
                "FindNote",
                ScenarioTag.NoRow,
                "no note, in a transaction",
                InTransaction,
                new FindNoteArgs(99)
            );

            foreach (
                var scenario in ReadScenarios(
                    host,
                    "FindNotes",
                    static count =>
                        Enumerable.Range(0, count).Select(static i => new FindNotesArgs((i % 3) + 1)).ToArray()
                )
            )
            {
                yield return scenario;
            }

            foreach (
                var scenario in ReadScenarios(
                    host,
                    "FindNotesStreamBoth",
                    static count =>
                        Enumerable
                            .Range(0, count)
                            .Select(static i => new FindNotesStreamBothArgs((i % 3) + 1))
                            .ToArray()
                )
            )
            {
                yield return scenario;
            }

            // The ids 1 to 4 in turn: the three notes, and nothing for 4, which no note has.
            foreach (
                var scenario in ReadScenarios(
                    host,
                    "FindNote",
                    static count =>
                        Enumerable.Range(0, count).Select(static i => new FindNoteArgs((i % 4) + 1)).ToArray()
                )
            )
            {
                yield return scenario;
            }
        }
    }

    // The query's method and the collection method of an insert, each on a copy of the seed.  The repository's method
    // takes no transaction, so it has no scenario in the caller's.
    private static IEnumerable<Scenario> InsertScenarios(
        Type host,
        string method,
        Func<DbConnection, DbDataSource, object>? instance
    )
    {
        Scenario Query(ScenarioTag tag, string what, Setup setup, object parameters) =>
            Of(host, method, tag, what, setup, parameters) with
            {
                Instance = instance,
            };

        Scenario Collection(ScenarioTag tag, string what, Setup setup, object items) =>
            OfCollection(host, method, tag, what, setup, items, instance);

        yield return Query(ScenarioTag.Ordinary, "open", Writes, One(method, FirstId, 2));
        yield return Query(ScenarioTag.Ordinary, "closed", WritesClosed, One(method, FirstId, 2));
        if (!NoRowCountOf.Contains(method))
        {
            yield return Query(ScenarioTag.NoRow, "no such account, closed", WritesClosed, One(method, FirstId, 99));
        }

        // Id 1 is the seed's first note.
        yield return Query(ScenarioTag.DatabaseError, "a duplicate id, closed", WritesClosed, One(method, 1, 2));
        if (instance is null)
        {
            yield return Query(
                ScenarioTag.DatabaseError,
                "a duplicate id, in a transaction",
                WritesInTransaction,
                One(method, 1, 2)
            );
        }

        yield return Collection(ScenarioTag.Empty, "no items, closed", WritesClosed, Many(method, 0));
        yield return Collection(ScenarioTag.ChunkBoundary, "one full chunk, open", Writes, Many(method, ChunkSize));
        yield return Collection(
            ScenarioTag.ChunkBoundary,
            "a second chunk of one item, closed",
            WritesClosed,
            Many(method, ChunkSize + 1)
        );
        yield return Collection(
            ScenarioTag.LaterChunkFailure,
            "a duplicate id in the second chunk, open",
            Writes,
            LaterFailure(method)
        );
        if (instance is null)
        {
            yield return Collection(
                ScenarioTag.Ordinary,
                "two chunks, in a transaction",
                WritesInTransaction,
                Many(method, ChunkSize + 1)
            );
        }
    }

    // The collection method of a read, on the seed.  Its failure in the second chunk is a null item.
    private static IEnumerable<Scenario> ReadScenarios(Type host, string method, Func<int, Array> items)
    {
        var failing = items(LaterFailureCount);
        failing.SetValue(null, LaterFailureCount - 1);
        return
        [
            OfCollection(host, method, ScenarioTag.Empty, "no items, closed", Closed, items(0)),
            OfCollection(host, method, ScenarioTag.ChunkBoundary, "one full chunk, open", Open, items(ChunkSize)),
            OfCollection(
                host,
                method,
                ScenarioTag.ChunkBoundary,
                "a second chunk of one item, closed",
                Closed,
                items(ChunkSize + 1)
            ),
            OfCollection(
                host,
                method,
                ScenarioTag.LaterChunkFailure,
                "a null item in the second chunk, open",
                Open,
                failing
            ),
            OfCollection(
                host,
                method,
                ScenarioTag.Ordinary,
                "two chunks, in a transaction",
                InTransaction,
                items(ChunkSize + 1)
            ),
        ];
    }

    // The input model of the method's query: a note with the id, to the account.
    private static object One(string method, int id, int account) =>
        method == "InsertNoteAfterLock"
            ? new InsertNoteAfterLockArgs(id, $"Note {id}.", FreeLock, account)
            : new NoteToAdd(id, $"Note {id}.", account);

    // The items of the method's collection: notes with the ids from FirstId on, to the seed's three accounts in turn.
    private static Array Many(string method, int count) => ItemsOf(method, IdsFrom(count));

    // Notes whose last, item 1,500, in the second chunk, repeats the first's id, which the primary key refuses.
    private static Array LaterFailure(string method) =>
        ItemsOf(method, [.. IdsFrom(LaterFailureCount - 1), (FirstId, 1)]);

    private static (int Id, int Account)[] IdsFrom(int count) =>
        [.. Enumerable.Range(0, count).Select(static i => (FirstId + i, (i % 3) + 1))];

    private static Array ItemsOf(string method, (int Id, int Account)[] notes) =>
        method == "InsertNoteAfterLock"
            ? notes
                .Select(static note => new InsertNoteAfterLockArgs(note.Id, $"Note {note.Id}.", FreeLock, note.Account))
                .ToArray()
            : notes.Select(static note => new NoteToAdd(note.Id, $"Note {note.Id}.", note.Account)).ToArray();
}
