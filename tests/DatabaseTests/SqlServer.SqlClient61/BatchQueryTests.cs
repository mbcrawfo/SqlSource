using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.SqlClient61;

// The collection methods on SqlClient 6.1, the oldest driver that is supported, which names the command of a database
// error in a batch, BatchCommand: the end-to-end project proves the methods on SqlClient 7, and this class holds that
// the method takes the item that SqlClient 6.1 names, with a reader and without one, and from the repository's copy.
// Each test makes a temporary table #notes on its own connection, which the inserts' token names, so that the method's
// own transaction leaves the seed as it was.
[Collection(UsesApp.Name)]
public sealed class BatchQueryTests(AppContainer app)
{
    // The temporary table that the inserts write into, by their token.
    private const string Notes = "#notes";

    // The error number of a duplicate key.
    private const int DuplicateKey = 2627;

    // The third item repeats the first's id, which the primary key refuses, and the rollback takes the first two.
    private static readonly NoteToAdd[] Items =
    [
        new(101, "First.", 1),
        new(102, "Second.", 2),
        new(101, "Again.", 3),
        new(104, "Fourth.", 1),
    ];

    [Fact]
    public async Task InsertNote_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwo()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNote(Items, Notes, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // The same failure, from a method that runs its batch without a reader.
    [Fact]
    public async Task InsertNoteRowCount_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwo()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            connection.InsertNoteRowCount(Items, Notes, cancellationToken: token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // The same failure, from the repository's copy, which runs on the borrowed connection in a transaction of its own.
    [Fact]
    public async Task InsertNoteInRepository_DuplicateKeyAtItemTwo_ThrowsCollectionItemExceptionAtTwo()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = await OpenWithTemporaryNotesAsync(token);

        var thrown = await Should.ThrowAsync<CollectionItemException>(() =>
            new BatchQueries(connection).InsertNoteInRepository(Items, Notes, token)
        );

        thrown.ItemIndex.ShouldBe(2);
        thrown.InnerException.ShouldBeOfType<SqlException>().Number.ShouldBe(DuplicateKey);
        (await CountNotesAsync(connection, token)).ShouldBe(0);
    }

    // A connection of "app" with the temporary table #notes, empty, with the columns of dbo.notes that the inserts
    // write and no identity, which ends with the session.
    private async Task<SqlConnection> OpenWithTemporaryNotesAsync(CancellationToken token)
    {
        var connection = new SqlConnection(app.ConnectionString);
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

    private static async Task<int> CountNotesAsync(SqlConnection connection, CancellationToken token)
    {
        await using var count = new SqlCommand("SELECT COUNT(*) FROM " + Notes, connection);
        return (int)await count.ExecuteScalarAsync(token);
    }
}
