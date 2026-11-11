using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Npgsql;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The collection methods of Queries/Batches.sql, run through BatchQueries: each runs its query once for each item, in
// batches of 1,000, and returns the rows of every item in the order of the items.  A method that is given no
// transaction begins its own and commits it, so a test that lets it commit, or shows that it rolled back, makes a
// temporary table notes on its own connection first: PostgreSQL searches the session's temporary tables before public,
// so the inserts land there and the seed is left as it was, and the table outlives the method's transaction, so the
// test reads what the method left in it, and then begins a transaction of its own, which Npgsql would refuse on a
// connection that the method left in one.  A test that reads, or that gives the method an ambient transaction, runs on
// the seed's notes, and one that writes there rolls back.  Every insert writes the id it is given, from 101 on, past
// the seed's.
//
// A failure is run under each value of transaction that PostgreSQL has and under the caller's transaction: on, the
// default, by InsertNote, at an item, in the second chunk, at a null item, at an item with no row and, by
// InsertNoteAfterLock, at a cancellation; off by InsertNoteNoTransaction, in the second chunk, at a null item and at
// the commit; each isolation level by its copy in the second chunk; and the caller's transaction at an item.
[Collection(UsesPostgres.Name)]
public sealed class BatchQueryTests(PostgresDatabases postgres)
{
    // The size of a batch, the generator's constant.
    private const int ChunkSize = 1000;

    // The id of the first note that a test adds.
    private const int FirstId = 101;

    // The advisory lock that InsertNoteAfterLock's held item waits for, which the cancellation test's second connection
    // holds; RunTimeTests takes another.
    private const long HeldLock = 113;

    // The advisory lock that the items before the held one take, which no connection holds.
    private const long FreeLock = 114;

    // The seed's three notes, as BatchNote reads them.
    private static readonly BatchNote[] SeedNotes =
    [
        new(1, 1, "Asked for a larger plan."),
        new(2, 1, "Sent the quote."),
        new(3, 2, "Closed the account on site."),
    ];

    [Fact]
    public async Task InsertNote_ThreeItems_ReturnsTheirRowsInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        // Out of the order of their ids, so that the rows follow the items.
        NoteToAdd[] items = [new(103, "Third.", 2), new(101, "First.", 1), new(102, "Second.", 3)];

        var rows = await connection.InsertNote(items, cancellationToken: token);

