using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.RecordingLog;
using static SqlSource.Tool.Tests.PostgresEventCases;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the describer says in the run's log of explain, of each node of the plan and of the walk.
public sealed class PostgresEventsPlanTests
{
    private static readonly DescribeRequest Joined = new(
        "ListUserTotals",
        "SELECT u.id, o.total FROM users u LEFT JOIN orders o ON o.user_id = u.id",
        new EquatableArray<SqlQueryParameter>([])
    );

    private static readonly PostgresColumnRow Total = PostgresTables.ColumnOf(
        1,
        "total",
        PostgresRows.Numeric.Oid,
        PostgresTables.Orders,
        3
    );

    // The plan of Joined: users is the side the join keeps.
    private static readonly string JoinedPlan = Text(
        Join("Left", Scan("u"), Scan("o")) with
        {
            Output = ["u.id", "o.total"],
        }
    );

    // The whole script of Joined, a gated query, with the answer of "explain" the test gives.
    private static PostgresScript JoinedScript(PostgresExplainAnswer explain) =>
        new PostgresScript()
            .Open()
            .Prepares(1, Joined.Sql, [], new PostgresPrepareAnswer([], null))
            .Columns(1, 0, PostgresTables.UsersId, Total)
            .Tables([PostgresTables.Users.Oid, PostgresTables.Orders.Oid], PostgresTables.Users, PostgresTables.Orders)
            .Resolves([23, 1700], PostgresRows.Int4, PostgresRows.Numeric)
            .Checks(Joined.Sql, [], PostgresTables.UsersId, Total)
            .Explains(1, 0, explain)
            .Frees(1);

