using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.SqlServer.Describer;

// "columns" of a text that would harm the server if it ran, each in a session of its own: the procedure analyses the
// text and runs none of it.  The canary of the schema, dbo.victim, must be there afterwards, and the server must
// answer.
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
