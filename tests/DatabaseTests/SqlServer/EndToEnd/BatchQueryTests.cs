using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient;
using Shouldly;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The collection methods of Queries/Batches.sql, run through BatchQueries: each runs its query once for each item, in
// batches of 1,000, and returns the rows of every item in the order of the items.  Each insert names its table by the
// token notes, and a test that writes passes Notes, a temporary table that it makes on its own connection before any
// transaction, so that a method that commits leaves the seed as it was, and the table outlives a rollback and holds
// what was committed.  A test that reads runs on the seed's notes.  Every insert writes the id it is given, from 101
// on.
//
// A failure is run under each value of transaction and under the caller's transaction: on, the default, by InsertNote
// at an item, in the second chunk, at a null item, at an item with no row, under XACT_ABORT ON, at a cancellation, and
// by InsertConvertedNote at a conversion that ends the batch; off by InsertNoteNoTransaction at an item, which keeps
// the items before and after it, in the second of three chunks, at a null item and under XACT_ABORT ON; each isolation
// level by its copy in the second chunk; and the caller's transaction at an item, with XACT_ABORT off and on, and with
// a later conversion that rolls it back after an item's error and after an exception of the method's own.
[Collection(UsesSqlServer.Name)]
public sealed class BatchQueryTests(SqlServerDatabases sqlServer)
{
    // The size of a batch, the generator's constant.
    private const int ChunkSize = 1000;

    // The id of the first note that a test adds.
    private const int FirstId = 101;

    // The temporary table that the inserts write into, by their token.
    private const string Notes = "#notes";

    // The error numbers of a duplicate key and of a failed conversion.
    private const int DuplicateKey = 2627;

    private const int ConversionFailed = 245;

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
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        // Out of the order of their ids, so that the rows follow the items.
        NoteToAdd[] items = [new(103, "Third.", 2), new(101, "First.", 1), new(102, "Second.", 3)];

        var rows = await connection.InsertNote(items, Notes, cancellationToken: token);

        rows.ShouldBe(RowsOf(items));
        connection.State.ShouldBe(ConnectionState.Open);
        (await IdsAsync(connection, null, token)).ShouldBe([101, 102, 103]);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    [Fact]
    public async Task InsertNote_OneItem_ReturnsItsRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var rows = await connection.InsertNote([new NoteToAdd(101, "Only.", 2)], Notes, cancellationToken: token);

        rows.ShouldBe([new BatchNote(101, 2, "Only.")]);
        (await CountNotesAsync(connection, null, token)).ShouldBe(1);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // An empty collection returns before the method opens the connection.
    [Fact]
    public async Task InsertNote_Empty_ReturnsEmptyAndLeavesTheConnectionClosed()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await connection.InsertNote([], Notes, cancellationToken: token);

        rows.ShouldBeEmpty();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // One full batch, and no second.
    [Fact]
    public async Task InsertNote_OneThousandItems_ReturnsAllInOrder() => await InsertManyAsync(ChunkSize);

    // A full batch, and a second of one item.
    [Fact]
    public async Task InsertNote_OneThousandAndOneItems_ReturnsAllInOrder() => await InsertManyAsync(ChunkSize + 1);

