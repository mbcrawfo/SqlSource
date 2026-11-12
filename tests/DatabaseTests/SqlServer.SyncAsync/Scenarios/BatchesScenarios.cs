using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Linq;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using static SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios.Calls;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync.Scenarios;

// Queries/Batches.sql, claimed by BatchQueries: the query's method and the collection method of each query, the inserts
// on a copy of the seed and the reads on the seed.  An insert is given SqlServerDatabases.BatchNotes by its token,
// which has no identity, so that it writes the id it is given.  A collection method runs with no items, with one full
// chunk, with a second chunk of one item, with a failure in its second chunk, and in the caller's transaction; an
// insert's failure is item 1,500 repeating the first item's id, and a read's, which no input can make fail on the
// server, a null item.  Each transaction copy also fails under XACT_ABORT ON, on a session of its own, which rolls back
// the transaction on the server, and the copies at an isolation level record the session's level after the call.  The
// errors of the queries that return other result sets, a conversion that ends the batch, a later error that rolls
// back the caller's transaction, and a completed transaction are run as the end-to-end tests run them.  The
// repository's methods are BatchQueries' own, on the connection the scenario gives, and take no transaction.
internal static class BatchesScenarios
{
    // The size of a chunk, the generator's constant.
    private const int ChunkSize = 1000;

    // The number of items of a failure in the second chunk, the last of them the one that fails.
    private const int LaterFailureCount = 1500;

    // The id of the first note an insert adds, past the seed's.
    private const int FirstId = 101;

    private const string BatchNotes =
        "SELECT id, account_id, kind, body FROM " + SqlServerDatabases.BatchNotes + " ORDER BY id";

    // The isolation level of the session, as sys.dm_exec_sessions numbers it, which a method that begins its
    // transaction at a level sets back after it.
    private const string SessionLevel =
        "SELECT transaction_isolation_level AS level FROM sys.dm_exec_sessions WHERE session_id = @@SPID";

    // A note of the id FirstId, which the table of the copy starts without, so that a query's method given the id is
    // refused.
    private const string InsertFirst =
        "INSERT INTO "
        + SqlServerDatabases.BatchNotes
        + " (id, account_id, kind, body, written_at) VALUES (101, 1, 'call', N'Already here.', '2024-01-01')";

    private static readonly Dictionary<string, string> NotesToken = new(StringComparer.Ordinal)
    {
        ["notes"] = SqlServerDatabases.BatchNotes,
    };

    private static readonly Setup Writes = new(Writes: true, Tables: [BatchNotes]);

    private static readonly Setup WritesClosed = Writes with { Connection = ConnectionStart.Closed };

    private static readonly Setup WritesInTransaction = Writes with { Transaction = CallerTransaction.Begun };

    // A session of its own with XACT_ABORT ON, under which a duplicate key rolls back the transaction it is in; the
    // pool would hand the setting to the next scenario.
    private static readonly Setup XactAbort = Writes with { Source = "unpooled", Prepare = "SET XACT_ABORT ON" };

    // An empty collection writes nothing, so it runs on the seed, whose table it records all the same.
    private static readonly Setup NoWritesClosed = new(ConnectionStart.Closed, Tables: [BatchNotes]);

    private static readonly Func<DbConnection, DbDataSource, object> Repository = static (connection, _) =>
        new BatchQueries(connection);

    // The inserts of the connection: their transaction copies, then the other shapes of InsertNote.
    private static readonly string[] TransactionCopies =
    [
        "InsertNote",
        "InsertNoteDefaultLevel",
        "InsertNoteSerializable",
        "InsertNoteReadUncommitted",
        "InsertNoteReadCommitted",
        "InsertNoteRepeatableRead",
        "InsertNoteSnapshot",
        "InsertNoteNoTransaction",
    ];

    private static readonly string[] OtherInserts =
    [
        "InsertNoteBoth",
        "InsertNoteRowCount",
        "InsertNoteQuietly",
        "InsertNoteRowCountWithoutCounts",
        "InsertConvertedNote",
        "InsertConvertedNoteNoTransaction",
        "InsertNoteThenOther",
    ];

    private static readonly string[] RepositoryCopies =
    [
        "InsertNoteInRepository",
        "InsertNoteSerializableInRepository",
        "InsertNoteDefaultLevelInRepository",
    ];

