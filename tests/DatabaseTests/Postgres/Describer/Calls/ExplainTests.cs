using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer.Calls;

// "explain" against a real server: the plan is a generic one that PlanNode reads, and the call leaves the describe's
// transaction as it found it.
[Collection(UsesPostgres.Name)]
public sealed class ExplainTests(PostgresDatabases postgres)
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

    // The statement's parameters are the $n of the SQL, each of an inferred type.
    private async Task<PostgresExplainAnswer> ExplainPartitionedAsync(
        PostgresConnection connection,
        string sql,
        int parameters = 1
    )
    {
        var token = TestContext.Current.CancellationToken;
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);
        var live = new PostgresLive(connection);
        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(Statement, sql, [.. Enumerable.Repeat("unknown", parameters)]),
            token
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);
        return await live.ExplainAsync(new PostgresExplainRequest(Statement, parameters, []), token);
    }

    // The statements the connection holds prepared, by name.
    private static async Task<string[]> PreparedAsync(PostgresConnection connection)
    {
        var token = TestContext.Current.CancellationToken;
        await using var command = await connection.CommandAsync(
            "SELECT pg_catalog.array_agg(name ORDER BY name) FROM pg_catalog.pg_prepared_statements",
            token
        );
        return (string[])(await command.ExecuteScalarAsync(token)).ShouldNotBeNull();
    }

    // The executor prunes every partition for the NULL of the parameter, or for the value of a stable function, and the
    // plan is asked again without pruning, of a second statement in place of the one that cached the first plan: every
    // member shows, the second statement is freed, and the settings end with the call.
    [Theory]
    [InlineData("SELECT p.id, p.note FROM scratch_parts p WHERE p.kind = $1", 1)]
    [InlineData("SELECT p.id, p.note FROM scratch_parts p WHERE p.kind = extract(dow FROM now())::integer * 0 + 3", 0)]
    public async Task ExplainAsync_KeyThatTheExecutorPrunes_AnswersThePlanOfEveryPartition(string sql, int parameters)
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);

        var answer = await ExplainPartitionedAsync(connection, sql, parameters);

        answer.Error.ShouldBeNull(answer.Error?.Message);
        var root = PlanNode.Read(answer.Plan.ShouldNotBeNull()).ShouldNotBeNull();
        root.NodeType.ShouldBe("Append");
        root.SubplansRemoved.ShouldBe(0);
        root.Plans.Select(static member => member.RelationName).ShouldBe(["scratch_parts_1", "scratch_parts_2"]);
        (await PreparedAsync(connection)).ShouldBe([Statement]);
        (await ShowAsync(connection, "enable_partition_pruning")).ShouldBe("on");
        (await ShowAsync(connection, "plan_cache_mode")).ShouldBe("auto");
    }

    // An UPDATE that a constant prunes to one partition is asked again too, since the row it writes may move to a
    // partition the planner pruned: its partitions show as members.
    [Fact]
    public async Task ExplainAsync_UpdateOfAPrunedPartitionedTable_AnswersThePlanOfEveryPartition()
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);

        var answer = await ExplainPartitionedAsync(
            connection,
            "UPDATE scratch_parts p SET kind = 2 WHERE p.kind = 1 AND p.id = $1 RETURNING p.note"
        );

        var root = PlanNode.Read(answer.Plan.ShouldNotBeNull()).ShouldNotBeNull();
        root.SelfAndDescendants()
            .Where(static node => node.RelationName is not null && node.NodeType != "ModifyTable")
            .Select(static node => node.RelationName)
            .ShouldBe(["scratch_parts_1", "scratch_parts_2"]);
        (await PreparedAsync(connection)).ShouldBe([Statement]);
    }

    // The second statement cannot be prepared, as when a statement of its name is left on the backend: the answer is
    // the server's error, never the first plan, which the executor pruned, and the settings end with the call.
    [Fact]
    public async Task ExplainAsync_SecondStatementThatFails_AnswersItsError()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        await postgres.ExecuteAsync(ScratchSchema.Partitioned);
        var live = new PostgresLive(connection);
        var prepared = await live.PrepareAsync(
            new PostgresPrepareRequest(
                Statement,
                "SELECT p.id, p.note FROM scratch_parts p WHERE p.kind = $1",
                ["unknown"]
            ),
            token
        );
        prepared.Error.ShouldBeNull(prepared.Error?.Message);
        await using (var left = await connection.CommandAsync($"PREPARE {Statement}_unpruned AS SELECT 1", token))
        {
            _ = await left.ExecuteNonQueryAsync(token);
        }

        var answer = await live.ExplainAsync(new PostgresExplainRequest(Statement, 1, []), token);

        answer.Plan.ShouldBeNull();
        answer.Error.ShouldNotBeNull().SqlState.ShouldBe("42P05");
        (await ShowAsync(connection, "enable_partition_pruning")).ShouldBe("on");
    }

    // A plan that needs every partition, of a statement that this connection's calls did not prepare, has no text to
    // prepare again: the answer is no plan, never the pruned one.
    [Fact]
    public async Task ExplainAsync_PrunedStatementPreparedElsewhere_AnswersNoPlan()
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);
        _ = await ExplainPartitionedAsync(connection, "SELECT p.id, p.note FROM scratch_parts p WHERE p.kind = $1");

        var answer = await new PostgresLive(connection).ExplainAsync(
            new PostgresExplainRequest(Statement, 1, []),
            token
        );

        answer.ShouldBe(new PostgresExplainAnswer(null, null));
    }

    // The planner prunes for a constant, and prints no count: the plan with pruning is the answer, a scan of the one
    // partition left under the query's alias, where a plan without pruning would show both.
    [Fact]
    public async Task ExplainAsync_ConstantOfThePartitionKey_AnswersThePlanWithPruning()
    {
        await using var connection = new PostgresConnection(postgres.ConnectionString, NullRunLog.Instance);

        var answer = await ExplainPartitionedAsync(
            connection,
            "SELECT p.id, p.note FROM scratch_parts p WHERE p.kind = 1 AND p.id = $1"
        );

        var root = PlanNode.Read(answer.Plan.ShouldNotBeNull()).ShouldNotBeNull();
        (root.NodeType, root.Alias, root.RelationName).ShouldBe(("Seq Scan", "p", "scratch_parts_1"));
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
