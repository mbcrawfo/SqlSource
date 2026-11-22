using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Testing;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresSessionCases;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// The session over a replay the test writes: "casts", asked only when the type of an entry of a scanned plan would
// change an answer.  A call the script does not hold is a CaptureMissException, so a describe that asks it needlessly
// fails.
public sealed class PostgresSessionCastsTests
{
    private const string Sql = "SELECT s.c FROM (SELECT o.note, count(*) AS c FROM o GROUP BY o.note) s";

    private static readonly PostgresTableRow Orders = PostgresTables.TableOf(
        16800,
        "o",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "note", PostgresRows.Text.Oid)
    );

    // A scan of a grouped Aggregate that prints the entries.
    private static PlanNode ScanOf(params string[] entries) =>
        Over("Subquery Scan", Aggregate("Hashed", true, Scan("o", "o.id", "o.note"), entries), "Subquery") with
        {
            Alias = "s",
            Output = ["s.c"],
        };

    private static async Task<(QueryDescription Description, RecordingLog Log)> DescribeAsync(
        PlanNode plan,
        Action<PostgresScript> afterRelations
    )
    {
        var script = new PostgresScript().Answer(
            "open",
            PostgresOpenRequest.Instance,
            PostgresScript.Opened with
            {
                Settings =
                [
                    new PostgresSetting("search_path", "\"$user\", public"),
                    new PostgresSetting("plan_cache_mode", "auto"),
                    new PostgresSetting("standard_conforming_strings", "on"),
                ],
            }
        );
        _ = script.Prepares(1, Sql, [], new([], null));
        _ = script.Columns(1, 0, PostgresTables.ExpressionOf(0, "c", PostgresRows.Int8.Oid));
        _ = script.Resolves([20], PostgresRows.Int8);
        _ = script.Checks(Sql, [], PostgresTables.ExpressionOf(0, "c", PostgresRows.Int8.Oid));
        _ = script.Explains(1, 0, PostgresScript.PlanOf(plan));
        _ = script.Relations([new PostgresRelationName("public", "o")], Orders);
        afterRelations(script);
        var exchange = script.Frees(1).Play();
        var log = new RecordingLog();
        await using var session = await PostgresScript.SessionAsync(exchange, log);

        var described = await session.DescribeAsync(Listed(Sql), TestContext.Current.CancellationToken);

        exchange.Unused.ShouldBeEmpty();
        return (described.Value.ShouldNotBeNull(), log);
    }

    [Fact]
    public async Task DescribeAsync_NoCastAcrossFamilies_RulesTheEntryOut()
    {
        var (description, log) = await DescribeAsync(
            ScanOf("o.note", "count(*)"),
            static script => script.Casts(new PostgresCastsAnswer(false, null))
        );

        AnswersOf(description).ShouldBe([NotNullExpression]);
        log.Events.Select(written => written.Name)
            .Where(name => name is "relations" or "casts" or "walk")
            .ShouldBe(["relations", "casts", "walk"]);
    }

    [Fact]
    public async Task DescribeAsync_CastAcrossFamilies_KeepsNoOrigin()
    {
        var (description, _) = await DescribeAsync(
            ScanOf("o.note", "count(*)"),
            static script => script.Casts(new PostgresCastsAnswer(true, null))
        );

        AnswersOf(description).ShouldBe([UnknownNoOrigin]);
    }

    // An error is logged and never reported: the entry is not ruled out.
    [Fact]
    public async Task DescribeAsync_CastsFails_KeepsNoOrigin()
    {
        var (description, log) = await DescribeAsync(
            ScanOf("o.note", "count(*)"),
            static script =>
                script.Casts(
                    new PostgresCastsAnswer(null, new PostgresServerError("XX000", "no catalog", null, null, null))
                )
        );

        description.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(description).ShouldBe([UnknownNoOrigin]);
        log.Events.Single(written => written.Name == "casts").Members.ShouldContain(member => member.Name == "error");
    }

    // Every entry proves the column without the type rule, so the server is not asked.
    [Fact]
    public async Task DescribeAsync_AnswerTheTypeRuleDoesNotChange_AsksNothing()
    {
        var (description, _) = await DescribeAsync(ScanOf("o.id", "count(*)"), static _ => { });

        AnswersOf(description).ShouldBe([NotNullExpression]);
    }
}
