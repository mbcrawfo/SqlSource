using System;
using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over the columns of a subquery scan and of a CTE scan, which are found by their origins.
public sealed class NullabilityWalkSubqueryTests
{
    // A Subquery Scan aliased "s" over the child plan, with its own Output.
    private static PlanNode Subquery(PlanNode child, params string[] output) =>
        Over("Subquery Scan", child, "Subquery") with
        {
            Alias = "s",
            Output = output,
        };

    // The row-number dedupe: the subquery keeps a LEFT JOIN, which the planner swapped into a Right join with b, the
    // side it extends, as its outer child.  The scan prints the columns in its own order, and they are found by their
    // origins.
    [Fact]
    public void Run_ColumnThroughASubqueryScan_IsAnsweredByItsOrigin()
    {
        var child = Over(
            "WindowAgg",
            Join("Right", Scan("b"), Over("Hash", Scan("t"), Inner)) with
            {
                Output = ["t.id", "t.opt", "b.id"],
            }
        ) with
        {
            Output = ["t.id", "t.opt", "b.id", "row_number() OVER w1"],
        };
        var plan = Over("Sort", Subquery(child, "s.y", "s.x", "s.z")) with { Output = ["s.x", "s.y", "s.z"] };

        var outcome = Run(plan, NotNull, Optional, OfB);

        AnswersOf(outcome).ShouldBe([NotNullCatalog, NullableCatalog, NullableOuterJoin]);
        outcome.Columns.Select(static walked => walked.Mapped).ShouldBe([true, true, true]);
    }

    // SELECT s.id, s.opt FROM (SELECT t.opt AS id, t.id AS opt FROM t) s: matched by name, the columns would be read
    // crossed; by their origins they are not.
    [Fact]
    public void Run_SubqueryColumnsRenamedToEachOthersNames_AreNotReadCrossed()
    {
        var plan = Subquery(Scan("t", "t.opt", "t.id"), "s.id", "s.opt") with { Output = ["s.id", "s.opt"] };

        AnswersOf(Run(plan, Optional, NotNull)).ShouldBe([NullableCatalog, NotNullCatalog]);
    }

    // a.id and a2.id are both a column of a: which one the subquery's column is cannot be told, and a2 is extended.
    [Fact]
    public void Run_SubqueryColumnThatTwoInstancesOfItsTableMayBe_KeepsTheHeuristic()
    {
        var child = Join("Left", Scan("a"), Scan("a2") with { RelationName = "a" }) with { Output = ["a.id", "a2.id"] };

        AnswersOf(Run(Subquery(child, "s.x") with { Output = ["s.x"] }, OfA)).ShouldBe([NullableHeuristic]);
    }

    // When every entry the column can be is of an extended instance, the column is the join's.
    [Fact]
    public void Run_SubqueryColumnOfAnExtendedInstance_IsTheOuterJoins()
    {
        var child = Join("Left", Scan("t"), Scan("b")) with { Output = ["t.id", "b.id", "b.opt"] };

        AnswersOf(Run(Subquery(child, "s.x") with { Output = ["s.x"] }, OfB)).ShouldBe([NullableOuterJoin]);
    }

