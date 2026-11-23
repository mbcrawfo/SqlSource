using System;
using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Describing.Postgres.Walk;
using SqlSource.Tool.Describing.Postgres.Walk.Expressions;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over the aggregates a node computes: a group that has a row, a node that returns a row for an input
// of none, and a value that comes from a child or from the side a join extends.
public sealed class NullabilityWalkAggregateTests
{
    // A join of "a" and "o" that prints what the aggregate above it reads.
    private static PlanNode JoinOfAAndO(string joinType) =>
        Join(joinType, Scan("a", "a.id"), Scan("o", "o.placed_at", "o.quantity", "o.note")) with
        {
            Output = ["a.id", "o.placed_at", "o.quantity", "o.note"],
        };

    // An Aggregate with a Group Key over the join of "a" and "o", which prints its aggregate bare, under a Sort that
    // prints it in parentheses or as the root.
    private static PlanNode GroupedOverAAndO(bool sorted, string aggregate)
    {
        var grouped = Aggregate("Hashed", true, JoinOfAAndO("Inner"), "a.id", aggregate);
        return sorted
            ? Over("Sort", grouped) with
            {
                Output = ["(" + aggregate + ")", "a.id"],
            }
            : grouped with
            {
                Output = [aggregate, "a.id"],
            };
    }

    // Each group of an Aggregate with a Group Key is made of the rows that share its key, so it has one at least, and
    // the node prints its aggregate bare where a node above prints it in parentheses.
    [Theory]
    [InlineData(true, "max(o.placed_at)")]
    [InlineData(false, "max(o.placed_at)")]
    [InlineData(true, "sum(o.quantity)")]
    public void Run_AggregateOfAGroupedNode_IsNotNull(bool sorted, string aggregate) =>
        AnswersOf(RunOverOrders(GroupedOverAAndO(sorted, aggregate))).ShouldBe([NotNullExpression]);

    // A group's every value may be NULL, array_agg is in no row of the table, and a FILTER may leave a group no row.
    [Theory]
    [InlineData("max(o.note)")]
    [InlineData("array_agg(o.quantity)")]
    [InlineData("max(o.placed_at) FILTER (WHERE (o.quantity > 1))")]
    public void Run_AggregateOfAGroupedNodeThatMayBeNull_KeepsNoOrigin(string aggregate) =>
        AnswersOf(RunOverOrders(GroupedOverAAndO(sorted: true, aggregate))).ShouldBe([UnknownNoOrigin]);

    // PostgreSQL 16 and later drop a key the WHERE fixes, and the planner writes Sorted only for a query with a
    // GROUP BY, whose Aggregate returns no row for an input of none.
    [Fact]
    public void Run_SortedAggregateWithNoKeyLeft_IsNotNull() =>
        AnswersOf(
                RunOverOrders(
                    Over("Sort", Aggregate("Sorted", false, JoinOfAAndO("Inner"), "max(o.placed_at)")) with
                    {
                        Output = ["(max(o.placed_at))"],
                    }
                )
            )
            .ShouldBe([NotNullExpression]);

    // A Plain Aggregate returns a row for an input of none, a Hashed one with no key was never seen, and a node that is
    // no Aggregate computes no aggregate.
    [Theory]
    [InlineData("Aggregate", "Plain", false)]
    [InlineData("Aggregate", "Hashed", false)]
    [InlineData("Group", "Sorted", true)]
    [InlineData("Group", "Sorted", false)]
    [InlineData("WindowAgg", null, true)]
    public void Run_AggregateOfAPlainNode_KeepsNoOrigin(string nodeType, string? strategy, bool groupKey)
    {
        var plain = Aggregate(strategy ?? "Plain", groupKey, JoinOfAAndO("Inner"), "max(o.placed_at)") with
        {
            NodeType = nodeType,
            Strategy = strategy,
        };

        AnswersOf(RunOverOrders(Over("Sort", plain) with { Output = ["(max(o.placed_at))"] }))
            .ShouldBe([UnknownNoOrigin]);
    }

    // A WindowAgg whose frame holds the current row computes the aggregate over a frame with a row.
    private static PlanNode WindowOverOrders(string aggregate, string? window) =>
        Over(
            "Sort",
            Over("WindowAgg", Scan("o", "o.placed_at", "o.note")) with
            {
                Output = [aggregate],
                Window = window,
            }
        ) with
        {
            Output = ["(" + aggregate + ")"],
        };