        rows.ShouldBe(RowsOf(items));
        connection.State.ShouldBe(ConnectionState.Open);
        (await CountNotesAsync(connection, token)).ShouldBe(3);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    [Fact]
    public async Task InsertNote_OneItem_ReturnsItsRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);

        var rows = await connection.InsertNote([new NoteToAdd(101, "Only.", 2)], cancellationToken: token);

        rows.ShouldBe([new BatchNote(101, 2, "Only.")]);
        (await CountNotesAsync(connection, token)).ShouldBe(1);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The middle item names an account that does not exist, so its insert adds no row, and the total is the sum of
    // each item's count, not the number of items.
    [Fact]
    public async Task InsertNoteRowCount_ThreeItems_ReturnsTheTotal()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 2)];

        var count = await connection.InsertNoteRowCount(items, cancellationToken: token);

        count.ShouldBe(2);
        (await CountNotesAsync(connection, token)).ShouldBe(2);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    [Fact]
    public async Task InsertNoteRowCount_OneItem_ReturnsOne()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);

        var count = await connection.InsertNoteRowCount([new NoteToAdd(101, "Only.", 2)], cancellationToken: token);

        count.ShouldBe(1);
        (await CountNotesAsync(connection, token)).ShouldBe(1);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // Two chunks, whose counts the method adds: the first of 1,000 and the second of one.
    [Fact]
    public async Task InsertNoteRowCount_OneThousandAndOneItems_SumsEveryChunk()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);

        var count = await connection.InsertNoteRowCount(Notes(ChunkSize + 1), cancellationToken: token);

        count.ShouldBe(ChunkSize + 1);
        (await CountNotesAsync(connection, token)).ShouldBe(ChunkSize + 1);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // A none method returns nothing: the table holds a note for each item, none for an empty collection, and one for
    // each of two chunks' items.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(ChunkSize + 1)]
    public async Task InsertNoteQuietly_Items_AddsTheNoteOfEach(int count)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);

        await connection.InsertNoteQuietly(Notes(count), cancellationToken: token);

        (await CountNotesAsync(connection, token)).ShouldBe(count);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // Bob's one note, then alice's two, in the order of the items and not of the ids.  The connection is closed, so the
    // method opens it and closes it again.
    [Fact]
    public async Task FindNotes_TwoItems_ReturnsEveryRowOfEachInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.FindNotes([new FindNotesArgs(2), new FindNotesArgs(1)], cancellationToken: token);

        rows.ShouldBe([SeedNotes[2], SeedNotes[0], SeedNotes[1]]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task FindNotes_OneItem_ReturnsItsRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.FindNotes([new FindNotesArgs(1)], cancellationToken: token);

        rows.ShouldBe([SeedNotes[0], SeedNotes[1]]);
    }

    // Alice's two notes, bob's one and carol's none, in turn, over two chunks: the rows keep the order of the items.
    [Fact]
    public async Task FindNotes_OneThousandAndOneItems_ReturnsEveryRowOfEachInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var accounts = Enumerable.Range(0, ChunkSize + 1).Select(static i => (i % 3) + 1).ToArray();

        var rows = await connection.FindNotes(
            accounts.Select(static account => new FindNotesArgs(account)),
            cancellationToken: token
        );

        rows.ShouldBe(accounts.SelectMany(static account => SeedNotes.Where(note => note.AccountId == account)));
    }

    [Fact]
    public async Task FindNote_ItemWithNoRow_IsLeftOut()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.FindNote(
            [new FindNoteArgs(3), new FindNoteArgs(99), new FindNoteArgs(1)],
            cancellationToken: token
        );

        rows.ShouldBe([SeedNotes[2], SeedNotes[0]]);
    }

    [Fact]
    public async Task FindNote_OneItem_ReturnsItsRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.FindNote([new FindNoteArgs(2)], cancellationToken: token);

        rows.ShouldBe([SeedNotes[1]]);
    }

    // The ids 1 to 4 in turn, over two chunks: the three notes in order, and nothing for 4, which no note has.
    [Fact]
    public async Task FindNote_OneThousandAndOneItems_LeavesOutTheMissingInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();
        var ids = Enumerable.Range(0, ChunkSize + 1).Select(static i => (i % 4) + 1).ToArray();

        var rows = await connection.FindNote(ids.Select(static id => new FindNoteArgs(id)), cancellationToken: token);

        rows.ShouldBe(ids.Where(static id => id <= 3).Select(static id => SeedNotes[id - 1]));
    }

    // An empty collection returns before the method opens the connection.
    [Fact]
    public async Task InsertNote_Empty_ReturnsEmptyAndLeavesTheConnectionClosed()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = source.CreateConnection();

        var rows = await connection.InsertNote([], cancellationToken: token);

        rows.ShouldBeEmpty();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // One full batch, and no second.
    [Fact]
    public async Task InsertNote_OneThousandItems_ReturnsAllInOrder() => await InsertManyAsync(ChunkSize);

    // A full batch, and a second of one item.
    [Fact]
    public async Task InsertNote_OneThousandAndOneItems_ReturnsAllInOrder() => await InsertManyAsync(ChunkSize + 1);

    // The third item repeats the first's id, which the primary key refuses: the method throws the driver's exception
    // inside its own, at the third item, and rolls back its transaction, which held the first two.
    [Fact]
    public async Task InsertNote_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwoAndLeavesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Second.", 2), new(101, "Again.", 3)];

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(items, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("InsertNote");
        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        thrown.SqlState.ShouldBe("23505");
        connection.State.ShouldBe(ConnectionState.Open);
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // The first item repeats the id of a note already in the table, so the error is the first command's, and the
    // method runs without a reader: Npgsql 10 names no command of the error, and the method finds the item by the
    // commands that completed, of which there are none.
    [Fact]
    public async Task InsertNoteRowCount_DuplicateKeyAtItemZero_ThrowsCollectionItemExceptionAtZero()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        _ = await connection.InsertNoteRowCount([new NoteToAdd(101, "Already there.", 1)], cancellationToken: token);
        NoteToAdd[] items = [new(101, "Again.", 1), new(102, "Second.", 2)];

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNoteRowCount(items, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("InsertNoteRowCount");
        thrown.ItemIndex.ShouldBe(0);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        (await CountNotesAsync(connection, token)).ShouldBe(1);
    }

    // The first batch runs before the null is found, as the method checks each item as it adds it to its batch, and
    // the rollback of the method's transaction takes the first batch's rows with it.
    [Fact]
    public async Task NullItemInTheSecondChunk_RollsBackTheFirst()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        var items = Notes(1500);
        items[1499] = null!;

        var thrown = await Should.ThrowAsync<ArgumentNullException>(() =>
            connection.InsertNote(items, cancellationToken: token)
        );

        thrown.ParamName.ShouldBe("parameters");
        thrown.Message.ShouldStartWith("Item 1499 of the collection is null.");
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // The same null under transaction=off: the first batch committed on its own before the null was found, so its rows
    // stay, which shows that the test above saw a rollback and not a batch that never ran.
    [Fact]
    public async Task InsertNoteNoTransaction_NullItemInTheSecondChunk_KeepsTheFirstChunk()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        var items = Notes(1500);
        items[1499] = null!;

        var thrown = await Should.ThrowAsync<ArgumentNullException>(() =>
            connection.InsertNoteNoTransaction(items, cancellationToken: token)
        );

        thrown.Message.ShouldStartWith("Item 1499 of the collection is null.");
        (await CountNotesAsync(connection, token)).ShouldBe(ChunkSize);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The middle item names an account that does not exist, so its insert returns no row, which a one query throws
    // for, and the rollback takes the first item's row with it.
    [Fact]
    public async Task One_ItemWithNoRow_ThrowsUnexpectedRowCountAndRollsBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 2)];

        var thrown = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.InsertNote(items, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("InsertNote");
        thrown.ItemIndex.ShouldBe(1);
        thrown.Message.ShouldBe("The query 'InsertNote' returned no rows for item 1.");
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // With no transaction, Npgsql runs each batch in an implicit transaction of its own: the first batch commits, and
    // the error at item 1,200 rolls back the whole of the second, the 200 items before it included.
    [Fact]
    public async Task InsertNoteNoTransaction_FailureInTheSecondChunk_KeepsTheFirstChunk()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNoteNoTransaction(DuplicateInTheSecondChunk(), cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(1200);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        (await CountNotesAsync(connection, token)).ShouldBe(ChunkSize);
        (await MaxNoteIdAsync(connection, token)).ShouldBe(FirstId + ChunkSize - 1);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // Item 1,200 repeats the first's id, in the second chunk, under the method's own transaction: it rolls back the
    // first chunk with the second.
    [Fact]
    public async Task InsertNote_DuplicateKeyInTheSecondChunk_LeavesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(DuplicateInTheSecondChunk(), cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(1200);
        (await CountNotesAsync(connection, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The same failure under each isolation level that PostgreSQL has, each a copy of InsertNoteSerializable.
    [Theory]
    [InlineData("read committed")]
    [InlineData("repeatable read")]
    [InlineData("serializable")]
    public async Task InsertNoteAtALevel_DuplicateKeyInTheSecondChunk_LeavesNothing(string level)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            InsertAtLevelAsync(connection, level, DuplicateInTheSecondChunk(), token)
        );

        thrown.ItemIndex.ShouldBe(1200);
        (await CountNotesAsync(connection, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // A constraint checked at the commit fails after every command of the batch completed, at the commit of the
    // implicit transaction that Npgsql runs the batch in under transaction=off.  No item raised it, so the method
    // throws the database's error as it is, and the batch is rolled back.
    [Fact]
    public async Task InsertNoteNoTransaction_DeferredConstraintFailsAtTheCommit_ThrowsTheDatabasesError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await source.OpenConnectionAsync(token);
        await using (
            var create = new NpgsqlCommand(
                "CREATE TEMPORARY TABLE notes (LIKE public.notes INCLUDING DEFAULTS INCLUDING IDENTITY, "
                    + "UNIQUE (id) DEFERRABLE INITIALLY DEFERRED)",
                connection
            )
        )
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        NoteToAdd[] items = [new(101, "First.", 1), new(101, "Again.", 2)];

        var thrown = await Should.ThrowAsync<PostgresException>(() =>
            connection.InsertNoteNoTransaction(items, cancellationToken: token)
        );

        thrown.SqlState.ShouldBe("23505");
        (await CountNotesAsync(connection, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The method runs in the caller's transaction, whose own read sees the rows, and neither commits nor ends it: the
    // caller's rollback, which would throw on a transaction that had completed, takes the rows away.
    [Fact]
    public async Task InsertNote_CallersTransaction_IsUsedAndNotCommitted()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        var items = Notes(3);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var rows = await connection.InsertNote(items, transaction, cancellationToken: token);
        var during = await CountNotesAsync(connection, token);
        await transaction.RollbackAsync(token);

        rows.ShouldBe(RowsOf(items));
        during.ShouldBe(3);
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // A duplicate key in the caller's transaction is thrown at its item, and the method leaves the transaction to the
    // caller, whose rollback takes the first two rows away.
    [Fact]
    public async Task InsertNote_DuplicateKeyInTheCallersTransaction_LeavesItsRollbackToTheCaller()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Second.", 2), new(101, "Again.", 3)];
        await using var transaction = await connection.BeginTransactionAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(items, transaction, cancellationToken: token)
        );
        await transaction.RollbackAsync(token);

        thrown.ItemIndex.ShouldBe(2);
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // The first chunk's items take a lock that no one holds, and the last item, in the second chunk, waits for one that
    // a second connection holds, until the token is cancelled: the method's transaction is rolled back, the first
    // chunk's rows with it.  The source has no command timeout, so only the token ends the wait.
    [Fact]
    public async Task InsertNoteAfterLock_TokenCancelledInTheSecondChunk_RollsBackTheFirst()
    {
        var token = TestContext.Current.CancellationToken;
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token);
        await using var source = EndToEndSources.AppWithoutCommandTimeout(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        await using var holder = await source.OpenConnectionAsync(token);
        await using var heldLock = await RunTimeTests.HoldAsync(
            holder,
            "SELECT pg_advisory_xact_lock($1)",
            HeldLock,
            token
        );
        var items = Enumerable
            .Range(0, ChunkSize + 1)
            .Select(static i => new InsertNoteAfterLockArgs(
                FirstId + i,
                "Note " + i + ".",
                i < ChunkSize ? FreeLock : HeldLock,
                (i % 3) + 1
            ))
            .ToArray();

        _ = await Should.ThrowAsync<OperationCanceledException>(async () =>
        {
            // The wait cancels the token however it ends, so the call never outlives it, and the call is awaited on
            // every path.
            var cancelOnceWaiting = RunTimeTests.CancelOnceWaitingAsync(source, holder.ProcessID, cancel, token);
            try
            {
                _ = await Eventually.WithinAsync(
                    connection.InsertNoteAfterLock(items, cancellationToken: cancel.Token),
                    "the collection method outlasted the cancellation of its token"
                );
            }
            finally
            {
                await cancelOnceWaiting;
            }
        });

        (await CountNotesAsync(connection, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The connection, opened inside the scope, is enlisted in its transaction, so the method begins none of its own:
    // Npgsql refuses a second transaction on a connection that is in one, and a transaction of the method's own would
    // have committed the rows.  The scope ends without Complete, which rolls them back, so the seed's notes are left
    // as they were.
    [Fact]
    public async Task InsertNote_InATransactionScope_BeginsNoTransactionOfItsOwn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        var items = Notes(3);
        IReadOnlyList<BatchNote> rows;
        long during;

        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            await using var connection = await source.OpenConnectionAsync(token);
            rows = await connection.InsertNote(items, cancellationToken: token);
            during = await CountAddedNotesAsync(connection, token);
        }

        rows.ShouldBe(RowsOf(items));
        during.ShouldBe(3);
        await using var after = await source.OpenConnectionAsync(token);
        (await CountAddedNotesAsync(after, token)).ShouldBe(0);
    }

    // PostgreSQL reports the level of the transaction that a statement runs in by the setting transaction_isolation.
    [Fact]
    public async Task InsertNoteSerializable_RunsAtSerializable()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);

        var rows = await connection.InsertNoteSerializable(Notes(2), cancellationToken: token);

        rows.ShouldBe([new(FirstId, "serializable"), new(FirstId + 1, "serializable")]);
        (await CountNotesAsync(connection, token)).ShouldBe(2);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The two other levels, each by its copy of InsertNoteSerializable.
    [Theory]
    [InlineData("read committed")]
    [InlineData("repeatable read")]
    public async Task InsertNoteAtALevel_TwoItems_RunsAtThatLevel(string level)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);

        var rows = await InsertAtLevelAsync(connection, level, Notes(2), token);

        rows.ShouldBe([new(FirstId, level), new(FirstId + 1, level)]);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The repository's method takes no transaction and begins its own, and commits it before it returns: a transaction
    // that it disposed uncommitted would have rolled the rows back.  The borrowed connection is open, with the
    // temporary table, so the method leaves it open.
    [Fact]
    public async Task Repository_InsertNote_CommitsItsOwnTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        var items = Notes(3);

        var rows = await new BatchQueries(connection).InsertNoteInRepository(items, token);

        rows.ShouldBe(RowsOf(items));
        connection.State.ShouldBe(ConnectionState.Open);
        (await CountNotesAsync(connection, token)).ShouldBe(3);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    private async Task InsertManyAsync(int count)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = EndToEndSources.App(postgres);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        var items = Notes(count);

        var rows = await connection.InsertNote(items, cancellationToken: token);

        rows.ShouldBe(RowsOf(items));
        (await CountNotesAsync(connection, token)).ShouldBe(count);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The copy of InsertNoteSerializable at the level that transaction_isolation names.
    private static Task<IReadOnlyList<IsolatedNote>> InsertAtLevelAsync(
        NpgsqlConnection connection,
        string level,
        NoteToAdd[] items,
        CancellationToken token
    ) =>
        level switch
        {
            "read committed" => connection.InsertNoteReadCommitted(items, cancellationToken: token),
            "repeatable read" => connection.InsertNoteRepeatableRead(items, cancellationToken: token),
            "serializable" => connection.InsertNoteSerializable(items, cancellationToken: token),
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
    private static async Task ShouldBeInNoTransactionAsync(NpgsqlConnection connection, CancellationToken token)
    {
        await using var transaction = await connection.BeginTransactionAsync(token);
        await transaction.RollbackAsync(token);
    }

    // Notes with the ids from FirstId on, to the seed's three accounts in turn.
    private static NoteToAdd[] Notes(int count) =>
        [.. Enumerable.Range(0, count).Select(static i => new NoteToAdd(FirstId + i, "Note " + i + ".", (i % 3) + 1))];

    private static BatchNote[] RowsOf(IEnumerable<NoteToAdd> items) =>
        [.. items.Select(static item => new BatchNote(item.Id, item.AccountId, item.Body))];

    // A connection of the source with a temporary table notes, empty, with the columns, defaults, keys and identity of
    // the seed's, which the queries' notes names until the connection closes.
    private static async Task<NpgsqlConnection> OpenWithTemporaryNotesAsync(
        NpgsqlDataSource source,
        CancellationToken token
    )
    {
        var connection = await source.OpenConnectionAsync(token);
        await using var create = new NpgsqlCommand(
            "CREATE TEMPORARY TABLE notes (LIKE public.notes INCLUDING ALL)",
            connection
        );
        _ = await create.ExecuteNonQueryAsync(token);
        return connection;
    }

    private static async Task<long> CountNotesAsync(NpgsqlConnection connection, CancellationToken token)
    {
        await using var count = new NpgsqlCommand("SELECT count(*) FROM notes", connection);
        return (long)(await count.ExecuteScalarAsync(token))!;
    }

    private static async Task<int> MaxNoteIdAsync(NpgsqlConnection connection, CancellationToken token)
    {
        await using var max = new NpgsqlCommand("SELECT max(id) FROM notes", connection);
        return (int)(await max.ExecuteScalarAsync(token))!;
    }

    // The notes of the seed's table that a test added, whose ids are from FirstId on.
    private static async Task<long> CountAddedNotesAsync(NpgsqlConnection connection, CancellationToken token)
    {
        await using var count = new NpgsqlCommand("SELECT count(*) FROM notes WHERE id >= $1", connection);
        _ = count.Parameters.Add(new NpgsqlParameter<int> { TypedValue = FirstId });
        return (long)(await count.ExecuteScalarAsync(token))!;
    }
}
