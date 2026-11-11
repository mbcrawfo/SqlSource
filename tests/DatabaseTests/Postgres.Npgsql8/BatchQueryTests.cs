using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The collection methods on Npgsql 8, which names the command of a database error in a batch, BatchCommand, where
// Npgsql 10 names none unless the connection string asks it to: the end-to-end project proves the methods on Npgsql 10,
// where the method finds the failed item by the commands that completed, and this class holds that it takes the item
// that Npgsql 8 names, with a reader and without one, and from the repository's copy.  Each test makes a temporary
// table notes on its own connection, which the queries' notes names, so that the method's own transaction leaves the
// seed as it was.
[Collection(UsesApp.Name)]
public sealed class BatchQueryTests(AppContainer app)
{
    // The third item repeats the first's id, which the primary key refuses, and the rollback takes the first two.
    [Fact]
    public async Task InsertNote_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwo()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Second.", 2), new(101, "Again.", 3)];

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(items, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // The same failure, from a method that runs its batch without a reader.
    [Fact]
    public async Task InsertNoteRowCount_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwo()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Second.", 2), new(101, "Again.", 3)];

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNoteRowCount(items, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // The same failure, from the repository's copy, which runs on the borrowed connection in a transaction of its own.
    [Fact]
    public async Task InsertNoteInRepository_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwo()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await OpenWithTemporaryNotesAsync(source, token);
        NoteToAdd[] items = [new(101, "First.", 1), new(102, "Second.", 2), new(101, "Again.", 3)];

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            new BatchQueries(connection).InsertNoteInRepository(items, token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<PostgresException>().SqlState.ShouldBe("23505");
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

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
}
