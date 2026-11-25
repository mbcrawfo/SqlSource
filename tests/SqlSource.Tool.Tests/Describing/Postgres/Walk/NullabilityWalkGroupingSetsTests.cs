using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Statements;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Describing.Postgres.Walk.Expressions;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over a plan with grouping sets: only a count or a GROUPING(...) that the grouping Aggregate computes
// is read, since each is never NULL in any group, the empty one included.
public sealed class NullabilityWalkGroupingSetsTests
{
    private static readonly CatalogNames CountAlone = new(
        new HashSet<string>(["count"], StringComparer.Ordinal),
        new HashSet<string>()
    );

    private static PlanNode Grouped(params string[] output) =>
        Aggregate("Sorted", true, Scan("o", "o.user_id", "o.quantity"), output) with
        {
            HasGroupingSets = true,
        };

    private static WalkOutcome RunOf(
        PlanNode root,
        CatalogNames? alone,
        int columnCount,
        bool catalogFunctionsFirst = true
    )
    {
        var (columns, origins) = Built(true, [.. Enumerable.Repeat(Expression, columnCount)]);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(
                true,
                OldAndNewNames.None,
                catalogFunctionsFirst,
                PostgresTables.Of(TableA, OrdersO),
                [.. Enumerable.Repeat(BuiltInTypes.Int8, columnCount)],
                CatalogAlone: alone
            )
        );
    }

    [Theory]
    [InlineData("count(*)")]
    [InlineData("count(*) FILTER (WHERE (o.quantity > 1))")]
    [InlineData("count(DISTINCT o.user_id)")]
    [InlineData("count(o.quantity)")]
    [InlineData("GROUPING(o.user_id)")]
    [InlineData("GROUPING(o.user_id, date_trunc('month'::text, o.placed_at))")]
    public void Run_CountOrGroupingAtTheGroupingAggregate_IsAnswered(string entry)
    {
        var outcome = RunOf(Grouped(entry), CountAlone, 1);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        outcome.Reason.ShouldBe(WalkReasons.GroupingSets);
        AnswersOf(outcome).ShouldBe([NotNullExpression]);
    }

    // A Sort above prints each in parentheses, and a value it did not take from the Aggregate is no such value.
    [Fact]
    public void Run_CountPassedUpThroughASort_IsAnswered() =>
        AnswersOf(RunOf(Over("Sort", Grouped("count(*)")) with { Output = ["(count(*))"] }, CountAlone, 1))
            .ShouldBe([NotNullExpression]);

    // count(x) reads a name another schema may hold.
    [Fact]
    public void Run_CountOfAColumnNotHeldAlone_KeepsNoOrigin()
    {
        var outcome = RunOf(Grouped("count(o.quantity)"), null, 1);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        AnswersOf(outcome).ShouldBe([UnknownNoOrigin]);
    }

    // Where another schema comes before pg_catalog, count(*) may be that schema's.  GROUPING(...) is syntax, which no
    // search path changes.
    [Theory]
    [InlineData("count(*)", false)]
    [InlineData("count(o.quantity)", false)]
    [InlineData("GROUPING(o.user_id)", true)]
    public void Run_WhenPgCatalogIsNotFirst_ReadsOnlyGrouping(string entry, bool answered) =>
        AnswersOf(RunOf(Grouped(entry), CountAlone, 1, catalogFunctionsFirst: false))
            .ShouldBe([answered ? NotNullExpression : UnknownNoOrigin]);

    // A grouping column is NULL on a subtotal row, and any other value may be NULL in the empty group.
    [Theory]
    [InlineData("sum(o.quantity)")]
    [InlineData("max(o.user_id)")]
    [InlineData("(count(*) + 1)")]
    [InlineData("(GROUPING(o.user_id) + 1)")]
    [InlineData("COALESCE(o.user_id, 0)")]
    [InlineData("'x'::text")]
    [InlineData("\"GROUPING\"(o.user_id)")]
    [InlineData("grouping(o.user_id)")]
    [InlineData("rank() OVER (?)")]
    public void Run_AnyOtherValue_KeepsNoOrigin(string entry)
    {
        var outcome = RunOf(Grouped(entry), CountAlone, 1);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        outcome.Reason.ShouldBe(WalkReasons.GroupingSets);
        AnswersOf(outcome).ShouldBe([UnknownNoOrigin]);
    }

    // A node above that computes it, and a join above that null-extends the Aggregate.
    [Fact]
    public void Run_CountANodeAboveComputes_KeepsNoOrigin() =>
        AnswersOf(RunOf(Over("Result", Grouped("count(*)")) with { Output = ["(count(*) + 1)"] }, CountAlone, 1))
            .ShouldBe([UnknownNoOrigin]);

    [Fact]
    public void Run_CountOnTheSideAJoinExtends_KeepsNoOrigin() =>
        AnswersOf(
                RunOf(
                    Join("Left", Scan("a", "a.id"), Grouped("count(*)")) with
                    {
                        Output = ["(count(*))"],
                    },
                    CountAlone,
                    1
                )
            )
            .ShouldBe([UnknownNoOrigin]);

    // A reason beside the grouping sets leaves the plan unusable, whichever was met first.
    [Fact]
    public void Run_GroupingSetsBesideAnUnknownJoin_KeepsNoOrigin()
    {
        var unknown = Join("Sideways", Scan("a", "a.id"), Scan("b", "b.id")) with { Output = ["a.id"] };
        var root = Aggregate("Sorted", true, unknown, "count(*)") with { HasGroupingSets = true };

        var outcome = RunOf(root, CountAlone, 1);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Unavailable);
        AnswersOf(outcome).ShouldBe([UnknownNoOrigin]);
    }
}