    // A scan that a join above it extends extends every column of it.
    [Fact]
    public void Run_SubqueryScanThatAJoinExtends_IsTheOuterJoins()
    {
        var plan = Join("Left", Scan("a"), Subquery(Scan("t", "t.id"), "s.id")) with { Output = ["a.id", "s.id"] };

        AnswersOf(Run(plan, OfA, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);
    }

    private static readonly Dictionary<string, string> UnresolvableChildrenRows = new(StringComparer.Ordinal)
    {
        // A LATERAL subquery's Output may name the query above, which is no instance of its plan.
        { "lateral-names-the-query-above", Text(Scan("t", "t.id", "u.id")) },
        // A bare name says nothing of which instance.
        { "bare-name", Text(Join("Inner", Scan("t"), Scan("b")) with { Output = ["t.id", "id"] }) },
        // A set operation prints its first branch.
        { "set-operation", Text(Over("Sort", Over("Append", Scan("t"), "Member")) with { Output = ["t.id"] }) },
        // No entry is of the origin's table.
        { "no-entry-of-the-origins-table", Text(Scan("b", "b.id")) },
        // No Output at all.
        { "no-output", Text(Scan("t")) },
    };

    public static TheoryData<string> UnresolvableChildren => [.. UnresolvableChildrenRows.Keys];

    [Theory]
    [MemberData(nameof(UnresolvableChildren))]
    public void Run_SubqueryColumnThatCannotBeResolved_KeepsTheHeuristic(string name) =>
        AnswersOf(
                Run(
                    Subquery(PlanNode.Read(UnresolvableChildrenRows[name]).ShouldNotBeNull(), "s.x") with
                    {
                        Output = ["s.x"],
                    },
                    NotNull
                )
            )
            .ShouldBe([NullableHeuristic]);

    // A subquery inside a subquery is resolved in turn.
    [Fact]
    public void Run_ColumnThroughNestedSubqueryScans_IsAnsweredByItsOrigin()
    {
        var inner = Over(
            "Subquery Scan",
            Join("Left", Scan("t"), Scan("b")) with
            {
                Output = ["t.id", "b.id"],
            },
            "Subquery"
        ) with
        {
            Alias = "s2",
            Output = ["s2.x", "s2.y"],
        };
        var plan = Subquery(Over("Limit", inner) with { Output = ["s2.x", "s2.y"] }, "s.x", "s.y") with
        {
            Output = ["s.x", "s.y"],
        };

        AnswersOf(Run(plan, NotNull, OfB)).ShouldBe([NotNullCatalog, NullableOuterJoin]);
    }

    // A CTE used twice: one plan, two scans, each with its own path.
    [Fact]
    public void Run_ColumnsOfACteScannedTwice_AreAnsweredForEachScan()
    {
        var cte = Scan("t", "t.id", "t.opt") with { ParentRelationship = "InitPlan", SubplanName = "CTE recent" };
        var join = Join(
            "Left",
            Scan("r") with
            {
                NodeType = "CTE Scan",
                CteName = "recent",
                RelationName = null,
            },
            Scan("n") with
            {
                NodeType = "CTE Scan",
                CteName = "recent",
                RelationName = null,
            }
        );
        var plan = Over("Sort", join) with
        {
            Plans = [cte, .. Over("Sort", join).Plans],
            Output = ["r.id", "r.opt", "n.id"],
        };

        AnswersOf(Run(plan, NotNull, Optional, NotNull)).ShouldBe([NotNullCatalog, NullableCatalog, NullableOuterJoin]);
    }

    // A LATERAL reference to the query above, or a placeholder over a column, prints as the column in parentheses: the
    // subquery's column may be that entry, which is no reference, and another entry of its table and column decides
    // nothing.
    [Theory]
    [InlineData("(t.id)")]
    [InlineData("((t.id))")]
    [InlineData("upper((t.id)::text)")]
    [InlineData("$1")]
    public void Run_SubqueryColumnThatAnExpressionMayPrint_KeepsTheHeuristic(string entry) =>
        AnswersOf(Run(Subquery(Scan("t", entry, "t.id"), "s.x") with { Output = ["s.x"] }, NotNull))
            .ShouldBe([NullableHeuristic]);

    // An expression that does not name the column is never it.
    [Fact]
    public void Run_SubqueryColumnBesideAnExpressionOfAnotherName_IsAnswered() =>
        AnswersOf(
                Run(
                    Subquery(Scan("t", "row_number() OVER w1", "(t.opt)", "t.id"), "s.x") with
                    {
                        Output = ["s.x"],
                    },
                    NotNull
                )
            )
            .ShouldBe([NotNullCatalog]);
}
