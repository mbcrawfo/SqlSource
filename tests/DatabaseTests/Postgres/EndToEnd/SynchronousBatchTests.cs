using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Transactions;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The synchronous collection methods of the copies under method-type=sync of Queries/Batches.sql, run as
// BatchQueryTests runs their originals' asynchronous ones: each shape over one item, a few and more than 1,000, the
// rows of every item in the order of the items, an empty collection, a closed connection, which the method opens and
// closes again whether it returns or throws, a failure in the second chunk under each value of transaction that
// PostgreSQL has and in the caller's transaction, with what it leaves in the table, the item that
// CollectionItemException names, a null item, an item with no row, a constraint that fails at the commit, each
// isolation level and an ambient TransactionScope.  Then the copies under method-type=both: a query's two collection
// methods insert their items, and a stream's collection methods return arrays beside the stream, which has no
// synchronous method.  A test whose method commits, or that shows a rollback, makes a temporary table notes on its own
// connection first, as BatchQueryTests does, so that the inserts land there and the seed is left as it was; one that
// reads runs on the seed's notes, and one that gives the method a TransactionScope writes there and is rolled back with
// the scope.  Every insert writes the id it is given, from 101 on, past the seed's.
[Collection(UsesPostgres.Name)]
public sealed class SynchronousBatchTests(PostgresDatabases postgres)
{
    // The size of a batch, the generator's constant.
    private const int ChunkSize = 1000;

    // The id of the first note that a test adds.
    private const int FirstId = 101;

    // The temporary table notes, empty, with the columns, defaults, keys and identity of the seed's.
    private const string TemporaryNotes = "CREATE TEMPORARY TABLE notes (LIKE public.notes INCLUDING ALL)";

    private const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    // The seed's three notes, as BatchNote reads them.
    private static readonly BatchNote[] SeedNotes =
    [
        new(1, 1, "Asked for a larger plan."),
        new(2, 1, "Sent the quote."),
        new(3, 2, "Closed the account on site."),
    ];