    // The middle item names an account that does not exist, so its insert adds no row, and the total is the sum of
    // each item's count, not the number of items.
    [Fact]
    public async Task InsertNoteRowCount_ThreeItems_ReturnsTheTotal()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 2)];

        var count = await connection.InsertNoteRowCount(items, Notes, cancellationToken: token);

        count.ShouldBe(2);
        (await IdsAsync(connection, null, token)).ShouldBe([101, 103]);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // No item, which returns 0, one, and two chunks, whose counts the method adds: the first of 1,000 and the second of
    // one.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(ChunkSize + 1)]
    public async Task InsertNoteRowCount_Items_ReturnsTheirCount(int count)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var total = await connection.InsertNoteRowCount(Items(count), Notes, cancellationToken: token);

        total.ShouldBe(count);
        (await CountNotesAsync(connection, null, token)).ShouldBe(count);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // SET NOCOUNT ON leaves every command without a count, a RecordsAffected of -1, which the method adds as 0: the
    // notes are written, and the total is 0.
    [Fact]
    public async Task InsertNoteRowCountWithoutCounts_ThreeItems_ReturnsZero()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var total = await connection.InsertNoteRowCountWithoutCounts(Items(3), Notes, cancellationToken: token);

        total.ShouldBe(0);
        (await CountNotesAsync(connection, null, token)).ShouldBe(3);
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
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        await connection.InsertNoteQuietly(Items(count), Notes, cancellationToken: token);

        (await CountNotesAsync(connection, null, token)).ShouldBe(count);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // Bob's one note, then alice's two, in the order of the items and not of the ids.  The connection is closed, so the
    // method opens it and closes it again.
    [Fact]
    public async Task FindNotes_TwoItems_ReturnsEveryRowOfEachInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await connection.FindNotes([new FindNotesArgs(2), new FindNotesArgs(1)], cancellationToken: token);

        rows.ShouldBe([SeedNotes[2], SeedNotes[0], SeedNotes[1]]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task FindNotes_OneItem_ReturnsItsRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await connection.FindNotes([new FindNotesArgs(1)], cancellationToken: token);

        rows.ShouldBe([SeedNotes[0], SeedNotes[1]]);
    }

    [Fact]
    public async Task FindNotes_Empty_ReturnsEmpty()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await connection.FindNotes([], cancellationToken: token);

        rows.ShouldBeEmpty();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // Alice's two notes, bob's one and carol's none, in turn, over two chunks: the rows keep the order of the items,
    // and carol's empty result set takes its place.
    [Fact]
    public async Task FindNotes_OneThousandAndOneItems_ReturnsEveryRowOfEachInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
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
        await using var connection = EndToEndSources.App(sqlServer);

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
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await connection.FindNote([new FindNoteArgs(2)], cancellationToken: token);

        rows.ShouldBe([SeedNotes[1]]);
    }

    [Fact]
    public async Task FindNote_Empty_ReturnsEmpty()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await connection.FindNote([], cancellationToken: token);

        rows.ShouldBeEmpty();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The ids 1 to 4 in turn, over two chunks: the three notes in order, and nothing for 4, which no note has.
    [Fact]
    public async Task FindNote_OneThousandAndOneItems_LeavesOutTheMissingInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var ids = Enumerable.Range(0, ChunkSize + 1).Select(static i => (i % 4) + 1).ToArray();

        var rows = await connection.FindNote(ids.Select(static id => new FindNoteArgs(id)), cancellationToken: token);

        rows.ShouldBe(ids.Where(static id => id <= 3).Select(static id => SeedNotes[id - 1]));
    }

    // The third item repeats the first's id, which the primary key refuses: the method throws the driver's exception
    // inside its own, at the third item, and rolls back its transaction, which held the first two.
    [Fact]
    public async Task InsertNote_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwoAndLeavesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(DuplicateAtItemTwo(), Notes, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("InsertNote");
        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        connection.State.ShouldBe(ConnectionState.Open);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The first item repeats the id of a note already in the table, so the error is the first command's, which
    // SqlClient throws from the batch's execution, here without a reader.
    [Fact]
    public async Task InsertNoteRowCount_DuplicateKeyAtItemZero_ThrowsCollectionItemExceptionAtZero()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        _ = await connection.InsertNoteRowCount(
            [new NoteToAdd(101, "Already there.", 1)],
            Notes,
            cancellationToken: token
        );
        NoteToAdd[] items = [new(101, "Again.", 1), new(102, "Second.", 2)];

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNoteRowCount(items, Notes, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("InsertNoteRowCount");
        thrown.ItemIndex.ShouldBe(0);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        (await IdsAsync(connection, null, token)).ShouldBe([101]);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The same failure at the third item of a batch run without a reader.
    [Fact]
    public async Task InsertNoteRowCount_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwo()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNoteRowCount(DuplicateAtItemTwo(), Notes, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The first batch runs before the null is found, as the method checks each item as it adds it to its batch, and
    // the rollback of the method's transaction takes the first batch's rows with it.
    [Fact]
    public async Task NullItemInTheSecondChunk_RollsBackTheFirst()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        var items = Items(1500);
        items[1499] = null!;

        var thrown = await Should.ThrowAsync<ArgumentNullException>(() =>
            connection.InsertNote(items, Notes, cancellationToken: token)
        );

        thrown.ParamName.ShouldBe("parameters");
        thrown.Message.ShouldStartWith("Item 1499 of the collection is null.");
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The same null under transaction=off: the first batch committed before the null was found, so its rows stay, which
    // shows that the test above saw a rollback and not a batch that never ran.
    [Fact]
    public async Task InsertNoteNoTransaction_NullItemInTheSecondChunk_KeepsTheFirstChunk()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        var items = Items(1500);
        items[1499] = null!;

        var thrown = await Should.ThrowAsync<ArgumentNullException>(() =>
            connection.InsertNoteNoTransaction(items, Notes, cancellationToken: token)
        );

        thrown.Message.ShouldStartWith("Item 1499 of the collection is null.");
        (await CountNotesAsync(connection, null, token)).ShouldBe(ChunkSize);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The middle item names an account that does not exist, so its insert returns no row, which a one query throws
    // for, and the rollback takes the first item's row with it.
    [Fact]
    public async Task One_ItemWithNoRow_ThrowsUnexpectedRowCountAndRollsBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 2)];

        var thrown = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.InsertNote(items, Notes, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("InsertNote");
        thrown.ItemIndex.ShouldBe(1);
        thrown.Message.ShouldBe("The query 'InsertNote' returned no rows for item 1.");
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // With no transaction, SQL Server commits each statement on its own, and a duplicate key ends only its item's: the
    // items before it and the one after it commit.
    [Fact]
    public async Task InsertNoteNoTransaction_FailureAtItemTwo_KeepsTheOthers()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNoteNoTransaction(DuplicateAtItemTwo(), Notes, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        (await IdsAsync(connection, null, token)).ShouldBe([101, 102, 104]);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The same failure at item 1,200 of 2,500, in the second chunk: every item of the first two chunks but the failed
    // one commits, and the third chunk never runs.
    [Fact]
    public async Task InsertNoteNoTransaction_FailureInTheSecondChunk_KeepsTheOthersOfItsChunkAndRunsNoMore()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        var items = Items(2500);
        items[1200] = items[1200] with { Id = FirstId };

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNoteNoTransaction(items, Notes, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(1200);
        (await CountNotesAsync(connection, null, token)).ShouldBe((2 * ChunkSize) - 1);
        (await MaxNoteIdAsync(connection, token)).ShouldBe(FirstId + (2 * ChunkSize) - 1);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // Under XACT_ABORT ON a duplicate key ends the batch: the items before it commit, and the item after it never runs.
    [Fact]
    public async Task InsertNoteNoTransaction_XactAbortOn_KeepsOnlyTheItemsBeforeTheFailure()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenUnpooledWithXactAbortAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNoteNoTransaction(DuplicateAtItemTwo(), Notes, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(2);
        (await IdsAsync(connection, null, token)).ShouldBe([101, 102]);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // Item 1,200 repeats the first's id, in the second chunk, under the method's own transaction: it rolls back the
    // first chunk with the second.
    [Fact]
    public async Task InsertNote_DuplicateKeyInTheSecondChunk_LeavesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(DuplicateInTheSecondChunk(), Notes, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(1200);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The same failure under each isolation level, each by its copy of InsertNoteSerializable.
    [Theory]
    [InlineData("read uncommitted")]
    [InlineData("read committed")]
    [InlineData("repeatable read")]
    [InlineData("serializable")]
    [InlineData("snapshot")]
    public async Task InsertNoteAtALevel_DuplicateKeyInTheSecondChunk_LeavesNothing(string level)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            InsertAtLevelAsync(connection, level, DuplicateInTheSecondChunk(), token)
        );

        thrown.ItemIndex.ShouldBe(1200);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // SQL Server reports the level of a session's transaction in sys.dm_exec_sessions, from 1 for read uncommitted to 5
    // for snapshot, which the schema of app allows.
    [Theory]
    [InlineData("read uncommitted", "1")]
    [InlineData("read committed", "2")]
    [InlineData("repeatable read", "3")]
    [InlineData("serializable", "4")]
    [InlineData("snapshot", "5")]
    public async Task InsertNoteAtALevel_TwoItems_RunsAtThatLevel(string level, string reported)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var rows = await InsertAtLevelAsync(connection, level, Items(2), token);

        rows.ShouldBe([new(FirstId, reported), new(FirstId + 1, reported)]);
        (await CountNotesAsync(connection, null, token)).ShouldBe(2);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // A conversion that fails ends the batch and rolls back the method's transaction on the server, which leaves its
    // Connection null: the method skips its own rollback, which would throw, and throws the conversion at its item.
    [Fact]
    public async Task ConversionErrorInOwnTransaction_RollsBackWithoutAnError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "int:x", 2), new(103, "Third.", 3)];

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertConvertedNote(items, Notes, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("InsertConvertedNote");
        thrown.ItemIndex.ShouldBe(1);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(ConversionFailed);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // Under XACT_ABORT ON the duplicate key rolls back the method's own transaction on the server, and the method skips
    // its rollback, as above.
    [Fact]
    public async Task XactAbortOn_OwnTransaction_ThrowsTheWrappedErrorAndLeavesNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenUnpooledWithXactAbortAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(DuplicateAtItemTwo(), Notes, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The method runs in the caller's transaction, whose own read sees the rows, and neither commits nor ends it: the
    // caller's rollback, which would throw on a transaction that had completed, takes the rows away.
    [Fact]
    public async Task InsertNote_CallersTransaction_IsUsedAndNotCommitted()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        var items = Items(3);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var rows = await connection.InsertNote(items, Notes, transaction, cancellationToken: token);
        var during = await CountNotesAsync(connection, transaction, token);
        await transaction.RollbackAsync(token);

        rows.ShouldBe(RowsOf(items));
        during.ShouldBe(3);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
    }

    // A transaction that has completed is refused before anything runs.
    [Fact]
    public async Task InsertNote_CompletedTransaction_IsRefused()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        await transaction.CommitAsync(token);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            connection.InsertNote(Items(1), Notes, transaction, cancellationToken: token)
        );

        thrown.Message.ShouldBe(
            "The transaction given to the query 'InsertNote' has completed; it is no longer usable."
        );
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
    }

    // With XACT_ABORT off a duplicate key ends only its item's statement, and leaves the caller's transaction open,
    // with the items before and after it in it: the caller's commit keeps them, which shows the item after the failure
    // ran.
    [Fact]
    public async Task InsertNote_DuplicateKeyInTheCallersTransaction_LeavesTheOtherItemsInIt()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(DuplicateAtItemTwo(), Notes, transaction, cancellationToken: token)
        );
        _ = transaction.Connection.ShouldNotBeNull();
        await transaction.CommitAsync(token);

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        (await IdsAsync(connection, null, token)).ShouldBe([101, 102, 104]);
    }

    // The same failure, after which the caller rolls back: nothing is left.
    [Fact]
    public async Task InsertNote_DuplicateKeyInTheCallersTransaction_LeavesItsRollbackToTheCaller()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(DuplicateAtItemTwo(), Notes, transaction, cancellationToken: token)
        );
        await transaction.RollbackAsync(token);

        thrown.ItemIndex.ShouldBe(2);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
    }

    // Item 1,200 repeats the first's id, in the second chunk of the caller's transaction: the transaction holds every
    // other item until the caller rolls it back, after which nothing is left.
    [Fact]
    public async Task InsertNote_DuplicateKeyInTheSecondChunkOfTheCallersTransaction_LeavesItsRollbackToTheCaller()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(DuplicateInTheSecondChunk(), Notes, transaction, cancellationToken: token)
        );
        var during = await CountNotesAsync(connection, transaction, token);
        await transaction.RollbackAsync(token);

        thrown.ItemIndex.ShouldBe(1200);
        during.ShouldBe(1499);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
    }

    // Under XACT_ABORT ON the duplicate key rolls back the caller's transaction itself, so the method throws it at its
    // item as it is, and does not report a rollback, which no later error caused: the transaction has completed, its
    // Connection is null, and nothing is left.
    [Fact]
    public async Task XactAbortOn_CallersTransaction_ThrowsTheWrappedErrorAndDoesNotRollBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenUnpooledWithXactAbortAsync(token);
        await using var transaction = await connection.BeginTransactionAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(DuplicateAtItemTwo(), Notes, transaction, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        transaction.Connection.ShouldBeNull();
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
    }

    // With XACT_ABORT off the duplicate key at item 1 leaves the caller's transaction open, and the conversion of item
    // 3, which the method reads after it, rolls it back: the method reports the rollback, with the item's error inside.
    [Fact]
    public async Task InsertConvertedNote_ItemErrorThenALaterRollbackInCallersTransaction_ThrowsTransactionRolledBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        NoteToAdd[] items = [new(101, "First.", 1), new(101, "Again.", 2), new(103, "Third.", 3), new(104, "int:x", 1)];

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(() =>
            connection.InsertConvertedNote(items, Notes, transaction, cancellationToken: token)
        );

        thrown.QueryName.ShouldBe("InsertConvertedNote");
        var inner = thrown.InnerException.ShouldBeOfType<CollectionItemException>();
        inner.ItemIndex.ShouldBe(1);
        inner.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        transaction.Connection.ShouldBeNull();
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
    }

    // Item 1 names an account that does not exist, so its insert returns no row, which a one query throws for, and the
    // conversion of item 3, which the reader's disposal reads, rolls back the caller's transaction: the method reports
    // the rollback, with its own exception inside.
    [Fact]
    public async Task InsertConvertedNote_NoRowThenALaterRollbackInCallersTransaction_ThrowsTransactionRolledBack()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        await using var transaction = await connection.BeginTransactionAsync(token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 3), new(104, "int:x", 1)];

        var thrown = await Should.ThrowAsync<TransactionRolledBackException>(() =>
            connection.InsertConvertedNote(items, Notes, transaction, cancellationToken: token)
        );

        thrown.InnerException.ShouldBeOfType<UnexpectedRowCountException>().ItemIndex.ShouldBe(1);
        transaction.Connection.ShouldBeNull();
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
    }

    // The query's second statement returns a result set of its own, which would take the place of the second item's.
    // With two items it is read as the second item's and refused for its columns; with one it follows the last item's.
    [Theory]
    [InlineData(2, 1)]
    [InlineData(1, 0)]
    public async Task TwoResultSetQuery_Throws(int count, int itemIndex)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        var items = Enumerable.Range(1, count).Select(static id => new FindAccountThenOtherArgs(id));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            connection.FindAccountThenOther(items, cancellationToken: token)
        );

        thrown.Message.ShouldBe(
            "The query 'FindAccountThenOther' returned a result set of another shape for item "
                + itemIndex.ToString(CultureInfo.InvariantCulture)
                + ", so it cannot run over a collection: a query whose statements return more than one result set "
                + "cannot."
        );
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The second item's statement returns no result set, so the batch has fewer than its items, and the method refuses
    // it at the item whose result set is missing.
    [Fact]
    public async Task FindAccountWhenPositive_ItemWithoutAResultSet_Throws()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            connection.FindAccountWhenPositive(
                [new FindAccountWhenPositiveArgs(1), new FindAccountWhenPositiveArgs(-1)],
                cancellationToken: token
            )
        );

        thrown.Message.ShouldStartWith(
            "The query 'FindAccountWhenPositive' returned a result set of another shape for item 1,"
        );
    }

    // The same query is read as usual when every item returns its result set.
    [Fact]
    public async Task FindAccountWhenPositive_EveryItemWithAResultSet_ReturnsTheirRows()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var rows = await connection.FindAccountWhenPositive(
            [new FindAccountWhenPositiveArgs(2), new FindAccountWhenPositiveArgs(1)],
            cancellationToken: token
        );

        rows.ShouldBe([new FindAccountWhenPositiveDto(2), new FindAccountWhenPositiveDto(1)]);
    }

    // The query's second statement returns an empty result set of the same one column, which the guard cannot tell from
    // the next item's: over two items the second reads it as its own, and the guard refuses the batch at its end, where
    // the second item's own result set is left, so no row is returned.
    [Fact]
    public async Task FindAccountThenEmpty_SameWidthResultSet_ThrowsAtTheEndOfTheBatch()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            connection.FindAccountThenEmpty(
                [new FindAccountThenEmptyArgs(1), new FindAccountThenEmptyArgs(2)],
                cancellationToken: token
            )
        );

        thrown.Message.ShouldStartWith(
            "The query 'FindAccountThenEmpty' returned a result set of another shape for item 1,"
        );
    }

    // The same query under one: the second item reads the empty result set as its own and throws for its missing row
    // before the guard is reached.
    [Fact]
    public async Task GetAccountThenEmpty_SameWidthResultSet_ThrowsUnexpectedRowCount()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        var thrown = await Should.ThrowAsync<UnexpectedRowCountException>(() =>
            connection.GetAccountThenEmpty(
                [new GetAccountThenEmptyArgs(1), new GetAccountThenEmptyArgs(2)],
                cancellationToken: token
            )
        );

        thrown.ItemIndex.ShouldBe(1);
    }

    // The second statement returns one column of text, which the second item reads as its id and cannot: SqlClient's
    // cast error is thrown before the guard is reached.
    [Fact]
    public async Task FindAccountThenText_SameWidthResultSetOfAnotherType_ThrowsTheCastError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

        _ = await Should.ThrowAsync<InvalidCastException>(() =>
            connection.FindAccountThenText(
                [new FindAccountThenTextArgs(1), new FindAccountThenTextArgs(2)],
                cancellationToken: token
            )
        );
    }

    // The insert's second statement returns a result set of another width, which the guard refuses at the second item,
    // after the first item's insert: the method's own transaction is rolled back, and the table is left empty.
    [Fact]
    public async Task InsertNoteThenOther_ResultSetOfAnotherWidth_RollsBackTheMethodsTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            connection.InsertNoteThenOther(Items(2), Notes, cancellationToken: token)
        );

        thrown.Message.ShouldStartWith(
            "The query 'InsertNoteThenOther' returned a result set of another shape for item 1,"
        );
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The first chunk's items read the accounts of alice and carol, and the last item, in the second chunk, reads
    // bob's, whose row a second connection holds, and waits on its lock until the token is cancelled: the method's
    // transaction is rolled back, the first chunk's rows with it.  An insert that returns rows through OUTPUT sends its
    // columns before it reads bob's row, so the wait is in the read of the item's first row, whose cancel SqlClient
    // throws as a TaskCanceledException, as RunTimeArgumentTests says of a SELECT.
    [Fact]
    public async Task InsertNote_TokenCancelledInTheSecondChunk_RollsBackTheFirst()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(
            EndToEndSources.AppWithoutCommandTimeout(sqlServer),
            token
        );
        // Alice and carol in turn, and bob last.
        var items = Items(ChunkSize + 1)
            .Select(static item => item with { AccountId = item.AccountId == 2 ? 3 : item.AccountId })
            .ToArray();
        items[ChunkSize] = items[ChunkSize] with { AccountId = 2 };

        var thrown = await CancelledWhileAnItemWaitsAsync(connection, items);

        _ = thrown.ShouldBeOfType<TaskCanceledException>();
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // The connection, enlisted in the scope's transaction, runs the method, which begins no transaction of its own: one
    // of its own would have committed the rows.  The scope ends without Complete, which rolls them back, and the
    // temporary table, made before the scope, is left empty.
    [Fact]
    public async Task InsertNote_InATransactionScope_BeginsNoTransactionOfItsOwn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        var items = Items(3);
        IReadOnlyList<BatchNote> rows;
        long during;

        using (new TransactionScope(TransactionScopeAsyncFlowOption.Enabled))
        {
            connection.EnlistTransaction(Transaction.Current);
            rows = await connection.InsertNote(items, Notes, cancellationToken: token);
            during = await CountNotesAsync(connection, null, token);
        }

        rows.ShouldBe(RowsOf(items));
        during.ShouldBe(3);
        (await CountNotesAsync(connection, null, token)).ShouldBe(0);
    }

    // The repository's method takes no transaction and begins its own, and commits it before it returns: a transaction
    // that it disposed uncommitted would have rolled the rows back.  The borrowed connection is open, with the
    // temporary table, so the method leaves it open.
    [Fact]
    public async Task Repository_InsertNote_CommitsItsOwnTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        var items = Items(3);

        var rows = await new BatchQueries(connection).InsertNoteInRepository(items, Notes, token);

        rows.ShouldBe(RowsOf(items));
        connection.State.ShouldBe(ConnectionState.Open);
        (await CountNotesAsync(connection, null, token)).ShouldBe(3);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    private async Task InsertManyAsync(int count)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.App(sqlServer), token);
        var items = Items(count);

        var rows = await connection.InsertNote(items, Notes, cancellationToken: token);

        rows.ShouldBe(RowsOf(items));
        (await CountNotesAsync(connection, null, token)).ShouldBe(count);
        await ShouldBeInNoTransactionAsync(connection, token);
    }

    // Runs InsertNote while LockOfBob holds bob's row, which an item reads, and cancels its token once a second
    // connection sees the method's session waiting on the lock, so that the cancel always falls while the batch runs,
    // as RunTimeArgumentTests cancels a query method, and returns what the call threw.  The connection has no command
    // timeout, so only the cancel ends the wait, and the call is awaited through Eventually.WithinAsync, so that a call
    // that ignores the token fails the test at the patience.
    private async Task<Exception> CancelledWhileAnItemWaitsAsync(SqlConnection connection, NoteToAdd[] items)
    {
        var token = TestContext.Current.CancellationToken;
        var session = string.Create(CultureInfo.InvariantCulture, $"s.session_id = {connection.ServerProcessId}");
        await using var lockOfBob = await RunTimeArgumentTests.LockOfBob.TakeAsync(sqlServer, token);
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(token);
        var call = Eventually.WithinAsync(
            connection.InsertNote(items, Notes, cancellationToken: cancellation.Token),
            "The collection method outlasted the cancellation of its token."
        );
        bool endedFirst;
        try
        {
            // The wait ends too when the call does, so that a call that fails at once is not waited for.
            await Eventually.TrueAsync(
                async () =>
                    call.IsCompleted || await sqlServer.ScalarAsync(RunTimeArgumentTests.WaitingOnALock(session)) is 1,
                "The server never showed the method's session waiting on the lock of bob's row."
            );
        }
        finally
        {
            // However the wait ends, the call is cancelled and waited for, so that it ends before the test does and a
            // failure of the wait is the one reported.
            endedFirst = call.IsCompleted;
            await cancellation.CancelAsync();
            await ((Task)call).ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);
        }

        if (endedFirst)
        {
            // A call that ended before its wait was seen reports its own failure.
            _ = await call;
            throw new ShouldAssertException("The method ended before the server showed it waiting on the lock.");
        }

        return await Should.ThrowAsync<Exception>(() => call);
    }

    // A connection that is not pooled, with the temporary table and XACT_ABORT ON, which ends with its session.
    private async Task<SqlConnection> OpenUnpooledWithXactAbortAsync(CancellationToken token)
    {
        var connection = await OpenWithTemporaryNotesAsync(EndToEndSources.AppUnpooled(sqlServer), token);
        await using var set = new SqlCommand("SET XACT_ABORT ON", connection);
        _ = await set.ExecuteNonQueryAsync(token);
        return connection;
    }

    // The copy of InsertNoteSerializable at the level.
    private static Task<IReadOnlyList<IsolatedNote>> InsertAtLevelAsync(
        SqlConnection connection,
        string level,
        NoteToAdd[] items,
        CancellationToken token
    ) =>
        level switch
        {
            "read uncommitted" => connection.InsertNoteReadUncommitted(items, Notes, cancellationToken: token),
            "read committed" => connection.InsertNoteReadCommitted(items, Notes, cancellationToken: token),
            "repeatable read" => connection.InsertNoteRepeatableRead(items, Notes, cancellationToken: token),
            "serializable" => connection.InsertNoteSerializable(items, Notes, cancellationToken: token),
            "snapshot" => connection.InsertNoteSnapshot(items, Notes, cancellationToken: token),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "No copy runs at this level."),
        };

    // Four notes whose third repeats the first's id.
    private static NoteToAdd[] DuplicateAtItemTwo()
    {
        var items = Items(4);
        items[2] = items[2] with { Id = FirstId };
        return items;
    }

    // 1,500 notes whose item 1,200, in the second chunk, repeats the first's id.
    private static NoteToAdd[] DuplicateInTheSecondChunk()
    {
        var items = Items(1500);
        items[1200] = items[1200] with { Id = FirstId };
        return items;
    }

    // SqlClient refuses a command without the transaction that its connection is in, and the server counts the
    // session's open transactions, so a method that left its own open fails this.
    private static async Task ShouldBeInNoTransactionAsync(SqlConnection connection, CancellationToken token)
    {
        await using var count = new SqlCommand("SELECT @@TRANCOUNT", connection);
        (await count.ExecuteScalarAsync(token)).ShouldBe(0);
    }

    // Notes with the ids from FirstId on, to the seed's three accounts in turn.
    private static NoteToAdd[] Items(int count) =>
        [.. Enumerable.Range(0, count).Select(static i => new NoteToAdd(FirstId + i, "Note " + i + ".", (i % 3) + 1))];

    private static BatchNote[] RowsOf(IEnumerable<NoteToAdd> items) =>
        [.. items.Select(static item => new BatchNote(item.Id, item.AccountId, item.Body))];

    // Opens the connection and makes the temporary table Notes on it, empty, with the columns of dbo.notes that the
    // inserts write, and no identity, so that an insert writes the id it is given; it ends with the session.
    private static async Task<SqlConnection> OpenWithTemporaryNotesAsync(
        SqlConnection connection,
        CancellationToken token
    )
    {
        await connection.OpenAsync(token);
        await using var create = new SqlCommand(
            "CREATE TABLE "
                + Notes
                + " (id int PRIMARY KEY, account_id int NOT NULL, kind varchar(10) NOT NULL, "
                + "body nvarchar(max) NOT NULL, written_at datetime2(3) NOT NULL)",
            connection
        );
        _ = await create.ExecuteNonQueryAsync(token);
        return connection;
    }

    private static async Task<int> CountNotesAsync(
        SqlConnection connection,
        DbTransaction? transaction,
        CancellationToken token
    )
    {
        await using var count = new SqlCommand(
            "SELECT COUNT(*) FROM " + Notes,
            connection,
            (SqlTransaction?)transaction
        );
        return (int)await count.ExecuteScalarAsync(token);
    }

    private static async Task<int> MaxNoteIdAsync(SqlConnection connection, CancellationToken token)
    {
        await using var max = new SqlCommand("SELECT MAX(id) FROM " + Notes, connection);
        return (int)await max.ExecuteScalarAsync(token);
    }

    private static async Task<List<int>> IdsAsync(
        SqlConnection connection,
        DbTransaction? transaction,
        CancellationToken token
    )
    {
        await using var select = new SqlCommand(
            "SELECT id FROM " + Notes + " ORDER BY id",
            connection,
            (SqlTransaction?)transaction
        );
        await using var reader = await select.ExecuteReaderAsync(token);
        var ids = new List<int>();
        while (await reader.ReadAsync(token))
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }
}
