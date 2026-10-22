using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tests.SqlServer.Describer;

// "columns" of a text that would harm the server if it ran, each in a session of its own: the procedure analyses the
// text and runs none of it.  The canary of the schema, dbo.victim, must be there afterwards, and the server must
// answer.
[Collection(UsesSqlServer.Name)]
public sealed class SafetyTests(SqlServerContainer sqlServer)
{
    // How long the call of the text that waits 30 seconds may take: far less than its delay, and generous enough for a
    // server under emulation.
    private static readonly TimeSpan AtOnce = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData("SELECT 1 AS a; DROP TABLE dbo.victim")]
    [InlineData("DROP TABLE dbo.victim; SELECT 1 AS a")]
    public async Task ColumnsAsync_TextThatDropsATable_DescribesItAndRunsNothing(string sql)
    {
        var (answer, _) = await ColumnsAsync(sql);

        VisibleNames(answer).ShouldBe(["a"]);
        await ShouldStandAsync();
    }

    // The server does not wait: TimeProvider.System, since the time the call takes on the machine is what is tested.
    [Fact]
    public async Task ColumnsAsync_TextThatWaits_AnswersAtOnce()
    {
        var (answer, elapsed) = await ColumnsAsync("WAITFOR DELAY '00:00:30'; SELECT 1 AS a");

        VisibleNames(answer).ShouldBe(["a"]);
        elapsed.ShouldBeLessThan(AtOnce);
    }

    [Fact]
    public async Task ColumnsAsync_TextThatShutsTheServerDown_HasNoRowsAndTheServerAnswers()
    {
        var (answer, _) = await ColumnsAsync("SHUTDOWN WITH NOWAIT");

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

    // The answer of "columns" for the SQL, with no declarations, and how long the call alone took.
    private async Task<(SqlServerColumnsAnswer Answer, TimeSpan Elapsed)> ColumnsAsync(string sql)
    {
        await using var session = await Sessions.OpenSessionAsync(sqlServer.ConnectionString);
        var live = new SqlServerLive(session.Connection);

        var started = TimeProvider.System.GetTimestamp();
        var answer = await live.ColumnsAsync(
            new SqlServerColumnsRequest(sql, null),
            TestContext.Current.CancellationToken
        );
        return (answer, TimeProvider.System.GetElapsedTime(started));
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