    // The copies that begin their transaction at a level, whose rows hold it, and after which the session is read.
    private static readonly HashSet<string> AtALevel =
    [
        "InsertNoteDefaultLevel",
        "InsertNoteSerializable",
        "InsertNoteReadUncommitted",
        "InsertNoteReadCommitted",
        "InsertNoteRepeatableRead",
        "InsertNoteSnapshot",
        "InsertNoteSerializableInRepository",
        "InsertNoteDefaultLevelInRepository",
    ];

    // The inserts that return a count or nothing, which refuse no count.
    private static readonly HashSet<string> NoRowCountOf =
    [
        "InsertNoteRowCount",
        "InsertNoteQuietly",
        "InsertNoteRowCountWithoutCounts",
    ];

    public static IEnumerable<Scenario> All
    {
        get
        {
            var host = typeof(BatchQueriesExtensions);
            var scenarios = new List<Scenario>();
            foreach (var method in TransactionCopies.Concat(OtherInserts))
            {
                scenarios.AddRange(InsertScenarios(host, method, null));
            }

            foreach (var method in RepositoryCopies)
            {
                scenarios.AddRange(InsertScenarios(typeof(BatchQueries), method, Repository));
            }

            scenarios.AddRange(InsertFailures(host));
            scenarios.AddRange(Reads(host));
            scenarios.AddRange(OtherResultSets(host));
            return scenarios;
        }
    }

    // The query's method and the collection method of an insert, each on a copy of the seed.  The repository's methods
    // take no transaction, so they have no scenario in the caller's.  Every copy fails under XACT_ABORT ON at the third
    // of four items.
    private static IEnumerable<Scenario> InsertScenarios(
        Type host,
        string method,
        Func<DbConnection, DbDataSource, object>? instance
    )
    {
        var levels = AtALevel.Contains(method);
        Setup Recorded(Setup setup) => levels ? setup with { Tables = [BatchNotes, SessionLevel] } : setup;

        Scenario Query(ScenarioTag tag, string what, Setup setup, object parameters) =>
            Of(host, method, tag, what, Recorded(setup), parameters) with
            {
                Tokens = NotesToken,
                Instance = instance,
            };

        Scenario Collection(ScenarioTag tag, string what, Setup setup, object items) =>
            OfCollection(host, method, tag, what, Recorded(setup), items, instance) with
            {
                Tokens = NotesToken,
            };

        yield return Query(ScenarioTag.Ordinary, "open", Writes, Note(FirstId, 2));
        yield return Query(ScenarioTag.Ordinary, "closed", WritesClosed, Note(FirstId, 2));
        yield return NoRowCountOf.Contains(method)
            ? Query(ScenarioTag.Other, "no such account, closed", WritesClosed, Note(FirstId, 99))
            : Query(ScenarioTag.NoRow, "no such account, closed", WritesClosed, Note(FirstId, 99));
        yield return Query(
            ScenarioTag.DatabaseError,
            "a duplicate id, closed",
            WritesClosed with
            {
                Prepare = InsertFirst,
            },
            Note(FirstId, 2)
        );
        if (instance is null)
        {
            yield return Query(
                ScenarioTag.DatabaseError,
                "a duplicate id, in a transaction",
                WritesInTransaction with
                {
                    Prepare = InsertFirst,
                },
                Note(FirstId, 2)
            );
        }

        yield return Collection(ScenarioTag.Empty, "no items, closed", NoWritesClosed, Array.Empty<NoteToAdd>());
        yield return Collection(ScenarioTag.ChunkBoundary, "one full chunk, open", Writes, Notes(ChunkSize));
        yield return Collection(
            ScenarioTag.ChunkBoundary,
            "a second chunk of one item, closed",
            WritesClosed,
            Notes(ChunkSize + 1)
        );
        yield return Collection(
            ScenarioTag.LaterChunkFailure,
            "a duplicate id in the second chunk, open",
            Writes,
            LaterFailure()
        );
        if (instance is null)
        {
            yield return Collection(
                ScenarioTag.Ordinary,
                "two chunks, in a transaction",
                WritesInTransaction,
                Notes(ChunkSize + 1)
            );
        }

        if (TransactionCopies.Contains(method) || RepositoryCopies.Contains(method))
        {
            yield return Collection(
                ScenarioTag.DatabaseError,
                "a duplicate id at the third item under XACT_ABORT ON, open",
                XactAbort,
                DuplicateAtItemTwo()
            );
        }
    }