    [Fact]
    public async Task DescribeAsync_GatedQuery_WritesExplainANodeForEachAColumnForEachAndWalkBeforeTheMatch()
    {
        var log = new RecordingLog();

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(JoinedPlan, null)), log, Joined);

        log.Events.Select(written => written.Name)
            .ShouldBe([
                "prepare",
                "columns",
                "tables",
                "types",
                "column",
                "column",
                "check",
                "explain",
                "walk.node",
                "walk.node",
                "walk.node",
                "walk.column",
                "walk.column",
                "walk",
                "table-match",
                "deallocate",
            ]);

        var explain = log.Events[7].Members;
        NamesOf(explain).ShouldBe(["query", "statement", "settings", "sql", "plan", "durationMs"]);
        Member(explain, "query").ShouldBe((LogValue)"ListUserTotals");
        Member(explain, "statement").ShouldBe((LogValue)"sqlsource_1");
        Member(explain, "settings").ShouldBe(LogValue.ListOfTexts([]));
        Member(explain, "sql").ShouldBe((LogValue)"EXPLAIN (VERBOSE, FORMAT JSON) EXECUTE sqlsource_1");
        Member(explain, "plan").ShouldBe((LogValue)JoinedPlan);
        Member(explain, "durationMs").ShouldBe((LogValue)(long)CallTime.TotalMilliseconds);

        var join = log.Events[8].Members;
        NamesOf(join)
            .ShouldBe(["depth", "nodeType", "joinType", "parentRelationship", "alias", "relation", "nullExtends"]);
        Member(join, "depth").ShouldBe((LogValue)0L);
        Member(join, "nodeType").ShouldBe((LogValue)"Hash Join");
        Member(join, "joinType").ShouldBe((LogValue)"Left");
        Member(join, "parentRelationship").ShouldBe(LogValue.Null);
        Member(join, "alias").ShouldBe(LogValue.Null);
        Member(join, "relation").ShouldBe(LogValue.Null);
        Member(join, "nullExtends").ShouldBe((LogValue)"inner");

        var orders = log.Events[10].Members;
        Member(orders, "depth").ShouldBe((LogValue)1L);
        Member(orders, "nodeType").ShouldBe((LogValue)"Seq Scan");
        Member(orders, "joinType").ShouldBe(LogValue.Null);
        Member(orders, "parentRelationship").ShouldBe((LogValue)"Inner");
        Member(orders, "alias").ShouldBe((LogValue)"o");
        Member(orders, "relation").ShouldBe(LogValue.MapOf(new("schema", "public"), new("table", "o")));
        Member(orders, "nullExtends").ShouldBe((LogValue)"none");

        var total = log.Events[12].Members;
        NamesOf(total)
            .ShouldBe([
                "ordinal",
                "name",
                "output",
                "alias",
                "column",
                "mapped",
                "nullExtended",
                "nullable",
                "nullableSource",
            ]);
        Member(total, "ordinal").ShouldBe((LogValue)1L);
        Member(total, "name").ShouldBe((LogValue)"total");
        Member(total, "output").ShouldBe((LogValue)"o.total");
        Member(total, "alias").ShouldBe((LogValue)"o");
        Member(total, "column").ShouldBe((LogValue)"total");
        Member(total, "mapped").ShouldBe((LogValue)true);
        Member(total, "nullExtended").ShouldBe((LogValue)true);
        Member(total, "nullable").ShouldBe((LogValue)true);
        Member(total, "nullableSource").ShouldBe((LogValue)"outer-join");

        var walk = log.Events[13].Members;
        NamesOf(walk).ShouldBe(["query", "plan", "reason"]);
        Member(walk, "query").ShouldBe((LogValue)"ListUserTotals");
        Member(walk, "plan").ShouldBe((LogValue)"walked");
        Member(walk, "reason").ShouldBe(LogValue.Null);
    }

    // The column event is written when the column is built, with the heuristic's answer: the walk's own event holds
    // what the sidecar records.
    [Fact]
    public async Task DescribeAsync_GatedQuery_KeepsTheHeuristicInTheColumnEventAndTheAnswerInTheWalks()
    {
        var log = new RecordingLog();

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(JoinedPlan, null)), log, Joined);

        var built = log.Events.First(written => written.Name == "column").Members;
        Member(built, "nullableSource").ShouldBe((LogValue)"heuristic");
        Member(built, "gated").ShouldBe((LogValue)true);
        var walked = log.Events.First(written => written.Name == "walk.column").Members;
        Member(walked, "nullable").ShouldBe((LogValue)false);
        Member(walked, "nullableSource").ShouldBe((LogValue)"catalog");
    }

    // The log is where a failed EXPLAIN is told from a bug of the walk.
    [Fact]
    public async Task DescribeAsync_ExplainThatFails_WritesItsErrorAndTheReasonAndNoNode()
    {
        var log = new RecordingLog();
        var refused = new PostgresServerError(
            "57014",
            "canceling statement due to statement timeout",
            null,
            null,
            null
        );

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(null, refused)), log, Joined);

        log.Events.Select(written => written.Name)
            .Skip(7)
            .ShouldBe(["explain", "walk.column", "walk.column", "walk", "table-match", "deallocate"]);
        var explain = log.Events[7].Members;
        NamesOf(explain).ShouldBe(["query", "statement", "settings", "sql", "error", "durationMs"]);
        Member(explain, "error")
            .ShouldBe(
                LogValue.MapOf(
                    new("sqlState", "57014"),
                    new("message", "canceling statement due to statement timeout"),
                    new("detail", LogValue.Null),
                    new("hint", LogValue.Null),
                    new("position", LogValue.Null)
                )
            );
        var column = log.Events[8].Members;
        Member(column, "output").ShouldBe(LogValue.Null);
        Member(column, "mapped").ShouldBe((LogValue)false);
        Member(column, "nullableSource").ShouldBe((LogValue)"heuristic");
        var walk = log.Events.Single(written => written.Name == "walk").Members;
        Member(walk, "plan").ShouldBe((LogValue)"unavailable");
        Member(walk, "reason").ShouldBe((LogValue)"explain-failed");
    }

    [Theory]
    [InlineData("not json", "unreadable")]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Append"}}]""",
        "no-output"
    )]
    public async Task DescribeAsync_PlanThatCannotBeUsed_WritesItsReason(string plan, string reason)
    {
        var log = new RecordingLog();

        await DescribeAsync(JoinedScript(new PostgresExplainAnswer(plan, null)), log, Joined);

        var walk = log.Events.Single(written => written.Name == "walk").Members;
        Member(walk, "plan").ShouldBe((LogValue)"unavailable");
        Member(walk, "reason").ShouldBe((LogValue)reason);
    }

    // A plan holds a user's names, and the connection's value is taken out of it as out of any string.
    [Fact]
    public async Task DescribeAsync_ThroughTheLogOfASession_RemovesTheSecretFromThePlanAndFromANode()
    {
        var log = new RecordingLog();

        await DescribeAsync(
            JoinedScript(new PostgresExplainAnswer(JoinedPlan, null)),
            new SessionRunLog(log, new ConnectionSecret("Seq Scan"), "postgres."),
            Joined
        );

        var plan = Member(log.Events.Single(written => written.Name == "postgres.explain").Members, "plan");
        plan.ShouldBeOfType<LogValue.Text>().Value.ShouldNotContain("Seq Scan");
        var scan = log.Events.Last(written => written.Name == "postgres.walk.node").Members;
        Member(scan, "nodeType").ShouldBe((LogValue)"***");
    }

    // The words of nullExtends are the log's contract, spelled apart from the members' names.
    [Theory]
    [InlineData(nameof(NullExtension.None), "none")]
    [InlineData(nameof(NullExtension.Inner), "inner")]
    [InlineData(nameof(NullExtension.Outer), "outer")]
    [InlineData(nameof(NullExtension.Both), "both")]
    public void TextOf_NullExtension_IsTheWordTheLogWrites(string member, string text) =>
        PostgresEvents.TextOf(Enum.Parse<NullExtension>(member)).ShouldBe(text);
}
