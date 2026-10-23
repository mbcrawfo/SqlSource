using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Tests.PostgresSessionCases;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: the name of each statement, its prepare and deallocate, and a statement
// the server no longer holds or already holds.  A call the script does not hold is a CaptureMissException, so a
// describe that asks more than its test wrote fails.
public sealed class PostgresSessionStatementTests
{
    // The trailing ";" is not sent, and the statement is freed when the describe is done.
    [Fact]
    public async Task DescribeAsync_Query_AsksPrepareThenDeallocateForTheStatement()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples WHERE flag", [], new PostgresPrepareAnswer([], null));
        _ = script.Checks("DELETE FROM samples WHERE flag", []);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Listed("DELETE FROM samples WHERE flag;"),
            TestContext.Current.CancellationToken
        );

        _ = described.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // A name is a counter within its session, so that a capture is the same each time it is recorded.
    [Fact]
    public async Task DescribeAsync_SecondQuery_TakesTheNextName()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        _ = script.Checks("DELETE FROM samples", []);
        _ = script.Frees(1);
        _ = script.PreparesWithNoColumns(2, "DELETE FROM orders", [], new PostgresPrepareAnswer([], null));
        _ = script.Checks("DELETE FROM orders", []);
        var exchange = script.Frees(2).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        _ = await session.DescribeAsync(Listed("DELETE FROM samples"), TestContext.Current.CancellationToken);
        var second = await session.DescribeAsync(Listed("DELETE FROM orders"), TestContext.Current.CancellationToken);

        _ = second.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // There is no statement to free, so "deallocate" is not asked: the script holds none for the first name.  The
    // counter advances all the same.
    [Fact]
    public async Task DescribeAsync_PrepareThatFails_IsTheFailureFreesNothingAndTheNextTakesTheNextName()
    {
        // "PREPARE sqlsource_1 AS " is 23 characters, and nope is the 13th of the SQL.
        var rejected = new PostgresServerError("42P01", "relation \"nope\" does not exist", null, null, 36);
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM nope", [], new PostgresPrepareAnswer(null, rejected));
        _ = script.PreparesWithNoColumns(2, "DELETE FROM orders", [], new PostgresPrepareAnswer([], null));
        _ = script.Checks("DELETE FROM orders", []);
        var exchange = script.Frees(2).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var first = await session.DescribeAsync(Listed("DELETE FROM nope"), TestContext.Current.CancellationToken);
        var second = await session.DescribeAsync(Listed("DELETE FROM orders"), TestContext.Current.CancellationToken);

        var failure = first.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC225");
        failure.Arguments.ShouldBe(["Q", "relation \"nope\" does not exist"]);
        failure.ServerLines.ShouldBe(["42P01: relation \"nope\" does not exist", "at character 13"]);
        _ = second.Value.ShouldNotBeNull();
        exchange.Unused.ShouldBeEmpty();
    }

    // The error of "deallocate" is logged and never reported: not even 26000, which anywhere else is SQLSRC236.
    [Fact]
    public async Task DescribeAsync_DeallocateAnswers26000_IsDescribed()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        _ = script.Checks("DELETE FROM samples", []);
        var exchange = script.Frees(1, new PostgresDeallocateAnswer(gone)).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(
            Listed("DELETE FROM samples"),
            TestContext.Current.CancellationToken
        );

        _ = described.Value.ShouldNotBeNull();
        described.Failure.ShouldBeNull();
        var (_, members) = log.Events.Last(written => written.Name == "deallocate");
        var error = members.Single(member => member.Name == "error").Value.ShouldBeOfType<LogValue.Members>();
        error.Values[0].ShouldBe(new LogMember("sqlState", "26000"));
    }

    // 42P05 at "prepare": a backend that a past run left a statement on.  The statement is not ours to free, so
    // "deallocate" is not asked: the script holds none.
    [Fact]
    public async Task DescribeAsync_PrepareAnswers42P05_IsSqlsrc236AndFreesNothing()
    {
        var exists = new PostgresServerError(
            "42P05",
            "prepared statement \"sqlsource_1\" already exists",
            null,
            null,
            null
        );
        var exchange = new PostgresScript()
            .Open()
            .Prepares(1, "DELETE FROM samples", [], new PostgresPrepareAnswer(null, exists))
            .Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Listed("DELETE FROM samples"),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Arguments.ShouldBe([PostgresScript.Database]);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        exchange.Unused.ShouldBeEmpty();
    }

    // The statement that was prepared is not listed: PostgresLive answers the fixed message, with no SQLSTATE.
    [Fact]
    public async Task DescribeAsync_PrepareAnswersStatementNotListed_IsSqlsrc236()
    {
        var unlisted = new PostgresServerError(null, PostgresLive.StatementNotListed, null, null, null);
        var exchange = new PostgresScript()
            .Open()
            .Prepares(1, "DELETE FROM samples", [], new PostgresPrepareAnswer(null, unlisted))
            .Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Listed("DELETE FROM samples"),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.ServerLines.ShouldBe([PostgresLive.StatementNotListed]);
        exchange.Unused.ShouldBeEmpty();
    }

    // The statement went with the backend that held it: the free is still asked.
    [Fact]
    public async Task DescribeAsync_ColumnsAnswers26000_IsSqlsrc236AtDescribeColumns()
    {
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, "DELETE FROM samples", [], new PostgresPrepareAnswer([], null));
        _ = script.Answer(
            "columns",
            new PostgresColumnsRequest("sqlsource_1", 0),
            new PostgresColumnsAnswer(null, gone)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Listed("DELETE FROM samples"),
            TestContext.Current.CancellationToken
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Arguments.ShouldBe([PostgresScript.Database]);
        failure.Step.ShouldBe(DescribeStep.DescribeColumns);
        exchange.Unused.ShouldBeEmpty();
    }

    // "explain" is the one call after "check" that names the statement and reports, so it finds out too.  A session
    // that lost its statement is no reason for the heuristic: the describe fails, and the statement is freed.
    [Fact]
    public async Task DescribeAsync_ExplainAnswers26000_IsSqlsrc236AtExplain()
    {
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, GatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            GatedUsersId,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        _ = script.Explains(1, 0, new PostgresExplainAnswer(null, gone));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Listed(GatedUsersId), TestContext.Current.CancellationToken);

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Arguments.ShouldBe([PostgresScript.Database]);
        failure.Step.ShouldBe(DescribeStep.Explain);
        failure.ServerLines.ShouldBe(["26000: prepared statement \"sqlsource_1\" does not exist"]);
        exchange.Unused.ShouldBeEmpty();
    }

    // A query planned for its expression alone finds out the same way.
    [Fact]
    public async Task DescribeAsync_UngatedQueryWithAnExpressionExplainAnswers26000_IsSqlsrc236AtExplain()
    {
        var gone = new PostgresServerError(
            "26000",
            "prepared statement \"sqlsource_1\" does not exist",
            null,
            null,
            null
        );
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, UngatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            UngatedUsersId,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        _ = script.Explains(1, 0, new PostgresExplainAnswer(null, gone));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Listed(UngatedUsersId), TestContext.Current.CancellationToken);

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC236");
        failure.Step.ShouldBe(DescribeStep.Explain);
        exchange.Unused.ShouldBeEmpty();
    }
}