    [Theory]
    [InlineData("w1 AS (ORDER BY o.id)")]
    [InlineData("w1 AS (ORDER BY o.id ROWS UNBOUNDED PRECEDING)")]
    public void Run_WindowAggregateOverAFrameWithTheCurrentRow_IsNotNull(string window) =>
        AnswersOf(RunOverOrders(WindowOverOrders("max(o.placed_at) OVER w1", window))).ShouldBe([NotNullExpression]);

    // No frame known, a frame that may be empty, another window's name, a value that may be NULL, a FILTER, and an
    // aggregate outside the table.
    [Theory]
    [InlineData("max(o.placed_at) OVER (?)", null)]
    [InlineData("max(o.placed_at) OVER w1", null)]
    [InlineData("max(o.placed_at) OVER w1", "w1 AS (ROWS BETWEEN '1'::bigint FOLLOWING AND '2'::bigint FOLLOWING)")]
    [InlineData("max(o.placed_at) OVER w2", "w1 AS (ORDER BY o.id)")]
    [InlineData("max(o.note) OVER w1", "w1 AS (ORDER BY o.id)")]
    [InlineData("max(o.placed_at) FILTER (WHERE (o.id > 1)) OVER w1", "w1 AS (ORDER BY o.id)")]
    [InlineData("lag(o.placed_at) OVER w1", "w1 AS (ORDER BY o.id)")]
    public void Run_WindowAggregateThatMayBeNull_KeepsNoOrigin(string aggregate, string? window) =>
        AnswersOf(RunOverOrders(WindowOverOrders(aggregate, window))).ShouldBe([UnknownNoOrigin]);

    // A node prints a value of a child in parentheses, so a bare aggregate of the same text as its child's is its own:
    // a Plain Aggregate over a grouped one returns a row for no input.
    [Theory]
    [InlineData("max(o.placed_at)", "max(o.placed_at)")]
    [InlineData("(max(o.placed_at) + '1 day'::interval)", "(max(o.placed_at) + '1 day'::interval)")]
    public void Run_PlainAggregateOfTheSameTextAsItsGroupedChild_KeepsNoOrigin(string entry, string child) =>
        AnswersOf(
                RunOverOrders(Aggregate("Plain", false, Aggregate("Sorted", true, JoinOfAAndO("Inner"), child), entry))
            )
            .ShouldBe([UnknownNoOrigin]);

    // A value the Plain Aggregate takes from its grouped child, in parentheses, is the child's.
    [Fact]
    public void Run_ValueAnAggregateTakesFromAGroupedChild_IsTheChilds() =>
        AnswersOf(
                RunOverOrders(
                    Aggregate(
                        "Sorted",
                        true,
                        Aggregate("Sorted", true, JoinOfAAndO("Inner"), "max(o.placed_at)"),
                        "(max(o.placed_at))"
                    )
                )
            )
            .ShouldBe([NotNullExpression]);

    // A WindowAgg over another of a window of the same name: its own aggregate is read under its own frame.
    [Theory]
    [InlineData("w1 AS (ROWS BETWEEN '1'::bigint FOLLOWING AND '2'::bigint FOLLOWING)", "w1 AS ()", false)]
    [InlineData("w1 AS ()", "w1 AS (ROWS BETWEEN '1'::bigint FOLLOWING AND '2'::bigint FOLLOWING)", true)]
    public void Run_WindowAggregateOfTheSameTextAsItsChild_IsReadUnderItsOwnWindow(
        string upper,
        string lower,
        bool proved
    )
    {
        var child = Over("WindowAgg", Scan("o", "o.placed_at")) with
        {
            Output = ["max(o.placed_at) OVER w1"],
            Window = lower,
        };
        var root = Over("WindowAgg", child) with { Output = ["max(o.placed_at) OVER w1"], Window = upper };

        AnswersOf(RunOverOrders(root)).ShouldBe([proved ? NotNullExpression : UnknownNoOrigin]);
    }