    // The failures of InsertNote, its copy under off and the conversions, as the end-to-end tests run them.
    private static IEnumerable<Scenario> InsertFailures(Type host)
    {
        Scenario Collection(string method, ScenarioTag tag, string what, Setup setup, object items) =>
            OfCollection(host, method, tag, what, setup, items) with
            {
                Tokens = NotesToken,
            };

        var noRowAtItemOne = new NoteToAdd[]
        {
            new(FirstId, "First.", 1),
            new(FirstId + 1, "Lost.", 99),
            new(FirstId + 2, "Third.", 2),
        };

        // An item with no row, which a one query refuses, rolls back the item before it.
        yield return Collection(
            "InsertNote",
            ScenarioTag.NoRow,
            "an item with no row, closed",
            WritesClosed,
            noRowAtItemOne
        );
        yield return Collection(
            "InsertNote",
            ScenarioTag.NoRow,
            "an item with no row, in a transaction",
            WritesInTransaction,
            noRowAtItemOne
        );
        yield return Collection(
            "InsertNote",
            ScenarioTag.LaterChunkFailure,
            "a duplicate id in the second chunk, closed",
            WritesClosed,
            LaterFailure()
        );
        yield return Collection(
            "InsertNote",
            ScenarioTag.LaterChunkFailure,
            "a duplicate id in the second chunk, in a transaction",
            WritesInTransaction,
            LaterFailure()
        );
        yield return Collection(
            "InsertNoteNoTransaction",
            ScenarioTag.LaterChunkFailure,
            "a duplicate id in the second chunk, in a transaction",
            WritesInTransaction,
            LaterFailure()
        );

        // A duplicate key at the third of four items: under off the others commit; in the caller's transaction it ends
        // only its statement with XACT_ABORT off, and rolls the transaction back with it on.
        yield return Collection(
            "InsertNoteNoTransaction",
            ScenarioTag.DatabaseError,
            "a duplicate id at the third item, closed",
            WritesClosed,
            DuplicateAtItemTwo()
        );
        yield return Collection(
            "InsertNote",
            ScenarioTag.DatabaseError,
            "a duplicate id at the third item, in a transaction",
            WritesInTransaction,
            DuplicateAtItemTwo()
        );
        yield return Collection(
            "InsertNote",
            ScenarioTag.DatabaseError,
            "a duplicate id at the third item under XACT_ABORT ON, in a transaction",
            XactAbort with
            {
                Transaction = CallerTransaction.Begun,
            },
            DuplicateAtItemTwo()
        );

        // A null item in the second chunk is found after the first chunk ran: its own transaction takes the first
        // chunk back under on, and under off the first chunk has committed.
        yield return Collection(
            "InsertNote",
            ScenarioTag.LaterChunkFailure,
            "a null item in the second chunk, open",
            Writes,
            WithNullAt(Notes(LaterFailureCount), LaterFailureCount - 1)
        );
        yield return Collection(
            "InsertNoteNoTransaction",
            ScenarioTag.LaterChunkFailure,
            "a null item in the second chunk, open",
            Writes,
            WithNullAt(Notes(LaterFailureCount), LaterFailureCount - 1)
        );

        // A completed transaction is refused before anything runs.
        yield return Of(
            host,
            "InsertNote",
            ScenarioTag.Other,
            "a completed transaction",
            CompletedTransaction with
            {
                Writes = true,
                Tables = [BatchNotes],
            },
            Note(FirstId, 2)
        ) with
        {
            Tokens = NotesToken,
        };
        yield return Collection(
            "InsertNote",
            ScenarioTag.Other,
            "a completed transaction",
            CompletedTransaction with
            {
                Writes = true,
                Tables = [BatchNotes],
            },
            Notes(3)
        );

        // The caller's transaction begun and not passed: under on the method cannot begin its own beside it, and under
        // off SqlClient refuses the command.
        yield return Collection(
            "InsertNote",
            ScenarioTag.Other,
            "a transaction not passed",
            TransactionNotPassed with
            {
                Writes = true,
                Tables = [BatchNotes],
            },
            Notes(3)
        );
        yield return Collection(
            "InsertNoteNoTransaction",
            ScenarioTag.Other,
            "a transaction not passed",
            TransactionNotPassed with
            {
                Writes = true,
                Tables = [BatchNotes],
            },
            Notes(3)
        );

        // A conversion that ends the batch: in the method's own transaction, which the server rolls back; under off,
        // after the item before it committed; and in the caller's transaction after an item's error or an item with
        // no row, which the method reports as a rollback.
        yield return Collection(
            "InsertConvertedNote",
            ScenarioTag.DatabaseError,
            "a conversion at the second item, open",
            Writes,
            Converted(3, 1)
        );
        yield return Collection(
            "InsertConvertedNote",
            ScenarioTag.DatabaseError,
            "a conversion at the second item, closed",
            WritesClosed,
            Converted(3, 1)
        );
        yield return Collection(
            "InsertConvertedNoteNoTransaction",
            ScenarioTag.DatabaseError,
            "a conversion at the second item, open",
            Writes,
            Converted(3, 1)
        );
        yield return Collection(
            "InsertConvertedNote",
            ScenarioTag.DatabaseError,
            "a duplicate id then a conversion, in a transaction",
            WritesInTransaction,
            new NoteToAdd[]
            {
                new(FirstId, "First.", 1),
                new(FirstId, "Again.", 2),
                new(FirstId + 2, "Third.", 3),
                new(FirstId + 3, "int:x", 1),
            }
        );
        yield return Collection(
            "InsertConvertedNote",
            ScenarioTag.NoRow,
            "an item with no row then a conversion, in a transaction",
            WritesInTransaction,
            new NoteToAdd[]
            {
                new(FirstId, "First.", 1),
                new(FirstId + 1, "Lost.", 99),
                new(FirstId + 2, "Third.", 3),
                new(FirstId + 3, "int:x", 1),
            }
        );
        yield return Of(
            host,
            "InsertConvertedNote",
            ScenarioTag.DatabaseError,
            "a conversion, in a transaction",
            WritesInTransaction,
            new NoteToAdd(FirstId, "int:x", 1)
        ) with
        {
            Tokens = NotesToken,
        };

        // A second result set of another width, refused at the second item after the first item's insert.
        yield return Collection("InsertNoteThenOther", ScenarioTag.Other, "two items, closed", WritesClosed, Notes(2));
        yield return Collection(
            "InsertNoteThenOther",
            ScenarioTag.Other,
            "two items, in a transaction",
            WritesInTransaction,
            Notes(2)
        );
    }

