using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// "explain" against a real server: the plan is a generic one that PlanNode reads, and the call leaves the describe's
// transaction as it found it.
[Collection(UsesPostgres.Name)]
public sealed class ExplainTests(PostgresContainer postgres)
{
    private const string Statement = "walk_explain";

    private const string LeftJoin =
        "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = $1";

    private static async Task<string> ShowAsync(PostgresConnection connection, string setting)
    {
        var token = TestContext.Current.CancellationToken;
        await using var command = await connection.CommandAsync($"SHOW {setting}", token);
        return (string)(await command.ExecuteScalarAsync(token)).ShouldNotBeNull();
    }

    private static async Task PrepareAsync(PostgresLive live)
    {
        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(Statement, LeftJoin, ["unknown"]),
            TestContext.Current.CancellationToken
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);
    }

    // The NULL of the parameter is not folded: the condition on $1 is in the plan, and the join is still there.
    [Fact]
    public async Task ExplainAsync_PreparedStatement_AnswersAGenericPlanThatPlanNodeReads()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);
        await PrepareAsync(live);

        var answer = await live.ExplainAsync(new PostgresExplainRequest(Statement, 1, []), token);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        var text = answer.Plan.ShouldNotBeNull();
        text.ShouldContain("$1");
        text.ShouldNotContain("One-Time Filter");
        var root = PlanNode.Read(text).ShouldNotBeNull();
        root.JoinType.ShouldBe("Left");
        root.Output.ShouldBe(["u.id", "o.total"]);
        root.Plans.Select(static child => child.Alias).ShouldBe(["u", "o"]);
    }

    [Theory]
    [InlineData("enable_mergejoin=off", "enable_nestloop=off", "Hash Join")]
    [InlineData("enable_hashjoin=off", "enable_nestloop=off", "Merge Join")]
    [InlineData("enable_hashjoin=off", "enable_mergejoin=off", "Nested Loop")]
    public async Task ExplainAsync_WithTwoMethodsOff_PlansWithTheThirdAndLeavesTheSettingsAsTheyWere(
        string first,
        string second,
        string expected
    )
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);
        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(
                Statement,
                "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.name = $1",
                ["unknown"]
            ),
            token
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);

        var answer = await live.ExplainAsync(new PostgresExplainRequest(Statement, 1, [first, second]), token);

        PlanNode.Read(answer.Plan.ShouldNotBeNull()).ShouldNotBeNull().NodeType.ShouldBe(expected);
        (await ShowAsync(connection, "plan_cache_mode")).ShouldBe("auto");
        (await ShowAsync(connection, "enable_hashjoin")).ShouldBe("on");
        (await ShowAsync(connection, "enable_mergejoin")).ShouldBe("on");
        (await ShowAsync(connection, "enable_nestloop")).ShouldBe("on");
    }

    // The error is the answer, the describe's transaction is rolled back to its savepoint, which ends the settings,
    // and the describe goes on.
    [Fact]
    public async Task ExplainAsync_StatementThatWasNotPrepared_AnswersTheErrorAndTheDescribeGoesOn()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);
        await PrepareAsync(live);

        var answer = await live.ExplainAsync(new PostgresExplainRequest("walk_nothing", 0, []), token);

        answer.Plan.ShouldBeNull();
        answer.Error.ShouldNotBeNull().SqlState.ShouldBe("26000");

        // A SHOW in a transaction that an error had aborted would be refused: the rollback was made.
        (await ShowAsync(connection, "plan_cache_mode")).ShouldBe("auto");
        connection.InDescribe.ShouldBeTrue();
    }

    // An UPDATE is planned and not executed: its row is unchanged.
    [Fact]
    public async Task ExplainAsync_Update_ChangesNoRow()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        var live = new PostgresLive(connection);
        await using (var made = await connection.CommandAsync(ScratchSchema.ExplainRows, token))
        {
            _ = await made.ExecuteNonQueryAsync(token);
        }

        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(Statement, "UPDATE walk_explain_rows SET label = 'changed' WHERE id = 1", []),
            token
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);

        var answer = await live.ExplainAsync(new PostgresExplainRequest(Statement, 0, []), token);

        PlanNode.Read(answer.Plan.ShouldNotBeNull()).ShouldNotBeNull().NodeType.ShouldBe("ModifyTable");
        await using var after = await connection.CommandAsync(
            "SELECT label FROM walk_explain_rows WHERE id = 1",
            token
        );
        (await after.ExecuteScalarAsync(token)).ShouldBe("kept");
    }
}
