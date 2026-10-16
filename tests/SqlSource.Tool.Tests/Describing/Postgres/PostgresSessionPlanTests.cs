using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresRequests;
using static SqlSource.Tool.Tests.PostgresSessionCases;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: the gate, the plan it asks for with the planner's settings, and the walk
// of that plan.  A call the script does not hold is a CaptureMissException, so a describe that asks more than its test
// wrote fails.
public sealed class PostgresSessionPlanTests
{
    // One script for a query of users.id and an aggregate, whatever its SQL says of a join.
    private static async Task<QueryDescription> DescribeUsersIdAsync(string sql, PostgresExplainAnswer? explain = null)
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, sql, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            sql,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        if (explain is not null)
        {
            _ = script.Explains(1, 0, explain);
        }

        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return described.Value.ShouldNotBeNull();
    }

    // A column with no origin is answered from the plan, so the plan is asked for, and the column of the table keeps
    // the catalog's answer: without a word of the gate no outer join can have touched it.
    [Fact]
    public async Task DescribeAsync_QueryWithoutAGateWordWithAnExpression_AsksForThePlanAndKeepsTheCatalogsAnswer()
    {
        var plan = Over("WindowAgg", Scan("users")) with { Output = ["users.id", "count(*) OVER w1"] };

        var description = await DescribeUsersIdAsync(UngatedUsersId, PostgresScript.PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullExpression]);
    }

    // A count the session's path may find in another schema than pg_catalog is no count the walk knows.
    [Fact]
    public async Task DescribeAsync_SearchPathThatPutsTheCatalogLast_LeavesAFunctionUnknown()
    {
        var users = PostgresTables.Users;
        var plan = Over("WindowAgg", Scan("users")) with { Output = ["users.id", "count(*) OVER w1"] };
        var script = new PostgresScript().Answer(
            "open",
            PostgresOpenRequest.Instance,
            PostgresScript.Opened with
            {
                Settings =
                [
                    new PostgresSetting("search_path", "public, pg_catalog"),
                    new PostgresSetting("plan_cache_mode", "auto"),
                    new PostgresSetting("standard_conforming_strings", "on"),
                ],
            }
        );
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
        _ = script.Explains(1, 0, PostgresScript.PlanOf(plan));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(UngatedUsersId), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        AnswersOf(described.Value.ShouldNotBeNull()).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    [Fact]
    public async Task DescribeAsync_QueryWhoseEveryColumnHasAnOrigin_NeedsNoPlan()
    {
        const string Sql = "SELECT id FROM users";
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, Sql, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId);
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks(Sql, [], PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(Sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        var description = described.Value.ShouldNotBeNull();
        description.Plan.ShouldBe(SidecarValues.Plan.NotNeeded);
        AnswersOf(description).ShouldBe([NotNullCatalog]);
    }

    // users is the side the join keeps: its NOT NULL column stays so, by the catalog.
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseJoinKeepsTheColumnsSide_TakesTheCatalogsAnswerAndIsWalked()
    {
        var plan = Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };

        var description = await DescribeUsersIdAsync(GatedUsersId, PostgresScript.PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // The settings of the describer are in the request, so a capture recorded with them is replayed with them.
    [Fact]
    public async Task DescribeAsync_DescriberWithSettings_AsksThePlanWithThem()
    {
        var users = PostgresTables.Users;
        var plan = Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };
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
        _ = script.Answer(
            "explain",
            new PostgresExplainRequest("sqlsource_1", 0, ["enable_hashjoin=off", "enable_mergejoin=off"]),
            PostgresScript.PlanOf(plan)
        );
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(
            exchange,
            explainSettings: ["enable_hashjoin=off", "enable_mergejoin=off"]
        );

        var described = await session.DescribeAsync(Of(GatedUsersId), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        described.Value.ShouldNotBeNull().Plan.ShouldBe(SidecarValues.Plan.Walked);
    }

    // The gated query of users.id and a window's count, planned with users as the side the join null-extends.
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseJoinExtendsTheColumnsSide_IsNullableByTheOuterJoin()
    {
        var plan = Join("Right", Scan("u"), Scan("o")) with { Output = ["u.id", "count(*) OVER (?)"] };

        var description = await DescribeUsersIdAsync(GatedUsersId, PostgresScript.PlanOf(plan));

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NullableOuterJoin, UnknownNoOrigin]);
    }

    private static readonly Dictionary<string, (string? Plan, string? Error)> UnusablePlanRows = new(
        StringComparer.Ordinal
    )
    {
        // The server gave no plan: the error is logged and never reported.
        ["no-plan-and-an-error"] = (null, "canceling statement due to statement timeout"),
        // The text is no plan.
        ["text-that-is-no-json"] = ("not json", null),
        ["empty-list"] = ("[]", null),
        // An answer that holds neither.
        ["no-plan-and-no-error"] = (null, null),
        // A plan that is read and cannot be used.
        ["plan-the-walk-cannot-use"] = (
            Text(Join("Sideways", Scan("u"), Scan("o")) with { Output = ["u.id", "n"] }),
            null
        ),
    };

    public static TheoryData<string> PlansThatCannotBeUsed => [.. UnusablePlanRows.Keys];

    [Theory]
    [MemberData(nameof(PlansThatCannotBeUsed))]
    public async Task DescribeAsync_GatedQueryWithoutAPlanTheWalkCanUse_IsDescribedByTheHeuristicAndUnavailable(
        string name
    )
    {
        var (plan, error) = UnusablePlanRows[name];
        var answer = new PostgresExplainAnswer(
            plan,
            error is null ? null : new PostgresServerError("57014", error, null, null, null)
        );

        var description = await DescribeUsersIdAsync(GatedUsersId, answer);

        description.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        description.TableMatch.ShouldBe(SidecarValues.TableMatch.NoOrigin);
        AnswersOf(description).ShouldBe([NullableHeuristic, UnknownNoOrigin]);
    }

    // A check that fails ends the describe before the plan is asked for: the script holds no "explain".
    [Fact]
    public async Task DescribeAsync_GatedQueryWhoseCheckDiffers_IsTheFailureAndAsksNoPlan()
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Open();
        _ = script.Prepares(1, GatedUsersId, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(GatedUsersId, [], PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(GatedUsersId), TestContext.Current.CancellationToken);

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC231");
        exchange.Unused.ShouldBeEmpty();
    }

    // The plan is asked with a NULL for each parameter the server resolved, which for this query are those of its
    // list.
    [Fact]
    public async Task DescribeAsync_GatedQueryWithAParameter_AsksThePlanWithTheNumberOfItsParameters()
    {
        var sql = "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = @id";
        var users = PostgresTables.Users;
        var id = PostgresTables.UsersId;
        var script = new PostgresScript().Open();
        _ = script.Prepares(
            1,
            "SELECT u.id FROM users u LEFT JOIN orders o ON o.user_id = u.id WHERE u.id = $1",
            ["unknown"],
            new([23], null)
        );
        _ = script.Columns(1, 1, id);
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([23], PostgresRows.Int4);
        _ = script.Checks(sql, [("id", "integer")], PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid));
        _ = script.Explains(1, 1, PostgresScript.PlanOf(Join("Left", Scan("u"), Scan("o")) with { Output = ["u.id"] }));
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(Of(sql, Inferred("id")), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        AnswersOf(described.Value.ShouldNotBeNull()).ShouldBe([NotNullCatalog]);
    }

    // A statement without rows has no provenance and asks for no plan, gate word or not.
    [Fact]
    public async Task DescribeAsync_GatedStatementWithoutRows_HasNoPlan()
    {
        var script = new PostgresScript().Open();
        _ = script.PreparesWithNoColumns(1, "DELETE FROM users WHERE left(name, 1) = 'a'", [], new([], null));
        _ = script.Checks("DELETE FROM users WHERE left(name, 1) = 'a'", []);
        var exchange = script.Frees(1).Play();
        await using var session = await PostgresScript.SessionAsync(exchange);

        var described = await session.DescribeAsync(
            Of("DELETE FROM users WHERE left(name, 1) = 'a'"),
            TestContext.Current.CancellationToken
        );

        described.Value.ShouldNotBeNull().Plan.ShouldBeNull();
    }
}
