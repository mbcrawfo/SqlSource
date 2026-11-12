using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Transactions;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The synchronous collection methods of the copies under method-type=sync of Queries/Batches.sql, run as
// BatchQueryTests runs their originals' asynchronous ones: each shape over one item, a few and more than 1,000, the
// rows of every item in the order of the items, an empty collection, a closed connection, which the method opens and
// closes again whether it returns or throws, a failure in the second chunk under on, off and each isolation level and
// in the caller's transaction, with what it leaves in the table, the item and the error number that
// CollectionItemException carries, a null item, an item with no row, a completed transaction, a later error that rolls
// the caller's transaction back, the checks of several result sets, a conversion that ends the batch, XACT_ABORT ON,
// the session's isolation level, which the method sets back after a transaction of its own, and an ambient
// TransactionScope.  Then the copies under method-type=both: a query's two collection methods insert their items, and
// a stream's collection methods return arrays beside the stream, which has no synchronous method.  As in
// BatchQueryTests, an insert writes into a temporary table #notes of the test's connection, passed by the query's
// token, so that a method that commits leaves the seed as it was and the table outlives a rollback, but for the test of
// the pooled session, which writes into a table of app named for it, which it drops; a test that sets XACT_ABORT ON or
// the session's level takes a connection of EndToEndSources.AppUnpooled.  A test that reads runs on the seed's notes.
// Every insert writes the id it is given, from 101 on.
[Collection(UsesSqlServer.Name)]
public sealed class SynchronousBatchTests(SqlServerDatabases sqlServer)
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

    private const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    // The seed's three notes, as BatchNote reads them.
    private static readonly BatchNote[] SeedNotes =
    [
        new(1, 1, "Asked for a larger plan."),
        new(2, 1, "Sent the quote."),
        new(3, 2, "Closed the account on site."),
    ];

    // The repository's synchronous collection method begins its own transaction and commits it before it returns, as
    // the asynchronous one does: a transaction that it disposed uncommitted would have rolled the rows back.  The
    // borrowed connection is open, with the temporary table, so the method leaves it open.
    [Fact]
    public void Repository_InsertNoteSync_CommitsItsOwnTransaction()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        var items = Items(3);

        var rows = new BatchQueries(connection).InsertNoteInRepositorySync(items, Notes);

        rows.ShouldBe(RowsOf(items));
        connection.State.ShouldBe(ConnectionState.Open);
        CountNotes(connection, null).ShouldBe(3);
        ShouldBeInNoTransaction(connection);
    }

    [Fact]
    public void InsertNoteSync_OneItem_ReturnsItsRow()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        var rows = connection.InsertNoteSync([new NoteToAdd(101, "Only.", 2)], Notes);

        rows.ShouldBe([new BatchNote(101, 2, "Only.")]);
        CountNotes(connection, null).ShouldBe(1);
        ShouldBeInNoTransaction(connection);
    }

    [Fact]
    public void InsertNoteSync_ThreeItems_ReturnsTheirRowsInOrder()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        // Out of the order of their ids, so that the rows follow the items.
        NoteToAdd[] items = [new(103, "Third.", 2), new(101, "First.", 1), new(102, "Second.", 3)];

        var rows = connection.InsertNoteSync(items, Notes);

        rows.ShouldBe(RowsOf(items));
        connection.State.ShouldBe(ConnectionState.Open);
        Ids(connection, null).ShouldBe([101, 102, 103]);
        ShouldBeInNoTransaction(connection);
    }

    // A full batch, and a second of one item.
    [Fact]
    public void InsertNoteSync_OneThousandAndOneItems_ReturnsAllInOrder()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        var items = Items(ChunkSize + 1);

        var rows = connection.InsertNoteSync(items, Notes);

        rows.ShouldBe(RowsOf(items));
        CountNotes(connection, null).ShouldBe(ChunkSize + 1);
        ShouldBeInNoTransaction(connection);
    }

    // An empty collection returns before the method opens the connection.
    [Fact]
    public void InsertNoteSync_Empty_ReturnsEmptyAndLeavesTheConnectionClosed()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var rows = connection.InsertNoteSync([], Notes);

        rows.ShouldBeEmpty();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // No item, which returns 0, one, and two chunks, whose counts the method adds: the first of 1,000 and the second of
    // one.
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(ChunkSize + 1)]
    public void InsertNoteRowCountSync_Items_ReturnsTheirCount(int count)
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        var total = connection.InsertNoteRowCountSync(Items(count), Notes);

        total.ShouldBe(count);
        CountNotes(connection, null).ShouldBe(count);
        ShouldBeInNoTransaction(connection);
    }

    // The middle item names an account that does not exist, so its insert adds no row, and the total is the sum of
    // each item's count, not the number of items.
    [Fact]
    public void InsertNoteRowCountSync_ThreeItems_ReturnsTheTotal()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 2)];

        var count = connection.InsertNoteRowCountSync(items, Notes);

        count.ShouldBe(2);
        Ids(connection, null).ShouldBe([101, 103]);
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
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        connection.InsertNoteQuietlySync(Items(count), Notes);

        CountNotes(connection, null).ShouldBe(count);
        ShouldBeInNoTransaction(connection);
    }

    [Fact]
    public void FindNotesSync_OneItem_ReturnsItsRows()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var rows = connection.FindNotesSync([new FindNotesSyncArgs(1)]);

        rows.ShouldBe([SeedNotes[0], SeedNotes[1]]);
    }

    // Bob's one note, alice's two and carol's none, in the order of the items and not of the ids.  The connection is
    // closed, so the method opens it and closes it again.
    [Fact]
    public void FindNotesSync_ThreeItems_ReturnsEveryRowOfEachInOrder()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var rows = connection.FindNotesSync([
            new FindNotesSyncArgs(2),
            new FindNotesSyncArgs(1),
            new FindNotesSyncArgs(3),
        ]);

        rows.ShouldBe([SeedNotes[2], SeedNotes[0], SeedNotes[1]]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // Alice's two notes, bob's one and carol's none, in turn, over two chunks: the rows keep the order of the items,
    // and carol's empty result set takes its place.
    [Fact]
    public void FindNotesSync_OneThousandAndOneItems_ReturnsEveryRowOfEachInOrder()
    {
        using var connection = EndToEndSources.App(sqlServer);
        var accounts = Enumerable.Range(0, ChunkSize + 1).Select(static i => (i % 3) + 1).ToArray();

        var rows = connection.FindNotesSync(accounts.Select(static account => new FindNotesSyncArgs(account)));

        rows.ShouldBe(accounts.SelectMany(static account => SeedNotes.Where(note => note.AccountId == account)));
    }

    [Fact]
    public void FindNoteSync_OneItem_ReturnsItsRow()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var rows = connection.FindNoteSync([new FindNoteSyncArgs(2)]);

        rows.ShouldBe([SeedNotes[1]]);
    }

    [Fact]
    public void FindNoteSync_ThreeItemsOneWithNoRow_LeavesItOut()
    {
        using var connection = EndToEndSources.App(sqlServer);

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
        using var connection = EndToEndSources.App(sqlServer);
        var ids = Enumerable.Range(0, ChunkSize + 1).Select(static i => (i % 4) + 1).ToArray();

        var rows = connection.FindNoteSync(ids.Select(static id => new FindNoteSyncArgs(id)));

        rows.ShouldBe(ids.Where(static id => id <= 3).Select(static id => SeedNotes[id - 1]));
    }

    [Fact]
    public void FindNoteSync_Empty_ReturnsEmptyAndLeavesTheConnectionClosed()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var rows = connection.FindNoteSync([]);

        rows.ShouldBeEmpty();
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The third item repeats the first's id, which the primary key refuses: the method throws the driver's exception
    // inside its own, at the third item, and rolls back its transaction, which held the first two.
    [Fact]
    public void InsertNoteSync_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwoAndLeavesNothing()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteSync(DuplicateAtItemTwo(), Notes)
        );

        thrown.QueryName.ShouldBe("InsertNoteSync");
        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        connection.State.ShouldBe(ConnectionState.Open);
        CountNotes(connection, null).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // The same failure at the third item of a batch run without a reader.
    [Fact]
    public void InsertNoteRowCountSync_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwo()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteRowCountSync(DuplicateAtItemTwo(), Notes)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        CountNotes(connection, null).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // The first item repeats the id of a note already in the table, so the error is the first command's, which
    // SqlClient throws from the batch's execution, here without a reader.
    [Fact]
    public void InsertNoteRowCountSync_DuplicateKeyAtItemZero_ThrowsCollectionItemExceptionAtZero()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        _ = connection.InsertNoteRowCountSync([new NoteToAdd(101, "Already there.", 1)], Notes);
        NoteToAdd[] items = [new(101, "Again.", 1), new(102, "Second.", 2)];

        var thrown = Should.Throw<CollectionItemException>(() => connection.InsertNoteRowCountSync(items, Notes));

        thrown.QueryName.ShouldBe("InsertNoteRowCountSync");
        thrown.ItemIndex.ShouldBe(0);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        Ids(connection, null).ShouldBe([101]);
        ShouldBeInNoTransaction(connection);
    }

    // Item 1,200 repeats the first's id, in the second chunk, under the method's own transaction: it rolls back the
    // first chunk with the second.
    [Fact]
    public void InsertNoteSync_DuplicateKeyInTheSecondChunk_LeavesNothing()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteSync(DuplicateInTheSecondChunk(), Notes)
        );

        thrown.ItemIndex.ShouldBe(1200);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        CountNotes(connection, null).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // The same failure under each isolation level, each by its copy of InsertNoteSerializableSync, and under on.
    [Theory]
    [InlineData("read uncommitted")]
    [InlineData("read committed")]
    [InlineData("repeatable read")]
    [InlineData("serializable")]
    [InlineData("snapshot")]
    [InlineData("on")]
    public void InsertNoteAtALevelSync_DuplicateKeyInTheSecondChunk_LeavesNothing(string level)
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        var thrown = Should.Throw<CollectionItemException>(() =>
            InsertAtLevel(connection, level, DuplicateInTheSecondChunk())
        );

        thrown.ItemIndex.ShouldBe(1200);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        CountNotes(connection, null).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // With no transaction, SQL Server commits each statement on its own, and a duplicate key ends only its item's: the
    // items before it and the one after it commit.
    [Fact]
    public void InsertNoteNoTransactionSync_DuplicateKeyAtItemTwo_KeepsTheOthers()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteNoTransactionSync(DuplicateAtItemTwo(), Notes)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        Ids(connection, null).ShouldBe([101, 102, 104]);
        ShouldBeInNoTransaction(connection);
    }

    // The same failure at item 1,200 of 2,500, in the second chunk: every item of the first two chunks but the failed
    // one commits, and the third chunk never runs.
    [Fact]
    public void InsertNoteNoTransactionSync_DuplicateKeyInTheSecondChunk_KeepsTheOthersOfItsChunkAndRunsNoMore()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        var items = Items(2500);
        items[1200] = items[1200] with { Id = FirstId };

        var thrown = Should.Throw<CollectionItemException>(() => connection.InsertNoteNoTransactionSync(items, Notes));

        thrown.ItemIndex.ShouldBe(1200);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        CountNotes(connection, null).ShouldBe((2 * ChunkSize) - 1);
        MaxNoteId(connection).ShouldBe(FirstId + (2 * ChunkSize) - 1);
        ShouldBeInNoTransaction(connection);
    }

    // The first batch runs before the null is found, as the method checks each item as it adds it to its batch, and
    // the rollback of the method's transaction takes the first batch's rows with it.
    [Fact]
    public void InsertNoteSync_NullItemInTheSecondChunk_RollsBackTheFirst()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        var items = Items(1500);
        items[1499] = null!;

        var thrown = Should.Throw<ArgumentNullException>(() => connection.InsertNoteSync(items, Notes));

        thrown.ParamName.ShouldBe("parameters");
        thrown.Message.ShouldStartWith("Item 1499 of the collection is null.");
        CountNotes(connection, null).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // The same null under transaction=off: the first batch committed before the null was found, so its rows stay, which
    // shows that the test above saw a rollback and not a batch that never ran.
    [Fact]
    public void InsertNoteNoTransactionSync_NullItemInTheSecondChunk_KeepsTheFirstChunk()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        var items = Items(1500);
        items[1499] = null!;

        var thrown = Should.Throw<ArgumentNullException>(() => connection.InsertNoteNoTransactionSync(items, Notes));

        thrown.Message.ShouldStartWith("Item 1499 of the collection is null.");
        CountNotes(connection, null).ShouldBe(ChunkSize);
        ShouldBeInNoTransaction(connection);
    }

    // The middle item names an account that does not exist, so its insert returns no row, which a one query throws
    // for, and the rollback takes the first item's row with it.
    [Fact]
    public void InsertNoteSync_ItemWithNoRow_ThrowsUnexpectedRowCountAndRollsBack()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 2)];

        var thrown = Should.Throw<UnexpectedRowCountException>(() => connection.InsertNoteSync(items, Notes));

        thrown.QueryName.ShouldBe("InsertNoteSync");
        thrown.ItemIndex.ShouldBe(1);
        thrown.Message.ShouldBe("The query 'InsertNoteSync' returned no rows for item 1.");
        CountNotes(connection, null).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // A conversion that fails ends the batch and rolls back the method's transaction on the server, which leaves its
    // Connection null: the method skips its own rollback, which would throw, and throws the conversion at its item.
    [Fact]
    public void InsertConvertedNoteSync_ConversionErrorInOwnTransaction_RollsBackWithoutAnError()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "int:x", 2), new(103, "Third.", 3)];

        var thrown = Should.Throw<CollectionItemException>(() => connection.InsertConvertedNoteSync(items, Notes));

        thrown.QueryName.ShouldBe("InsertConvertedNoteSync");
        thrown.ItemIndex.ShouldBe(1);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(ConversionFailed);
        CountNotes(connection, null).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // Under XACT_ABORT ON the duplicate key rolls back the method's own transaction on the server, and the method skips
    // its rollback, as above.
    [Fact]
    public void InsertNoteSync_XactAbortOnInItsOwnTransaction_ThrowsTheWrappedErrorAndLeavesNothing()
    {
        using var connection = OpenUnpooledWithXactAbort();

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteSync(DuplicateAtItemTwo(), Notes)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        CountNotes(connection, null).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // Under XACT_ABORT ON with no transaction a duplicate key ends the batch: the items before it commit, and the item
    // after it never runs.
    [Fact]
    public void InsertNoteNoTransactionSync_XactAbortOn_KeepsOnlyTheItemsBeforeTheFailure()
    {
        using var connection = OpenUnpooledWithXactAbort();

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteNoTransactionSync(DuplicateAtItemTwo(), Notes)
        );

        thrown.ItemIndex.ShouldBe(2);
        Ids(connection, null).ShouldBe([101, 102]);
        ShouldBeInNoTransaction(connection);
    }

    // The method runs in the caller's transaction, whose own read sees the rows, and neither commits nor ends it: the
    // caller's rollback, which would throw on a transaction that had completed, takes the rows away.
    [Fact]
    public void InsertNoteSync_CallersTransaction_IsUsedAndNotCommitted()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        var items = Items(3);
        using var transaction = connection.BeginTransaction();

        var rows = connection.InsertNoteSync(items, Notes, transaction);
        var during = CountNotes(connection, transaction);
        transaction.Rollback();

        rows.ShouldBe(RowsOf(items));
        during.ShouldBe(3);
        CountNotes(connection, null).ShouldBe(0);
    }

    // A transaction that has completed is refused before anything runs.
    [Fact]
    public void InsertNoteSync_CompletedTransaction_IsRefused()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        using var transaction = connection.BeginTransaction();
        transaction.Commit();

        var thrown = Should.Throw<InvalidOperationException>(() =>
            connection.InsertNoteSync(Items(1), Notes, transaction)
        );

        thrown.Message.ShouldBe(
            "The transaction given to the query 'InsertNoteSync' has completed; it is no longer usable."
        );
        CountNotes(connection, null).ShouldBe(0);
    }

    // With XACT_ABORT off a duplicate key ends only its item's statement, and leaves the caller's transaction open,
    // with the items before and after it in it: the caller's commit keeps them, which shows the item after the failure
    // ran.
    [Fact]
    public void InsertNoteSync_DuplicateKeyInTheCallersTransaction_LeavesTheOtherItemsInIt()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        using var transaction = connection.BeginTransaction();

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteSync(DuplicateAtItemTwo(), Notes, transaction)
        );
        _ = transaction.Connection.ShouldNotBeNull();
        transaction.Commit();

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        Ids(connection, null).ShouldBe([101, 102, 104]);
    }

    // Item 1,200 repeats the first's id, in the second chunk of the caller's transaction: the transaction holds every
    // other item until the caller rolls it back, after which nothing is left.
    [Fact]
    public void InsertNoteSync_DuplicateKeyInTheSecondChunkOfTheCallersTransaction_LeavesItsRollbackToTheCaller()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        using var transaction = connection.BeginTransaction();

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteSync(DuplicateInTheSecondChunk(), Notes, transaction)
        );
        var during = CountNotes(connection, transaction);
        transaction.Rollback();

        thrown.ItemIndex.ShouldBe(1200);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        during.ShouldBe(1499);
        CountNotes(connection, null).ShouldBe(0);
    }

    // Under XACT_ABORT ON the duplicate key rolls back the caller's transaction itself, so the method throws it at its
    // item as it is, and does not report a rollback, which no later error caused: the transaction has completed, its
    // Connection is null, and nothing is left.
    [Fact]
    public void InsertNoteSync_XactAbortOnInTheCallersTransaction_ThrowsTheWrappedErrorAndDoesNotRollBack()
    {
        using var connection = OpenUnpooledWithXactAbort();
        using var transaction = connection.BeginTransaction();

        var thrown = Should.Throw<CollectionItemException>(() =>
            connection.InsertNoteSync(DuplicateAtItemTwo(), Notes, transaction)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        transaction.Connection.ShouldBeNull();
        CountNotes(connection, null).ShouldBe(0);
    }

    // With XACT_ABORT off the duplicate key at item 1 leaves the caller's transaction open, and the conversion of item
    // 3, which the method reads after it, rolls it back: the method reports the rollback, with the item's error inside.
    [Fact]
    public void InsertConvertedNoteSync_ItemErrorThenALaterRollbackInCallersTransaction_ThrowsTransactionRolledBack()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        using var transaction = connection.BeginTransaction();
        NoteToAdd[] items = [new(101, "First.", 1), new(101, "Again.", 2), new(103, "Third.", 3), new(104, "int:x", 1)];

        var thrown = Should.Throw<TransactionRolledBackException>(() =>
            connection.InsertConvertedNoteSync(items, Notes, transaction)
        );

        thrown.QueryName.ShouldBe("InsertConvertedNoteSync");
        var inner = thrown.InnerException.ShouldBeOfType<CollectionItemException>();
        inner.ItemIndex.ShouldBe(1);
        inner.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        transaction.Connection.ShouldBeNull();
        CountNotes(connection, null).ShouldBe(0);
    }

    // Item 1 names an account that does not exist, so its insert returns no row, which a one query throws for, and the
    // conversion of item 3, which the reader's disposal reads, rolls back the caller's transaction: the method reports
    // the rollback, with its own exception inside.
    [Fact]
    public void InsertConvertedNoteSync_NoRowThenALaterRollbackInCallersTransaction_ThrowsTransactionRolledBack()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        using var transaction = connection.BeginTransaction();
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Lost.", 99), new(103, "Third.", 3), new(104, "int:x", 1)];

        var thrown = Should.Throw<TransactionRolledBackException>(() =>
            connection.InsertConvertedNoteSync(items, Notes, transaction)
        );

        thrown.InnerException.ShouldBeOfType<UnexpectedRowCountException>().ItemIndex.ShouldBe(1);
        transaction.Connection.ShouldBeNull();
        CountNotes(connection, null).ShouldBe(0);
    }

    // The missing item's statement returns no result set, so its chunk has fewer than its items, and SqlClient skips
    // its command: each later item of the chunk reads the next one's result set, and the results run out at the
    // chunk's last.  The method cannot tell which item returned none, so it names the chunk's items: the last of two,
    // the first of three, and the first of the second chunk's two.
    [Theory]
    [InlineData(2, 1, 0, 1)]
    [InlineData(3, 0, 0, 2)]
    [InlineData(ChunkSize + 2, ChunkSize, ChunkSize, ChunkSize + 1)]
    public void FindAccountWhenPositiveSync_ItemWithoutAResultSet_ThrowsNamingItsChunksItems(
        int count,
        int missingItem,
        int first,
        int last
    )
    {
        using var connection = EndToEndSources.App(sqlServer);
        var items = Enumerable
            .Range(0, count)
            .Select(i => new FindAccountWhenPositiveSyncArgs(i == missingItem ? -1 : (i % 3) + 1));

        var thrown = Should.Throw<InvalidOperationException>(() => connection.FindAccountWhenPositiveSync(items));

        thrown.Message.ShouldStartWith(
            "The query 'FindAccountWhenPositiveSync' returned a result set of another shape, or none, for one of the "
                + string.Create(CultureInfo.InvariantCulture, $"items from {first} to {last},")
        );
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The same query is read as usual when every item returns its result set.
    [Fact]
    public void FindAccountWhenPositiveSync_EveryItemWithAResultSet_ReturnsTheirRows()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var rows = connection.FindAccountWhenPositiveSync([
            new FindAccountWhenPositiveSyncArgs(2),
            new FindAccountWhenPositiveSyncArgs(1),
        ]);

        rows.ShouldBe([new FindAccountWhenPositiveSyncDto(2), new FindAccountWhenPositiveSyncDto(1)]);
    }

    // The query's second statement returns an empty result set of the same one column, which the guard cannot tell from
    // the next item's: over two items the second reads it as its own, and the guard refuses the batch at its end, where
    // the second item's own result set is left, so no row is returned.
    [Fact]
    public void FindAccountThenEmptySync_SameWidthResultSet_ThrowsAtTheEndOfTheBatch()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var thrown = Should.Throw<InvalidOperationException>(() =>
            connection.FindAccountThenEmptySync([
                new FindAccountThenEmptySyncArgs(1),
                new FindAccountThenEmptySyncArgs(2),
            ])
        );

        thrown.Message.ShouldStartWith(
            "The query 'FindAccountThenEmptySync' returned a result set of another shape, or none, for one of the "
                + "items from 0 to 1,"
        );
    }

    // The same query under one: the second item reads the empty result set as its own and throws for its missing row
    // before the guard is reached.  The connection was closed, and the method closes it again as it throws.
    [Fact]
    public void GetAccountThenEmptySync_SameWidthResultSet_ThrowsUnexpectedRowCountAndIsClosedAgain()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var thrown = Should.Throw<UnexpectedRowCountException>(() =>
            connection.GetAccountThenEmptySync([new GetAccountThenEmptySyncArgs(1), new GetAccountThenEmptySyncArgs(2)])
        );

        thrown.ItemIndex.ShouldBe(1);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    // The second statement returns one column of text, which the second item reads as its id and cannot: SqlClient's
    // cast error is thrown before the guard is reached.
    [Fact]
    public void FindAccountThenTextSync_SameWidthResultSetOfAnotherType_ThrowsTheCastError()
    {
        using var connection = EndToEndSources.App(sqlServer);

        _ = Should.Throw<InvalidCastException>(() =>
            connection.FindAccountThenTextSync([new FindAccountThenTextSyncArgs(1), new FindAccountThenTextSyncArgs(2)])
        );
    }

    // The insert's second statement returns a result set of another width, which the guard refuses at the second item,
    // after the first item's insert: the method's own transaction is rolled back, and the table is left empty.
    [Fact]
    public void InsertNoteThenOtherSync_ResultSetOfAnotherWidth_RollsBackTheMethodsTransaction()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        var thrown = Should.Throw<InvalidOperationException>(() => connection.InsertNoteThenOtherSync(Items(2), Notes));

        thrown.Message.ShouldStartWith(
            "The query 'InsertNoteThenOtherSync' returned a result set of another shape, or none, for one of the items "
                + "from 0 to 1,"
        );
        CountNotes(connection, null).ShouldBe(0);
        ShouldBeInNoTransaction(connection);
    }

    // SQL Server reports the level of a session's transaction in sys.dm_exec_sessions, from 1 for read uncommitted to 5
    // for snapshot, which the schema of app allows, and on begins at read committed.
    [Theory]
    [InlineData("read uncommitted", "1")]
    [InlineData("read committed", "2")]
    [InlineData("repeatable read", "3")]
    [InlineData("serializable", "4")]
    [InlineData("snapshot", "5")]
    [InlineData("on", "2")]
    public void InsertNoteAtALevelSync_TwoItems_RunsAtThatLevel(string level, string reported)
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));

        var rows = InsertAtLevel(connection, level, Items(2));

        rows.ShouldBe([new(FirstId, reported), new(FirstId + 1, reported)]);
        CountNotes(connection, null).ShouldBe(2);
        ShouldBeInNoTransaction(connection);
    }

    // A transaction begun at a level, or under on at read committed, sets the session's level, which SQL Server keeps
    // after the transaction ends, so the method sets it back to the level it found after it commits.  Each level runs
    // from read committed and from repeatable read, so that every level differs from one of them.  The connection is
    // not pooled, since a test sets its session's level.
    [Theory]
    [MemberData(nameof(LevelsAndSessionLevels))]
    public void InsertNoteAtALevelSync_OnAnOpenConnection_LeavesTheSessionsLevelAsItFound(string level, string before)
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.AppUnpooled(sqlServer));
        SetSessionLevel(connection, before);

        var rows = InsertAtLevel(connection, level, Items(2));

        rows.ShouldAllBe(row => row.Kind == LevelNumber(level).ToString(CultureInfo.InvariantCulture));
        SessionLevel(connection, null).ShouldBe(LevelNumber(before));
    }

    // An item's error rolls the method's transaction back, and the method sets the session's level back all the same.
    [Theory]
    [MemberData(nameof(LevelsAndSessionLevels))]
    public void InsertNoteAtALevelSync_DuplicateKeyAtItemTwo_LeavesTheSessionsLevelAsItFound(
        string level,
        string before
    )
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.AppUnpooled(sqlServer));
        SetSessionLevel(connection, before);

        var thrown = Should.Throw<CollectionItemException>(() =>
            InsertAtLevel(connection, level, DuplicateAtItemTwo())
        );

        thrown.ItemIndex.ShouldBe(2);
        CountNotes(connection, null).ShouldBe(0);
        SessionLevel(connection, null).ShouldBe(LevelNumber(before));
    }

    // The pool keeps a session's level when the connection is closed and opened again, so a closed connection that the
    // method opens and closes goes back to the pool at the level it came out at: on a pool of the test's own, the
    // session is left at a level, the method runs on the closed connection, and the connection opened again is the
    // same session at the same level.  The notes are a table of app of the test's own, which it drops, as in
    // BatchQueryTests, since the pool drops a session's temporary tables and a snapshot transaction cannot write a
    // global one in tempdb.
    [Theory]
    [InlineData("read uncommitted", "repeatable read")]
    [InlineData("read committed", "repeatable read")]
    [InlineData("repeatable read", "serializable")]
    [InlineData("serializable", "repeatable read")]
    [InlineData("snapshot", "repeatable read")]
    [InlineData("on", "repeatable read")]
    public void InsertNoteAtALevelSync_OnAClosedPooledConnection_LeavesThePooledSessionAsItFound(
        string level,
        string before
    )
    {
        var unique = Guid.NewGuid().ToString("N");
        var notes = "dbo.isolation_notes_" + unique;
        using var owner = OpenWithNotes(EndToEndSources.AppUnpooled(sqlServer), notes);
        try
        {
            using var connection = new SqlConnection(
                new SqlConnectionStringBuilder(EndToEndSources.AppConnectionString(sqlServer))
                {
                    ApplicationName = "isolation " + unique,
                }.ConnectionString
            );
            connection.Open();
            var session = connection.ServerProcessId;
            SetSessionLevel(connection, before);
            connection.Close();

            var rows = InsertAtLevel(connection, level, Items(2), notes);

            connection.State.ShouldBe(ConnectionState.Closed);
            rows.ShouldAllBe(row => row.Kind == LevelNumber(level).ToString(CultureInfo.InvariantCulture));
            connection.Open();
            connection.ServerProcessId.ShouldBe(session);
            SessionLevel(connection, null).ShouldBe(LevelNumber(before));
        }
        finally
        {
            Drop(owner, notes);
        }
    }

    // Given the caller's transaction, the method begins none, so it neither reads the session's level nor sets it: the
    // caller's transaction keeps its level, repeatable read, where setting it back to the default would have changed
    // it.
    [Fact]
    public void InsertNoteSerializableSync_InTheCallersRepeatableReadTransaction_LeavesItsLevel()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.AppUnpooled(sqlServer));
        using var transaction = connection.BeginTransaction(System.Data.IsolationLevel.RepeatableRead);

        var rows = connection.InsertNoteSerializableSync(Items(2), Notes, transaction);

        rows.ShouldBe([new(FirstId, "3"), new(FirstId + 1, "3")]);
        SessionLevel(connection, transaction).ShouldBe(3);
    }

    // Within an ambient transaction the method begins none either, so the scope's level, repeatable read, is kept.
    [Fact]
    public void InsertNoteSerializableSync_InARepeatableReadScope_LeavesItsLevel()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.AppUnpooled(sqlServer));
        var options = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.RepeatableRead };
        IReadOnlyList<IsolatedNote> rows;
        int during;

        using (new TransactionScope(TransactionScopeOption.Required, options))
        {
            connection.EnlistTransaction(Transaction.Current);
            rows = connection.InsertNoteSerializableSync(Items(2), Notes);
            during = SessionLevel(connection, null);
        }

        rows.ShouldBe([new(FirstId, "3"), new(FirstId + 1, "3")]);
        during.ShouldBe(3);
    }

    // The connection, enlisted in the scope's transaction at read committed, runs the serializable copy, which begins
    // no transaction of its own, so each item reports read committed, 2, where one of its own would set the session to
    // serializable, 4.  The scope ends without Complete, which rolls the rows back, and the temporary table, made
    // before the scope, is left empty.
    [Fact]
    public void InsertNoteSerializableSync_InAReadCommittedScope_RunsInTheScopesTransaction()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        var items = Items(3);
        var options = new TransactionOptions { IsolationLevel = System.Transactions.IsolationLevel.ReadCommitted };
        IReadOnlyList<IsolatedNote> rows;
        long during;

        using (new TransactionScope(TransactionScopeOption.Required, options))
        {
            connection.EnlistTransaction(Transaction.Current);
            rows = connection.InsertNoteSerializableSync(items, Notes);
            during = CountNotes(connection, null);
        }

        rows.ShouldBe([new(FirstId, "2"), new(FirstId + 1, "2"), new(FirstId + 2, "2")]);
        during.ShouldBe(3);
        CountNotes(connection, null).ShouldBe(0);
    }

    [Fact]
    public void InsertNoteBoth_ThreeItems_InsertsThem()
    {
        using var connection = OpenWithTemporaryNotes(EndToEndSources.App(sqlServer));
        var items = Items(3);

        var rows = connection.InsertNoteBoth(items, Notes);

        rows.ShouldBe(RowsOf(items));
        CountNotes(connection, null).ShouldBe(3);
        ShouldBeInNoTransaction(connection);
    }

    // The asynchronous collection method beside the synchronous one, which the test above runs, awaited throughout.
    [Fact]
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The name is Notes, the test's own constant."
    )]
    public async Task InsertNoteBothAsync_ThreeItems_InsertsThem()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);
        await connection.OpenAsync(token);
        await using (var create = new SqlCommand(CreateNotes(Notes), connection))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        var items = Items(3);

        var rows = await connection.InsertNoteBothAsync(items, Notes, cancellationToken: token);

        rows.ShouldBe(RowsOf(items));
        await using var count = new SqlCommand("SELECT COUNT(*) FROM " + Notes, connection);
        ((int)await count.ExecuteScalarAsync(token)).ShouldBe(3);
    }

    // A stream's collection method returns an array, as the query has no list of its own to return.
    [Fact]
    public void FindNotesStreamBoth_TwoItems_ReturnsAnArrayOfEveryRowInOrder()
    {
        using var connection = EndToEndSources.App(sqlServer);

        var rows = connection.FindNotesStreamBoth([new FindNotesStreamBothArgs(2), new FindNotesStreamBothArgs(1)]);

        rows.ShouldBe([SeedNotes[2], SeedNotes[0], SeedNotes[1]]);
        connection.State.ShouldBe(ConnectionState.Closed);
    }

    [Fact]
    public async Task FindNotesStreamBothAsync_TwoItems_ReturnsAnArrayOfEveryRowInOrder()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = EndToEndSources.App(sqlServer);

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

        synchronous.Single().ReturnType.ShouldBe(typeof(BatchNote[]));
        synchronous.Single().GetParameters()[1].ParameterType.ShouldBe(typeof(IEnumerable<FindNotesStreamBothArgs>));
        stream.Single().GetParameters()[1].ParameterType.ShouldBe(typeof(FindNotesStreamBothArgs));
    }

    // The copy of InsertNoteSerializableSync at the level, or under on, whose transaction SqlClient begins at read
    // committed, into the notes given, Notes unless another is named.
    private static IReadOnlyList<IsolatedNote> InsertAtLevel(
        SqlConnection connection,
        string level,
        NoteToAdd[] items,
        string notes = Notes
    ) =>
        level switch
        {
            "read uncommitted" => connection.InsertNoteReadUncommittedSync(items, notes),
            "read committed" => connection.InsertNoteReadCommittedSync(items, notes),
            "repeatable read" => connection.InsertNoteRepeatableReadSync(items, notes),
            "serializable" => connection.InsertNoteSerializableSync(items, notes),
            "snapshot" => connection.InsertNoteSnapshotSync(items, notes),
            "on" => connection.InsertNoteDefaultLevelSync(items, notes),
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "No copy runs at this level."),
        };

    // Each level of a copy, under on too, with each level a session starts the method at: read committed, the default,
    // and repeatable read, so that every level differs from one of them.
    public static TheoryData<string, string> LevelsAndSessionLevels()
    {
        var data = new TheoryData<string, string>();
        foreach (
            var level in new[]
            {
                "read uncommitted",
                "read committed",
                "repeatable read",
                "serializable",
                "snapshot",
                "on",
            }
        )
        {
            data.Add(level, "read committed");
            data.Add(level, "repeatable read");
        }

        return data;
    }

    // The number sys.dm_exec_sessions gives the level, or the level of the copy that the name gives, read committed
    // under on.
    private static int LevelNumber(string level) =>
        level switch
        {
            "read uncommitted" => 1,
            "read committed" or "on" => 2,
            "repeatable read" => 3,
            "serializable" => 4,
            "snapshot" => 5,
            _ => throw new ArgumentOutOfRangeException(nameof(level), level, "No such level."),
        };

    // Sets the level of the session's later transactions and statements.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The level is one of the test's own constants."
    )]
    private static void SetSessionLevel(SqlConnection connection, string level)
    {
        using var set = new SqlCommand("SET TRANSACTION ISOLATION LEVEL " + level.ToUpperInvariant(), connection);
        _ = set.ExecuteNonQuery();
    }

    // The session's level, as sys.dm_exec_sessions numbers it, read in the transaction given, if any.
    private static int SessionLevel(SqlConnection connection, DbTransaction? transaction)
    {
        using var read = new SqlCommand(
            "SELECT transaction_isolation_level FROM sys.dm_exec_sessions WHERE session_id = @@SPID",
            connection,
            (SqlTransaction?)transaction
        );
        return (short)read.ExecuteScalar();
    }

    // A connection that is not pooled, with the temporary table and XACT_ABORT ON, which ends with its session.
    private SqlConnection OpenUnpooledWithXactAbort()
    {
        var connection = OpenWithTemporaryNotes(EndToEndSources.AppUnpooled(sqlServer));
        using var set = new SqlCommand("SET XACT_ABORT ON", connection);
        _ = set.ExecuteNonQuery();
        return connection;
    }

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
    private static void ShouldBeInNoTransaction(SqlConnection connection)
    {
        using var count = new SqlCommand("SELECT @@TRANCOUNT", connection);
        count.ExecuteScalar().ShouldBe(0);
    }

    // Notes with the ids from FirstId on, to the seed's three accounts in turn.
    private static NoteToAdd[] Items(int count) =>
        [.. Enumerable.Range(0, count).Select(static i => new NoteToAdd(FirstId + i, "Note " + i + ".", (i % 3) + 1))];

    private static BatchNote[] RowsOf(IEnumerable<NoteToAdd> items) =>
        [.. items.Select(static item => new BatchNote(item.Id, item.AccountId, item.Body))];

    // The table of the name, empty, with the columns of dbo.notes that the inserts write, and no identity, so that an
    // insert writes the id it is given.
    private static string CreateNotes(string notes) =>
        "CREATE TABLE "
        + notes
        + " (id int PRIMARY KEY, account_id int NOT NULL, kind varchar(10) NOT NULL, "
        + "body nvarchar(max) NOT NULL, written_at datetime2(3) NOT NULL)";

    // Opens the connection and makes the temporary table Notes on it, which ends with the session.
    private static SqlConnection OpenWithTemporaryNotes(SqlConnection connection) => OpenWithNotes(connection, Notes);

    // Opens the connection and makes the table of the name on it, as Notes is made: a temporary one, or one of app.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The name is Notes or one the test makes of a GUID."
    )]
    private static SqlConnection OpenWithNotes(SqlConnection connection, string notes)
    {
        connection.Open();
        using var create = new SqlCommand(CreateNotes(notes), connection);
        _ = create.ExecuteNonQuery();
        return connection;
    }

    // Drops the table of the name.
    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The name is one the test makes of a GUID."
    )]
    private static void Drop(SqlConnection connection, string table)
    {
        using var drop = new SqlCommand("DROP TABLE " + table, connection);
        _ = drop.ExecuteNonQuery();
    }

    private static int CountNotes(SqlConnection connection, DbTransaction? transaction)
    {
        using var count = new SqlCommand("SELECT COUNT(*) FROM " + Notes, connection, (SqlTransaction?)transaction);
        return (int)count.ExecuteScalar();
    }

    private static int MaxNoteId(SqlConnection connection)
    {
        using var max = new SqlCommand("SELECT MAX(id) FROM " + Notes, connection);
        return (int)max.ExecuteScalar();
    }

    private static List<int> Ids(SqlConnection connection, DbTransaction? transaction)
    {
        using var select = new SqlCommand(
            "SELECT id FROM " + Notes + " ORDER BY id",
            connection,
            (SqlTransaction?)transaction
        );
        using var reader = select.ExecuteReader();
        var ids = new List<int>();
        while (reader.Read())
        {
            ids.Add(reader.GetInt32(0));
        }

        return ids;
    }
}
