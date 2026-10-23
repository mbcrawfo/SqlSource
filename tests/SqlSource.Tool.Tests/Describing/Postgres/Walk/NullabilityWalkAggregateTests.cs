using Shouldly;
using SqlSource.Tool.Describing.Postgres.Walk;
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

    // An Aggregate with no Group Key returns a row for an input of none, and a node that is no Aggregate computes no
    // aggregate.
    [Theory]
    [InlineData("Aggregate", "Plain", false)]
    [InlineData("Aggregate", "Sorted", false)]
    [InlineData("Group", "Sorted", true)]
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
}