    // The value of a child's window, inside an expression of the node above, is not read under the node's own window.
    [Fact]
    public void Run_ValueOfAChildsWindowOfTheSameName_IsNotReadUnderTheNodesWindow()
    {
        var child = Over("WindowAgg", Scan("o", "o.quantity")) with
        {
            Output = ["sum(o.quantity) OVER w1"],
            Window = "w1 AS (ROWS BETWEEN '1'::bigint FOLLOWING AND '2'::bigint FOLLOWING)",
        };
        var root = Over("WindowAgg", child) with
        {
            Output = ["(sum(o.quantity) OVER w1 + (sum(o.quantity) OVER w1))"],
            Window = "w1 AS ()",
        };

        AnswersOf(RunOverOrders(root)).ShouldBe([UnknownNoOrigin]);
    }

    // The same text at a node that is no WindowAgg says nothing of a frame.
    [Fact]
    public void Run_WindowTextAtANodeThatIsNoWindowAgg_KeepsNoOrigin() =>
        AnswersOf(RunOverOrders(Scan("o", "max(o.placed_at) OVER w1") with { Window = "w1 AS (ORDER BY o.id)" }))
            .ShouldBe([UnknownNoOrigin]);

    // A node with Grouping Sets may have a group of no row, the one of the empty set: the walk does not use its plan.
    [Fact]
    public void Run_AggregateOfANodeWithGroupingSets_KeepsNoOrigin() =>
        AnswersOf(
                RunOverOrders(
                    Aggregate("Mixed", true, JoinOfAAndO("Inner"), "max(o.placed_at)") with
                    {
                        HasGroupingSets = true,
                    }
                )
            )
            .ShouldBe([UnknownNoOrigin]);

    // A grouped Aggregate over a join of "a" and "o" that null-extends "o", under a Sort.
    private static PlanNode GroupedOverALeftJoin(string aggregate) =>
        Over("Sort", Aggregate("Sorted", true, JoinOfAAndO("Left"), "a.id", aggregate)) with
        {
            Output = ["(" + aggregate + ")", "a.id"],
        };

    // A value of the side a join null-extends may be NULL in every row of a group.
    [Fact]
    public void Run_AggregateOfAnExtendedSide_KeepsNoOrigin() =>
        AnswersOf(RunOverOrders(GroupedOverALeftJoin("max(o.placed_at)"))).ShouldBe([UnknownNoOrigin]);

    // A value of the side the join keeps is in every row of a group.
    [Fact]
    public void Run_AggregateOfThePreservedSideOfAnOuterJoin_IsNotNull() =>
        AnswersOf(RunOverOrders(GroupedOverALeftJoin("max(a.id)"))).ShouldBe([NotNullExpression]);

    // A value the node takes from a child was computed under the child's grouping, here a plain one that returns a row
    // of NULL for an input of none.
    [Fact]
    public void Run_AggregateTakenFromAChild_KeepsNoOrigin() =>
        AnswersOf(
                RunOverOrders(
                    Aggregate(
                        "Sorted",
                        true,
                        Aggregate("Plain", false, Scan("o", "o.quantity"), "sum(o.quantity)"),
                        "((sum(o.quantity)) * 2)"
                    )
                )
            )
            .ShouldBe([UnknownNoOrigin]);

    // The same entry the node computes itself, over the rows its child scans.
    [Fact]
    public void Run_AggregateTheNodeComputesOverAChild_IsAnswered() =>
        AnswersOf(RunOverOrders(Aggregate("Sorted", true, Scan("o", "o.quantity"), "(sum(o.quantity) * 2)")))
            .ShouldBe([NotNullExpression]);

    // The count of an argument over the side a LEFT JOIN extends, computed by the grouped Aggregate above it, is the
    // catalog's only when "overloads" answered that no other schema holds count.
    [Fact]
    public void Run_CountOfAnArgumentWithTheCatalogAlone_IsAnswered()
    {
        var plan = Aggregate(
            "Hashed",
            true,
            Join("Left", Scan("a", "a.id"), Scan("b", "b.id")) with
            {
                Output = ["a.id", "b.id"],
            },
            "a.id",
            "count(b.id)"
        ) with
        {
            Output = ["a.id", "count(b.id)"],
        };
        var alone = new CatalogNames(new HashSet<string>(["count"], StringComparer.Ordinal), new HashSet<string>());

        AnswersOf(RunAs(Scope(true) with { CatalogAlone = alone }, plan, OfA, Expression))
            .ShouldBe([NotNullCatalog, NotNullExpression]);
        AnswersOf(RunAs(Scope(true), plan, OfA, Expression)).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }
}