    // The reads, on the seed: alice's two notes, bob's one and carol's none.
    private static IEnumerable<Scenario> Reads(Type host)
    {
        foreach (var scenario in OpenAndClosed(host, "FindNotes", new FindNotesArgs(1)))
        {
            yield return scenario;
        }

        yield return Of(host, "FindNotes", ScenarioTag.Other, "no notes, closed", Closed, new FindNotesArgs(3));
        yield return Of(
            host,
            "FindNotes",
            ScenarioTag.Ordinary,
            "in a transaction",
            InTransaction,
            new FindNotesArgs(1)
        );
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
                static count => Enumerable.Range(0, count).Select(static i => new FindNotesArgs((i % 3) + 1)).ToArray()
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
                    Enumerable.Range(0, count).Select(static i => new FindNotesStreamBothArgs((i % 3) + 1)).ToArray()
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
                static count => Enumerable.Range(0, count).Select(static i => new FindNoteArgs((i % 4) + 1)).ToArray()
            )
        )
        {
            yield return scenario;
        }
    }

    // The queries whose statements return another result set, or none, for an item, which a collection method refuses
    // or misreads; the query's own method reads its first result set.  Each collection method runs the inputs of a
    // read, most of which fail at the second item, and the end-to-end tests' cases.
    private static List<Scenario> OtherResultSets(Type host)
    {
        var scenarios = new List<Scenario>
        {
            Of(host, "FindAccountThenOther", ScenarioTag.Ordinary, "open", Open, new FindAccountThenOtherArgs(1)),
            Of(host, "FindAccountThenOther", ScenarioTag.Ordinary, "closed", Closed, new FindAccountThenOtherArgs(2)),
            Of(host, "FindAccountWhenPositive", ScenarioTag.Ordinary, "open", Open, new FindAccountWhenPositiveArgs(1)),
            Of(
                host,
                "FindAccountWhenPositive",
                ScenarioTag.Other,
                "no result set, closed",
                Closed,
                new FindAccountWhenPositiveArgs(-1)
            ),
            Of(host, "FindAccountThenEmpty", ScenarioTag.Ordinary, "open", Open, new FindAccountThenEmptyArgs(1)),
            Of(host, "FindAccountThenEmpty", ScenarioTag.Ordinary, "closed", Closed, new FindAccountThenEmptyArgs(2)),
            Of(host, "GetAccountThenEmpty", ScenarioTag.Ordinary, "open", Open, new GetAccountThenEmptyArgs(1)),
            Of(
                host,
                "GetAccountThenEmpty",
                ScenarioTag.NoRow,
                "no account, closed",
                Closed,
                new GetAccountThenEmptyArgs(99)
            ),
            Of(host, "FindAccountThenText", ScenarioTag.Ordinary, "open", Open, new FindAccountThenTextArgs(1)),
            Of(host, "FindAccountThenText", ScenarioTag.Ordinary, "closed", Closed, new FindAccountThenTextArgs(3)),
            // One item: the second result set follows the last item's.
            OfCollection(
                host,
                "FindAccountThenOther",
                ScenarioTag.Other,
                "one item, closed",
                Closed,
                new[] { new FindAccountThenOtherArgs(1) }
            ),
            OfCollection(
                host,
                "FindAccountThenEmpty",
                ScenarioTag.Other,
                "one item, closed",
                Closed,
                new[] { new FindAccountThenEmptyArgs(1) }
            ),
            // An item without a result set: the last of two, the first of three, and the first of the second chunk's
            // two.
            OfCollection(
                host,
                "FindAccountWhenPositive",
                ScenarioTag.Other,
                "no result set for the last of two, closed",
                Closed,
                AccountsWithout(2, 1)
            ),
            OfCollection(
                host,
                "FindAccountWhenPositive",
                ScenarioTag.Other,
                "no result set for the first of three, open",
                Open,
                AccountsWithout(3, 0)
            ),
            OfCollection(
                host,
                "FindAccountWhenPositive",
                ScenarioTag.LaterChunkFailure,
                "no result set for the first of the second chunk, closed",
                Closed,
                AccountsWithout(ChunkSize + 2, ChunkSize)
            ),
        };

        scenarios.AddRange(
            ReadScenarios(
                host,
                "FindAccountThenOther",
                static count =>
                    Enumerable.Range(0, count).Select(static i => new FindAccountThenOtherArgs((i % 3) + 1)).ToArray()
            )
        );
        scenarios.AddRange(
            ReadScenarios(
                host,
                "FindAccountWhenPositive",
                static count =>
                    Enumerable
                        .Range(0, count)
                        .Select(static i => new FindAccountWhenPositiveArgs((i % 3) + 1))
                        .ToArray()
            )
        );
        scenarios.AddRange(
            ReadScenarios(
                host,
                "FindAccountThenEmpty",
                static count =>
                    Enumerable.Range(0, count).Select(static i => new FindAccountThenEmptyArgs((i % 3) + 1)).ToArray()
            )
        );
        scenarios.AddRange(
            ReadScenarios(
                host,
                "GetAccountThenEmpty",
                static count =>
                    Enumerable.Range(0, count).Select(static i => new GetAccountThenEmptyArgs((i % 3) + 1)).ToArray()
            )
        );
        scenarios.AddRange(
            ReadScenarios(
                host,
                "FindAccountThenText",
                static count =>
                    Enumerable.Range(0, count).Select(static i => new FindAccountThenTextArgs((i % 3) + 1)).ToArray()
            )
        );
        return scenarios;
    }

    // The collection method of a read, on the seed.  Its failure in the second chunk is a null item.
    private static IEnumerable<Scenario> ReadScenarios(Type host, string method, Func<int, Array> items) =>
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
                WithNullAt(items(LaterFailureCount), LaterFailureCount - 1)
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

    // A note with the id, to the account.
    private static NoteToAdd Note(int id, int account) => new(id, $"Note {id}.", account);

    // Notes with the ids from FirstId on, to the seed's three accounts in turn.
    private static NoteToAdd[] Notes(int count) =>
        [.. Enumerable.Range(0, count).Select(static i => Note(FirstId + i, (i % 3) + 1))];

    // Four notes whose third repeats the first's id.
    private static NoteToAdd[] DuplicateAtItemTwo()
    {
        var items = Notes(4);
        items[2] = items[2] with { Id = FirstId };
        return items;
    }

    // Notes whose last, item 1,500, in the second chunk, repeats the first's id, which the primary key refuses.
    private static NoteToAdd[] LaterFailure()
    {
        var items = Notes(LaterFailureCount);
        items[^1] = items[^1] with { Id = FirstId };
        return items;
    }

    // Notes whose item at the index has a body of int:x, whose conversion fails and ends the batch.
    private static NoteToAdd[] Converted(int count, int index)
    {
        var items = Notes(count);
        items[index] = items[index] with { Body = "int:x" };
        return items;
    }

    // The accounts 1 to 3 in turn, but the item at the index, whose id of -1 returns no result set.
    private static FindAccountWhenPositiveArgs[] AccountsWithout(int count, int missing) =>
        [.. Enumerable.Range(0, count).Select(i => new FindAccountWhenPositiveArgs(i == missing ? -1 : (i % 3) + 1))];

    private static Array WithNullAt(Array items, int index)
    {
        items.SetValue(null, index);
        return items;
    }
}
