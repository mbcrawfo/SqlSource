using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// "explain" against a real server: the plan is a generic one that PlanNode reads, and the call leaves the session
// as it found it.
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
        await using var connection = new PostgresConnection(postgres.ConnectionString);
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
        await using var connection = new PostgresConnection(postgres.ConnectionString);
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

    // The error is the answer, the transaction is rolled back, and the session goes on.
    [Fact]
    public async Task ExplainAsync_StatementThatWasNotPrepared_AnswersTheErrorAndLeavesNoTransaction()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString);
        var live = new PostgresLive(connection);

        var answer = await live.ExplainAsync(new PostgresExplainRequest("walk_nothing", 0, []), token);

        answer.Plan.ShouldBeNull();
        answer.Error.ShouldNotBeNull().SqlState.ShouldBe("26000");

        // A SHOW in a transaction that an error had ended would be refused: the rollback was made.
        (await ShowAsync(connection, "plan_cache_mode")).ShouldBe("auto");
    }

    // A table of this test alone with one row, made by SQL that can be run again, and left: the container is
    // thrown away, and the fixtures are recorded in one of their own.
    private const string Rows = """
        CREATE TABLE IF NOT EXISTS walk_explain_rows (id integer PRIMARY KEY, label text);
        INSERT INTO walk_explain_rows VALUES (1, 'kept') ON CONFLICT (id) DO UPDATE SET label = 'kept';
        """;

    // An UPDATE is planned and not run.
    [Fact]
    public async Task ExplainAsync_Update_RunsNothing()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString);
        var live = new PostgresLive(connection);
        await using (var made = await connection.CommandAsync(Rows, token))
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
