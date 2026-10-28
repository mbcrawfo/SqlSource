using System.Linq;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Npgsql8;

// The streaming method on Npgsql 8, the oldest driver that is supported, which drains a reader that is disposed by its
// own code: the end-to-end project proves the method on Npgsql 10, and this class holds the two things that depend on
// that code, that a stream yields every row and that a break partway through many rows does not cancel the command.
[Collection(UsesApp.Name)]
public sealed class StreamingQueryTests(AppContainer app)
{
    // The seed's three accounts.
    private static readonly CollectedAccount[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    // The count of the numbers that a break leaves unread.  The query sorts them, so the server is still sending when
    // the loop stops, and a stream that cancelled its command on the break would abort the transaction of the caller;
    // with 10,000 rows the server has sent them all by then and a cancel changes nothing.
    private const int ManyRows = 1_000_000;

    [Fact]
    public async Task StreamAccounts_EveryAccount_YieldsEveryRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = source.CreateConnection();

        var streamed = await connection.StreamAccounts(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe(Accounts);
    }

    // The break ends the stream while the transaction is open and the server is still sending rows: the stream's reader
    // is closed and its command is not cancelled, which would abort the transaction, so the commit that follows commits
    // what the transaction did.  A temporary table keeps what the transaction writes out of the seed, which every test
    // of the collection shares, and lives as long as the connection.
    [Fact]
    public async Task StreamNumbers_BreakPartwayInATransaction_TheTransactionCommits()
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(app.ConnectionString);
        await using var connection = await source.OpenConnectionAsync(token);
        await using (var create = new NpgsqlCommand("CREATE TEMPORARY TABLE streamed (n integer)", connection))
        {
            _ = await create.ExecuteNonQueryAsync(token);
        }

        await using var transaction = await connection.BeginTransactionAsync(token);
        await using (var insert = new NpgsqlCommand("INSERT INTO streamed (n) VALUES (7)", connection, transaction))
        {
            _ = await insert.ExecuteNonQueryAsync(token);
        }

        var rows = 0;
        await foreach (
            var row in connection.StreamNumbers(
                new StreamNumbersArgs(ManyRows),
                transaction: transaction,
                cancellationToken: token
            )
        )
        {
            if (++rows == 10)
            {
                break;
            }
        }

        await transaction.CommitAsync(token);

        rows.ShouldBe(10);
        await using var select = new NpgsqlCommand("SELECT n FROM streamed", connection);
        (await select.ExecuteScalarAsync(token)).ShouldBe(7);
    }
}
