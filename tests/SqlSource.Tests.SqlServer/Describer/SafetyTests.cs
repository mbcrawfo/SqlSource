using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.SqlServer.Describer;

// "columns" of a text that would harm the server if it ran, each in a session of its own: the procedure analyses the
// text and runs none of it.  The canary of the schema, dbo.victim, must be there afterwards, and the server must
// answer.  "operands", whose text is the query's own tokens rearranged, is sent the same way, and runs none of it
// either.
[Collection(UsesSqlServer.Name)]
public sealed class SafetyTests(SqlServerContainer sqlServer)
{
    [Theory]
    [InlineData("SELECT 1 AS a; DROP TABLE dbo.victim")]
    [InlineData("DROP TABLE dbo.victim; SELECT 1 AS a")]
    public async Task ColumnsAsync_TextThatDropsATable_DescribesItAndRunsNothing(string sql)
    {
        var answer = await ColumnsAsync(sql);

        VisibleNames(answer).ShouldBe(["a"]);
        await ShouldStandAsync();
    }

    // The server does not wait.  Had it run the wait of a day, SqlClient's command timeout would have ended the call
    // with an error and no rows, or the test's patience would have ended the test.
    [Fact]
    public async Task ColumnsAsync_TextThatWaits_DescribesItAndWaitsForNothing()
    {
        var answer = await Eventually.WithinAsync(
            ColumnsAsync("WAITFOR DELAY '23:59:59'; SELECT 1 AS a"),
            "The call of a text that waits did not answer."
        );

        answer.Error.ShouldBeNull();
        VisibleNames(answer).ShouldBe(["a"]);
    }

    [Fact]
    public async Task ColumnsAsync_TextThatShutsTheServerDown_HasNoRowsAndTheServerAnswers()
    {
        var answer = await ColumnsAsync("SHUTDOWN WITH NOWAIT");

        answer.Error.ShouldBeNull(
            string.Join("; ", answer.Error?.Errors.Select(error => $"Msg {error.Number}: {error.Message}") ?? [])
        );
        answer.Errors.ShouldBeNull(
            string.Join("; ", answer.Errors?.Select(error => $"Msg {error.Number}: {error.Message}") ?? [])
        );
        answer.Rows.ShouldNotBeNull().ShouldBeEmpty();
        (await sqlServer.ScalarAsync("SELECT 1")).ShouldBe(1);
        await ShouldStandAsync();
    }

    // A describe whose operand would take a value of a sequence if it ran: "operands" asks about it, and the sequence
    // has given none, so its first value is still its start.
    [Fact]
    public async Task DescribeAsync_OperandThatTakesASequenceValue_AsksOperandsAndRunsNothing()
    {
        await sqlServer.ExecuteAsync("DROP SEQUENCE IF EXISTS dbo.safety_sequence");
        await sqlServer.ExecuteAsync("CREATE SEQUENCE dbo.safety_sequence AS int START WITH 1");
        try
        {
            var (described, capture) = await Sessions.DescribeCapturedAsync(
                sqlServer.ConnectionString,
                DescribeRequests.Of(
                    SqlDialectRules.SqlServer,
                    "SELECT CAST(NEXT VALUE FOR dbo.safety_sequence AS bigint) AS n"
                )
            );

            described.Failure.ShouldBeNull(described.Failure?.Descriptor.Id);
            capture.Calls.Select(static call => call.Method).ShouldContain("operands");
            (await sqlServer.ScalarAsync("SELECT NEXT VALUE FOR dbo.safety_sequence")).ShouldBe(1);
        }
        finally
        {
            await sqlServer.ExecuteAsync("DROP SEQUENCE dbo.safety_sequence");
        }
    }

    // The answer of "columns" for the SQL, with no declarations.
    private async Task<SqlServerColumnsAnswer> ColumnsAsync(string sql)
    {
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        var live = new SqlServerLive(session.Connection);

        return await live.ColumnsAsync(new SqlServerColumnsRequest(sql, null), TestContext.Current.CancellationToken);
    }

    // The names of the answer's visible columns, which must be there.
    private static string?[] VisibleNames(SqlServerColumnsAnswer answer) =>
        [.. answer.Rows.ShouldNotBeNull().Where(static row => !row.IsHidden).Select(static row => row.Name)];

    // The canary is still there.
    private async Task ShouldStandAsync() =>
        (await sqlServer.ScalarAsync("SELECT OBJECT_ID('dbo.victim')")).ShouldBeOfType<int>(
            "the call dropped the table"
        );
}