    [Fact]
    public void InsertNoteSync_OneItem_ReturnsItsRow()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);

        var rows = connection.InsertNoteSync([new NoteToAdd(101, "Only.", 2)]);

        rows.ShouldBe([new BatchNote(101, 2, "Only.")]);
        CountNotes(connection).ShouldBe(1);
        ShouldBeInNoTransaction(connection);
    }

    [Fact]
    public void InsertNoteSync_ThreeItems_ReturnsTheirRowsInOrder()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        // Out of the order of their ids, so that the rows follow the items.
        NoteToAdd[] items = [new(103, "Third.", 2), new(101, "First.", 1), new(102, "Second.", 3)];

        var rows = connection.InsertNoteSync(items);

        rows.ShouldBe(RowsOf(items));
        connection.State.ShouldBe(ConnectionState.Open);
        CountNotes(connection).ShouldBe(3);
        ShouldBeInNoTransaction(connection);
    }

    // A full batch, and a second of one item.
    [Fact]
    public void InsertNoteSync_OneThousandAndOneItems_ReturnsAllInOrder()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        var items = Notes(ChunkSize + 1);

        var rows = connection.InsertNoteSync(items);

        rows.ShouldBe(RowsOf(items));
        CountNotes(connection).ShouldBe(ChunkSize + 1);
        ShouldBeInNoTransaction(connection);
    }

    [Fact]
    public void InsertNoteRowCountSync_OneItem_ReturnsOne()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);

        var count = connection.InsertNoteRowCountSync([new NoteToAdd(101, "Only.", 2)]);

        count.ShouldBe(1);
        CountNotes(connection).ShouldBe(1);
        ShouldBeInNoTransaction(connection);
    }

    // The middle item names an account that does not exist, so its insert adds no row, and the total is the sum of
    // each item's count, not the number of items.
    [Fact]
    public void InsertNoteRowCountSync_ThreeItems_ReturnsTheTotal()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 2)];

        var count = connection.InsertNoteRowCountSync(items);

        count.ShouldBe(2);
        CountNotes(connection).ShouldBe(2);
        ShouldBeInNoTransaction(connection);
    }

    // Two chunks, whose counts the method adds: the first of 1,000 and the second of one.
    [Fact]
    public void InsertNoteRowCountSync_OneThousandAndOneItems_SumsEveryChunk()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);

        var count = connection.InsertNoteRowCountSync(Notes(ChunkSize + 1));

        count.ShouldBe(ChunkSize + 1);
        CountNotes(connection).ShouldBe(ChunkSize + 1);
        ShouldBeInNoTransaction(connection);
    }

    // A none method returns nothing: the table holds a note for each item, none for an empty collection, and one for
    // each of two chunks' items.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(ChunkSize + 1)]
    public void InsertNoteQuietlySync_Items_AddsTheNoteOfEach(int count)
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);

        connection.InsertNoteQuietlySync(Notes(count));

        CountNotes(connection).ShouldBe(count);
        ShouldBeInNoTransaction(connection);
    }

    [Fact]
    public void FindNotesSync_OneItem_ReturnsItsRows()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var rows = connection.FindNotesSync([new FindNotesSyncArgs(1)]);

        rows.ShouldBe([SeedNotes[0], SeedNotes[1]]);
    }

    // Bob's one note, alice's two and carol's none, in the order of the items and not of the ids.  The connection is
    // closed, so the method opens it and closes it again.
    [Fact]
    public void FindNotesSync_ThreeItems_ReturnsEveryRowOfEachInOrder()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var rows = connection.FindNotesSync([
            new FindNotesSyncArgs(2),
            new FindNotesSyncArgs(1),
            new FindNotesSyncArgs(3),
        ]);

        rows.ShouldBe([SeedNotes[2], SeedNotes[0], SeedNotes[1]]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // Alice's two notes, bob's one and carol's none, in turn, over two chunks: the rows keep the order of the items.
    [Fact]
    public void FindNotesSync_OneThousandAndOneItems_ReturnsEveryRowOfEachInOrder()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();
        var accounts = Enumerable.Range(0, ChunkSize + 1).Select(static i => (i % 3) + 1).ToArray();

        var rows = connection.FindNotesSync(accounts.Select(static account => new FindNotesSyncArgs(account)));

        rows.ShouldBe(accounts.SelectMany(static account => SeedNotes.Where(note => note.AccountId == account)));
    }

    [Fact]
    public void FindNoteSync_OneItem_ReturnsItsRow()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var rows = connection.FindNoteSync([new FindNoteSyncArgs(2)]);

        rows.ShouldBe([SeedNotes[1]]);
    }

    [Fact]
    public void FindNoteSync_ThreeItemsOneWithNoRow_LeavesItOut()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var rows = connection.FindNoteSync([
            new FindNoteSyncArgs(3),
            new FindNoteSyncArgs(99),
            new FindNoteSyncArgs(1),
        ]);

        rows.ShouldBe([SeedNotes[2], SeedNotes[0]]);
    }

    // The ids 1 to 4 in turn, over two chunks: the three notes in order, and nothing for 4, which no note has.
    [Fact]
    public void FindNoteSync_OneThousandAndOneItems_LeavesOutTheMissingInOrder()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();
        var ids = Enumerable.Range(0, ChunkSize + 1).Select(static i => (i % 4) + 1).ToArray();

        var rows = connection.FindNoteSync(ids.Select(static id => new FindNoteSyncArgs(id)));

        rows.ShouldBe(ids.Where(static id => id <= 3).Select(static id => SeedNotes[id - 1]));
    }

    // An empty collection returns before the method opens the connection.
    [Fact]
    public void InsertNoteSync_Empty_ReturnsEmptyAndLeavesTheConnectionClosed()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var rows = connection.InsertNoteSync([]);

        rows.ShouldBeEmpty();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The one item names an account that does not exist, so its insert writes nothing and returns no row: the method
    // opened the connection, and closes it again as it throws.
    [Fact]
    public void InsertNoteSync_ItemWithNoRowOnAClosedConnection_ThrowsAndIsClosedAgain()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var thrown = Should.Throw<UnexpectedRowCountException>(() =>
            connection.InsertNoteSync([new NoteToAdd(101, "Lost.", 99)])
        );

        thrown.ItemIndex.ShouldBe(0);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The third item repeats the first's id, which the primary key refuses: the method throws the driver's exception
    // inside its own, at the third item, and rolls back its transaction, which held the first two.
    [Fact]
    public void InsertNoteSync_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwoAndLeavesNothing()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Second.", 2), new(101, "Again.", 3)];

        var thrown = Should.Throw<CollectionItemException>(() => connection.InsertNoteSync(items));

        thrown.QueryName.ShouldBe("InsertNoteSync");
        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        thrown.SqlState.ShouldBe("23505");
        connection.State.ShouldBe(ConnectionState.Open);
        CountNotes(connection).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // The first item repeats the id of a note already in the table, so the error is the first command's, and the
    // method runs without a reader: Npgsql 10 names no command of the error, and the method finds the item by the
    // commands that completed, of which there are none.
    [Fact]
    public void InsertNoteRowCountSync_DuplicateKeyAtItemZero_ThrowsCollectionItemExceptionAtZero()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        _ = connection.InsertNoteRowCountSync([new NoteToAdd(101, "Already there.", 1)]);
        NoteToAdd[] items = [new(101, "Again.", 1), new(102, "Second.", 2)];

        var thrown = Should.Throw<CollectionItemException>(() => connection.InsertNoteRowCountSync(items));

        thrown.QueryName.ShouldBe("InsertNoteRowCountSync");
        thrown.ItemIndex.ShouldBe(0);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        CountNotes(connection).ShouldBe(1);
    }

    // Item 1,200 repeats the first's id, in the second chunk, under the method's own transaction: it rolls back the
    // first chunk with the second.
    [Fact]
    public void InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteSync(DuplicateInTheSecondChunk())
        );

        thrown.ItemIndex.ShouldBe(1200);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        CountNotes(connection).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // With no transaction, Npgsql runs each batch in an implicit transaction of its own: the first batch commits, and
    // the error at item 1,200 rolls back the whole of the second, the 200 items before it included.
    [Fact]
    public void InsertNoteNoTransactionSync_DuplicateKeyInTheSecondChunk_KeepsTheFirstChunk()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteNoTransactionSync(DuplicateInTheSecondChunk())
        );

        thrown.ItemIndex.ShouldBe(1200);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        CountNotes(connection).ShouldBe(ChunkSize);
        MaxNoteId(connection).ShouldBe(FirstId + ChunkSize - 1);
        ShouldBeInNoTransaction(connection);
    }

    // The same failure under each isolation level that PostgreSQL has, each a copy of InsertNoteSerializableSync.
    [Theory]
    [InlineData("read committed")]
    [InlineData("repeatable read")]
    [InlineData("serializable")]
    public void InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing(string level)
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);

        var thrown = Should.Throw<CollectionItemException>(() =>
            InsertAtLevel(connection, level, DuplicateInTheSecondChunk())
        );

        thrown.ItemIndex.ShouldBe(1200);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        CountNotes(connection).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // The first batch runs before the null is found, as the method checks each item as it adds it to its batch, and
    // the rollback of the method's transaction takes the first batch's rows with it.
    [Fact]
    public void InsertNoteSync_NullItemInTheSecondChunk_RollsBackTheFirst()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        var items = Notes(1500);
        items[1499] = null!;

        var thrown = Should.Throw<ArgumentNullException>(() => connection.InsertNoteSync(items));

        thrown.ParamName.ShouldBe("parameters");
        thrown.Message.ShouldStartWith("Item 1499 of the collection is null.");
        CountNotes(connection).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // The same null under transaction=off: the first batch committed on its own before the null was found, so its rows
    // stay, which shows that the test above saw a rollback and not a batch that never ran.
    [Fact]
    public void InsertNoteNoTransactionSync_NullItemInTheSecondChunk_KeepsTheFirstChunk()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        var items = Notes(1500);
        items[1499] = null!;

        var thrown = Should.Throw<ArgumentNullException>(() => connection.InsertNoteNoTransactionSync(items));

        thrown.Message.ShouldStartWith("Item 1499 of the collection is null.");
        CountNotes(connection).ShouldBe(ChunkSize);
        ShouldBeInNoTransaction(connection);
    }

    // The middle item names an account that does not exist, so its insert returns no row, which a one query throws
    // for, and the rollback takes the first item's row with it.
    [Fact]
    public void InsertNoteSync_ItemWithNoRow_ThrowsUnexpectedRowCountAndRollsBack()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 2)];

        var thrown = Should.Throw<UnexpectedRowCountException>(() => connection.InsertNoteSync(items));

        thrown.QueryName.ShouldBe("InsertNoteSync");
        thrown.ItemIndex.ShouldBe(1);
        thrown.Message.ShouldBe("The query 'InsertNoteSync' returned no rows for item 1.");
        CountNotes(connection).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // A constraint checked at the commit fails after every item ran, at the method's own commit: no item raised it, so
    // the method throws the database's error as it is, and nothing is left.
    [Fact]
    public void InsertNoteSync_DeferredConstraintFailsAtTheCommit_ThrowsTheDatabasesError()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithDeferredTemporaryNotes(source);
        NoteToAdd[] items = [new(101, "First.", 1), new(101, "Again.", 2)];

        var thrown = Should.Throw<PostgresException>(() => connection.InsertNoteSync(items));

        thrown.SqlState.ShouldBe("23505");
        CountNotes(connection).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // The same constraint under transaction=off, at the commit of the implicit transaction that Npgsql runs the batch
    // in: the method throws the database's error as it is, and the batch is rolled back.
    [Fact]
    public void InsertNoteNoTransactionSync_DeferredConstraintFailsAtTheCommit_ThrowsTheDatabasesError()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithDeferredTemporaryNotes(source);
        NoteToAdd[] items = [new(101, "First.", 1), new(101, "Again.", 2)];

        var thrown = Should.Throw<PostgresException>(() => connection.InsertNoteNoTransactionSync(items));

        thrown.SqlState.ShouldBe("23505");
        CountNotes(connection).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // The method runs in the caller's transaction, whose own read sees the rows, and neither commits nor ends it: the
    // caller's rollback, which would throw on a transaction that had completed, takes the rows away.
    [Fact]
    public void InsertNoteSync_CallersTransaction_IsUsedAndNotCommitted()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        var items = Notes(3);
        using var transaction = connection.BeginTransaction();

        var rows = connection.InsertNoteSync(items, transaction);
        var during = CountNotes(connection);
        transaction.Rollback();

        rows.ShouldBe(RowsOf(items));
        during.ShouldBe(3);
        CountNotes(connection).ShouldBe(0);
    }

    // A duplicate key in the caller's transaction is thrown at its item, and the method leaves the transaction to the
    // caller, whose rollback takes the first two rows away.
    [Fact]
    public void InsertNoteSync_DuplicateKeyInTheCallersTransaction_LeavesItsRollbackToTheCaller()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Second.", 2), new(101, "Again.", 3)];
        using var transaction = connection.BeginTransaction();

        var thrown = Should.Throw<CollectionItemException>(() => connection.InsertNoteSync(items, transaction));
        transaction.Rollback();

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        CountNotes(connection).ShouldBe(0);
    }

    // PostgreSQL reports the level of the transaction that a statement runs in by the setting transaction_isolation.
    [Theory]
    [InlineData("read committed")]
    [InlineData("repeatable read")]
    [InlineData("serializable")]
    public void InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel(string level)
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);

        var rows = InsertAtLevel(connection, level, Notes(2));

        rows.ShouldBe([new(FirstId, level), new(FirstId + 1, level)]);
        CountNotes(connection).ShouldBe(2);
        ShouldBeInNoTransaction(connection);
    }

    // The connection, opened inside the scope, is enlisted in its transaction, so the method begins none of its own:
    // Npgsql refuses a second transaction on a connection that is in one, and a transaction of the method's own would
    // have committed the rows.  The scope ends without Complete, which rolls them back, so the seed's notes are left
    // as they were.
    [Fact]
    public void InsertNoteSync_InATransactionScope_BeginsNoTransactionOfItsOwn()
    {
        using var source = EndToEndSources.App(postgres);
        var items = Notes(3);
        IReadOnlyList<BatchNote> rows;
        long during;

        using (new TransactionScope())
        {
            using var connection = source.OpenConnection();
            rows = connection.InsertNoteSync(items);
            during = CountAddedNotes(connection);
        }

        rows.ShouldBe(RowsOf(items));
        during.ShouldBe(3);
        using var after = source.OpenConnection();
        CountAddedNotes(after).ShouldBe(0);
    }

    [Fact]
    public void InsertNoteBoth_ThreeItems_InsertsThem()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = OpenWithTemporaryNotes(source);
        var items = Notes(3);

        var rows = connection.InsertNoteBoth(items);

        rows.ShouldBe(RowsOf(items));
        CountNotes(connection).ShouldBe(3);
        ShouldBeInNoTransaction(connection);
    }

    // The asynchronous collection method beside the synchronous one, which the test above runs, awaited throughout.
    [Fact]
    public async Task InsertNoteBothAsync_ThreeItems_InsertsThem()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using (var create = new NpgsqlCommand(TemporaryNotes, connection))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        var items = Notes(3);

        var rows = await connection.InsertNoteBothAsync(items, cancellationToken: token);

        rows.ShouldBe(RowsOf(items));
        await using var count = new NpgsqlCommand("SELECT count(*) FROM notes", connection);
        ((long)(await count.ExecuteScalarAsync(token))!).ShouldBe(3);
    }

    // A stream's collection method returns an array, as the query has no list of its own to return.
    [Fact]
    public void FindNotesStreamBoth_TwoItems_ReturnsAnArrayOfEveryRowInOrder()
    {
        using var source = EndToEndSources.App(postgres);
        using var connection = source.CreateConnection();

        var rows = connection.FindNotesStreamBoth([new FindNotesStreamBothArgs(2), new FindNotesStreamBothArgs(1)]);

        rows.ShouldBe([SeedNotes[2], SeedNotes[0], SeedNotes[1]]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task FindNotesStreamBothAsync_TwoItems_ReturnsAnArrayOfEveryRowInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.FindNotesStreamBothAsync(
            [new FindNotesStreamBothArgs(2), new FindNotesStreamBothArgs(1)],
            cancellationToken: token
        );

        rows.ShouldBe([SeedNotes[2], SeedNotes[0], SeedNotes[1]]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The methods are internal, the project's default visibility, so the lookup takes the non-public ones too.  The
    // one synchronous method of the name is the collection method, which takes the items after the connection; the
    // stream is the asynchronous method that takes no items.
    [Fact]
    public void FindNotesStreamBoth_UnderBoth_HasNoSynchronousMethodWithoutItems()
    {
        var synchronous = typeof(BatchQueriesExtensions)
            .GetMethods(Statics)
            .Where(static method => method.Name == "FindNotesStreamBoth")
            .ToArray();
        var stream = typeof(BatchQueriesExtensions)
            .GetMethods(Statics)
            .Where(static method =>
                method.Name == "FindNotesStreamBothAsync" && method.ReturnType == typeof(IAsyncEnumerable<BatchNote>)
            )
            .ToArray();

        synchronous.Single().GetParameters()[1].ParameterType.ShouldBe(typeof(IEnumerable<FindNotesStreamBothArgs>));
        stream.Single().GetParameters()[1].ParameterType.ShouldBe(typeof(FindNotesStreamBothArgs));
    }

    // The copy of InsertNoteSerializableSync at the level that transaction_isolation names.
    private static IReadOnlyList<IsolatedNote> InsertAtLevel(
        NpgsqlConnection connection,
        string level,
        NoteToAdd[] items
    ) =>
        level switch
        {
            "read committed" => connection.InsertNoteReadCommittedSync(items),
            "repeatable read" => connection.InsertNoteRepeatableReadSync(items),
            "serializable" => connection.InsertNoteSerializableSync(items),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "No copy runs at this level."),
        };

    // 1,500 notes whose item 1,200, in the second chunk, repeats the first's id.
    private static NoteToAdd[] DuplicateInTheSecondChunk()
    {
        var items = Notes(1500);
        items[1200] = items[1200] with { Id = FirstId };
        return items;
    }

    // Npgsql refuses a transaction on a connection that is in one, so a method that left its own open fails this.
    private static void ShouldBeInNoTransaction(NpgsqlConnection connection)
    {
        using var transaction = connection.BeginTransaction();
        transaction.Rollback();
    }

    // Notes with the ids from FirstId on, to the seed's three accounts in turn.
    private static NoteToAdd[] Notes(int count) =>
        [.. Enumerable.Range(0, count).Select(static i => new NoteToAdd(FirstId + i, "Note " + i + ".", (i % 3) + 1))];

    private static BatchNote[] RowsOf(IEnumerable<NoteToAdd> items) =>
        [.. items.Select(static item => new BatchNote(item.Id, item.AccountId, item.Body))];

    // A connection of the source with a temporary table notes, empty, with the columns, defaults, keys and identity of
    // the seed's, which the queries' notes names until the connection closes.
    private static NpgsqlConnection OpenWithTemporaryNotes(NpgsqlDataSource source)
    {
        var connection = source.OpenConnection();
        using var create = new NpgsqlCommand(TemporaryNotes, connection);
        _ = create.ExecuteNonQuery();
        return connection;
    }

    // The same table with its key checked at the commit, and not at each insert.
    private static NpgsqlConnection OpenWithDeferredTemporaryNotes(NpgsqlDataSource source)
    {
        var connection = source.OpenConnection();
        using var create = new NpgsqlCommand(
            "CREATE TEMPORARY TABLE notes (LIKE public.notes INCLUDING DEFAULTS INCLUDING IDENTITY, "
                + "UNIQUE (id) DEFERRABLE INITIALLY DEFERRED)",
            connection
        );
        _ = create.ExecuteNonQuery();
        return connection;
    }

    private static long CountNotes(NpgsqlConnection connection)
    {
        using var count = new NpgsqlCommand("SELECT count(*) FROM notes", connection);
        return (long)count.ExecuteScalar()!;
    }

    private static int MaxNoteId(NpgsqlConnection connection)
    {
        using var max = new NpgsqlCommand("SELECT max(id) FROM notes", connection);
        return (int)max.ExecuteScalar()!;
    }

    // The notes of the seed's table that a test added, whose ids are from FirstId on.
    private static long CountAddedNotes(NpgsqlConnection connection)
    {
        using var count = new NpgsqlCommand("SELECT count(*) FROM notes WHERE id >= $1", connection);
        _ = count.Parameters.Add(new NpgsqlParameter<int> { TypedValue = FirstId });
        return (long)count.ExecuteScalar()!;
    }
}
