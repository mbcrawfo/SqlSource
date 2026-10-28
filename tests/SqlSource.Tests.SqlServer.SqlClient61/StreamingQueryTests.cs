using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.SqlServer.SqlClient61;

// The streaming method on SqlClient 6.1, the oldest driver that is supported, which drains a reader that is disposed
// by its own code: the end-to-end project proves the method on SqlClient 7, and this class holds the two things that
// depend on that code, that a stream yields every row and that a break partway through many rows does not cancel the
// command.
[Collection(UsesApp.Name)]
public sealed class StreamingQueryTests(AppContainer app)
{
    // The seed's three accounts.
    private static readonly CollectedAccount[] Accounts = [new(1, "alice"), new(2, "bob"), new(3, "carol")];

    // The count of the numbers that a break leaves unread.  The query sorts them, so the server has all of them before
    // it sends the first and is still sending when the loop stops.
    private const int ManyRows = 1_000_000;

    [Fact]
    public async Task StreamAccounts_EveryAccount_YieldsEveryRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(app.ConnectionString);

        var streamed = await connection.StreamAccounts(cancellationToken: token).ToListAsync(token);

        streamed.ShouldBe(Accounts);
    }

    // A break reads and discards the rest of the result, and the statement after it runs: SET CONTEXT_INFO marks the
    // session of the connection, which stays open, and the mark is there only when the batch ran to its end, where a
    // command cancelled by the break, with a million numbers still to send, would not have reached it.
    [Fact]
    public async Task StreamNumbersThenMark_BreakPartway_RunsTheStatementAfterTheNumbers()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(app.ConnectionString);
        await connection.OpenAsync(token);
        // Keep this clear and the read below in step with tests/SqlSource.Tests.SqlServer/EndToEnd/SessionMark.cs.
        await using (var clear = new SqlCommand("SET CONTEXT_INFO 0x00", connection))
        {
            _ = await clear.ExecuteNonQueryAsync(token);
        }

        var rows = 0;
        await foreach (
            var row in connection.StreamNumbersThenMark(
                new StreamNumbersThenMarkArgs(ManyRows),
                cancellationToken: token
            )
        )
        {
            if (++rows == 10)
            {
                break;
            }
        }

        rows.ShouldBe(10);
        await using var select = new SqlCommand("SELECT CAST(SUBSTRING(CONTEXT_INFO(), 1, 1) AS int)", connection);
        (await select.ExecuteScalarAsync(token)).ShouldBe(1);
    }
}
