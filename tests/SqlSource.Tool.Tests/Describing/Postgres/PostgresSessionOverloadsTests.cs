using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresSessionCases;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: "overloads", asked after "relations" for the names of CatalogOverloads
// the plan prints, and what the walk makes of its answer.  A call the script does not hold is a CaptureMissException,
// so a describe that asks more than its test wrote fails.
public sealed class PostgresSessionOverloadsTests
{
    private const string CountOfOrders =
        "SELECT u.id, count(o.id) AS n FROM users u LEFT JOIN orders o ON o.user_id = u.id GROUP BY u.id";

    // The relations of a join of "u" and "o", which "tables" did not answer and a plan that scans them asks for.
    private static readonly PostgresRelationName[] JoinedRelations =
    [
        new PostgresRelationName("public", "o"),
        new PostgresRelationName("public", "u"),
    ];

    private static readonly PostgresOverloadsRequest CountAsked = new(["count"], []);

    // A grouped Aggregate over a LEFT JOIN of "u" and "o" that prints the count the test gives.
    private static PlanNode GroupedCount(string count) =>
        Aggregate(
            "Hashed",
            true,
            Join("Left", Scan("u", "u.id"), Scan("o", "o.id")) with
            {
                Output = ["u.id", "o.id"],
            },
            "u.id",
            count
        ) with
        {
            Output = ["u.id", count],
        };

    // The describe of the query, over a server whose path is the one given, with the plan the test gives and each call
    // after "relations" the script writes.
    private static async Task<(QueryDescription Description, RecordingLog Log)> DescribeAsync(
        PlanNode plan,
        Action<PostgresScript> afterRelations,
        string searchPath = "\"$user\", public"
    )
    {
        var users = PostgresTables.Users;
        var script = new PostgresScript().Answer(
            "open",
            PostgresOpenRequest.Instance,
            PostgresScript.Opened with
            {
                Settings =
                [
                    new PostgresSetting("search_path", searchPath),
                    new PostgresSetting("plan_cache_mode", "auto"),
                    new PostgresSetting("standard_conforming_strings", "on"),
                ],
            }
        );
        _ = script.Prepares(1, CountOfOrders, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.UsersId, PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid));
        _ = script.Tables([users.Oid], users);
        _ = script.Resolves([20, 23], PostgresRows.Int8, PostgresRows.Int4);
        _ = script.Checks(
            CountOfOrders,
            [],
            PostgresTables.ExpressionOf(0, "id", PostgresRows.Int4.Oid),
            PostgresTables.ExpressionOf(1, "n", PostgresRows.Int8.Oid)
        );
        _ = script.Explains(1, 0, PostgresScript.PlanOf(plan));
        _ = script.Relations(JoinedRelations);
        afterRelations(script);
        var exchange = script.Frees(1).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(Listed(CountOfOrders), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return (described.Value.ShouldNotBeNull(), log);
    }

    // The plan prints count of an argument, which no schema but pg_catalog holds: the count is the catalog's, and is
    // never NULL.
    [Fact]
    public async Task DescribeAsync_CountOfAnArgument_AsksOverloadsAndIsAnswered()
    {
        var (description, log) = await DescribeAsync(
            GroupedCount("count(o.id)"),
            static script => script.Overloads(["count"], [])
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullExpression]);
        log.Events.Select(written => written.Name)
            .Where(name => name is "explain" or "relations" or "overloads" or "walk")
            .ShouldBe(["explain", "relations", "overloads", "walk"]);
    }

    // Another schema holds a count, which may be the one the server chose: the column stays unknown.
    [Fact]
    public async Task DescribeAsync_NameAnotherSchemaHolds_KeepsNoOrigin()
    {
        var (description, _) = await DescribeAsync(
            GroupedCount("count(o.id)"),
            static script => script.Overloads(CountAsked, new PostgresOverloadsAnswer(["count"], [], null))
        );

        AnswersOf(description).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // An "overloads" that fails is logged and never reported: every name is held elsewhere.
    [Fact]
    public async Task DescribeAsync_OverloadsFails_KeepsNoOrigin()
    {
        var (description, log) = await DescribeAsync(
            GroupedCount("count(o.id)"),
            static script =>
                script.Overloads(
                    CountAsked,
                    new PostgresOverloadsAnswer(
                        null,
                        null,
                        new PostgresServerError("XX000", "no catalog", null, null, null)
                    )
                )
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
        var (_, members) = log.Events.Single(written => written.Name == "overloads");
        members.ShouldContain(member => member.Name == "error");
    }

    // count(*) is the catalog's by its signature, and is read without the call.
    [Fact]
    public async Task DescribeAsync_PlanOfCountStar_AsksNoOverloads()
    {
        var (description, _) = await DescribeAsync(GroupedCount("count(*)"), static _ => { });

        AnswersOf(description).ShouldBe([NotNullCatalog, NotNullExpression]);
    }

    // A session that may find another schema's function before pg_catalog's reads no name of the table, so nothing
    // is asked.
    [Fact]
    public async Task DescribeAsync_CatalogNotFirst_AsksNoOverloads()
    {
        var (description, _) = await DescribeAsync(GroupedCount("count(o.id)"), static _ => { }, "shadow, pg_catalog");

        AnswersOf(description).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }
}
