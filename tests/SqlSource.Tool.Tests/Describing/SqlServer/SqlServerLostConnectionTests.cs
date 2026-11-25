using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// A connection that a call's error ended is lost for every later call, whatever state SqlClient reports, and
// SqlClient's own refusal of a command on it is the lost connection, never a bug.  SqlException has no public
// constructor, so an error of the server that ends the session is held by SessionTests of
// tests/DatabaseTests/SqlServer, which kills a session between two calls.  No call here reaches a server: the
// connection is never opened.
public sealed class SqlServerLostConnectionTests
{
    // SqlClient's text when it refuses a command on a connection the server ended, whose state it reports as open or
    // as broken.
    private const string NotAvailable =
        "BeginExecuteReader requires an open and available Connection. The connection's current state is open.";

    [Fact]
    public void IsLost_NewConnection_IsFalse() => Connection().IsLost.ShouldBeFalse();

    [Fact]
    public void IsLost_AfterLose_IsTrue()
    {
        var connection = Connection();

        connection.Lose();

        connection.IsLost.ShouldBeTrue();
    }

    // A lost connection refuses the next command before SqlClient is asked, with the lost connection's text.
    [Fact]
    public async Task CommandAsync_AfterLose_IsTheLostConnection()
    {
        var connection = Connection();
        connection.Lose();

        var refused = await Should.ThrowAsync<SqlServerConnection.UnusableException>(() =>
            connection.CommandAsync("SELECT 1", TestContext.Current.CancellationToken)
        );

        refused.Message.ShouldBe(ConnectionTexts.LostConnection);
    }

    [Fact]
    public async Task AnswerAsync_SqlClientsRefusalOnALostConnection_IsTheLostConnection()
    {
        var connection = Connection();
        connection.Lose();

        var answer = await AnswerAsync(connection, () => throw new InvalidOperationException(NotAvailable));

        answer.Errors.ShouldBe(ConnectionErrors.Own(ConnectionTexts.LostConnection).Errors);
    }

    // While the connection is not lost, an InvalidOperationException is a bug of the describer and stays one.
    [Fact]
    public async Task AnswerAsync_InvalidOperationOnAConnectionNotLost_IsABug()
    {
        var connection = Connection();

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            AnswerAsync(connection, () => throw new InvalidOperationException(NotAvailable))
        );
        connection.IsLost.ShouldBeFalse();
    }

    [Fact]
    public async Task AnswerAsync_UnusableException_IsItsText() =>
        (
            await AnswerAsync(
                Connection(),
                () => throw new SqlServerConnection.UnusableException(ConnectionErrors.EmptyConnectionString)
            )
        ).Errors.ShouldBe(ConnectionErrors.Own(ConnectionErrors.EmptyConnectionString).Errors);

    private static SqlServerConnection Connection() =>
        new("Server=localhost;User ID=sa;Password=unused;Connect Timeout=1", NullRunLog.Instance);

    // The error of the call's answer, which is the error itself here.
    private static Task<SqlServerServerError> AnswerAsync(
        SqlServerConnection connection,
        Func<Task<SqlServerServerError>> call
    ) =>
        SqlServerLive.AnswerAsync(
            call,
            static _ => throw new InvalidOperationException("No SqlException is thrown here."),
            static error => error,
            connection,
            TestContext.Current.CancellationToken
        );
}
