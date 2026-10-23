using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over plans whose columns are expressions: where a join that extends a side, or one that does not,
// changes the answer, the function arguments an expression reads, and the value the root computes over a join.
public sealed class NullabilityWalkExpressionTests
{
    [Fact]
    public void Run_ExpressionWithNoJoin_IsAnsweredFromItsEntry()
    {
        var plan = Over("Sort", Scan("t")) with
        {
            Output = ["(count(*))", "'a'::text", "NULL::text", "upper((t.opt)::text)"],
        };

        var outcome = RunUngated(plan, Expression, Expression, Expression, Expression);

        AnswersOf(outcome).ShouldBe([NotNullExpression, NotNullExpression, NullableExpression, UnknownNoOrigin]);
    }

    [Theory]
    [InlineData("Inner", true)]
    [InlineData("Inner", false)]
    [InlineData("Semi", true)]
    [InlineData("Right Semi", false)]
    public void Run_ExpressionUnderAJoinThatExtendsNothing_IsAnswered(string joinType, bool gated) =>
        AnswersOf(RunAs(Scope(gated), Join(joinType, Scan("a"), Scan("b")) with { Output = ["'k'::text"] }, Expression))
            .ShouldBe([NotNullExpression]);

    // A constant or a COALESCE of a subquery the planner pulled up from the side a LEFT JOIN extends prints at the root
    // as the same text, and is NULL for a row the join did not match.
    [Theory]
    [InlineData("Left", true)]
    [InlineData("Right", true)]
    [InlineData("Full", true)]
    [InlineData("Left", false)]
    public void Run_ExpressionUnderAJoinThatExtends_KeepsNoOrigin(string joinType, bool gated) =>
        AnswersOf(
                RunAs(Scope(gated), Join(joinType, Scan("a"), Scan("b")) with { Output = ["('x'::text)"] }, Expression)
            )
            .ShouldBe([UnknownNoOrigin]);

    // An anti join may be a LEFT JOIN ... IS NULL, which returns its other side NULL, and a view brings one into a
    // query without a word of the gate: the words do not say what the plan holds.
    [Theory]
    [InlineData("Anti", false)]
    [InlineData("Right Anti", false)]
    [InlineData("Anti", true)]
    [InlineData("Right Anti", true)]
    public void Run_ExpressionUnderAnAntiJoin_KeepsNoOrigin(string joinType, bool gated)
    {
        var outcome = RunAs(
            Scope(gated),
            Join(joinType, Scan("a"), Scan("b")) with
            {
                Output = ["('x'::text)"],
            },
            Expression
        );

        AnswersOf(outcome).ShouldBe([UnknownNoOrigin]);
    }

    [Fact]
    public void Run_FunctionWhenTheCatalogIsNotFirst_KeepsNoOrigin() =>
        AnswersOf(RunAs(Scope(false, catalogFunctionsFirst: false), Scan("t", "count(*)"), Expression))
            .ShouldBe([UnknownNoOrigin]);

    [Theory]
    [InlineData("GREATEST(a.id, a.opt)", false)]
    [InlineData("GREATEST(a.opt, b.opt)", null)]
    [InlineData("GREATEST(c.id, a.opt)", null)]
    public void Run_ReferenceArgumentUnderAnInnerJoin_IsTheCatalogsAnswerOfItsInstance(string entry, bool? nullable) =>
        AnswersOf(RunUngated(Join("Inner", Scan("a"), Scan("b")) with { Output = [entry] }, Expression))
            .ShouldBe([(nullable, nullable is null ? "no-origin" : "expression")]);

    // A plan of one relation prints its columns bare.
    [Fact]
    public void Run_BareReferenceArgumentOfThePlansOneRelation_IsTheCatalogsAnswer() =>
        AnswersOf(RunUngated(Over("Sort", Scan("a")) with { Output = ["(GREATEST(id, opt))"] }, Expression))
            .ShouldBe([NotNullExpression]);

    [Fact]
    public void Run_BareReferenceArgumentUnderAJoinOfTwo_KeepsNoOrigin() =>
        AnswersOf(RunUngated(Join("Inner", Scan("a"), Scan("b")) with { Output = ["GREATEST(id, opt)"] }, Expression))
            .ShouldBe([UnknownNoOrigin]);

    // A value the root computes over a join that extends is answered; one a node under the extended side computes,
    // printed in parentheses at the root and as itself there, is not.
    [Fact]
    public void Run_ExpressionTheRootComputesOverAJoinThatExtends_IsAnswered()
    {
        var plan = Join(
            "Right",
            Scan("b", "b.opt", "b.id", "'x'::text"),
            Over("Hash", Scan("a", "a.id"), Inner) with
            {
                Output = ["a.id"],
            }
        ) with
        {
            Output =
            [
                "('x'::text)",
                "'k'::text",
                "COALESCE(b.opt, 'z'::text)",
                "COALESCE(b.opt, a.id)",
                "COALESCE(b.opt, b.id)",
            ],
        };

        AnswersOf(RunUngated(plan, Expression, Expression, Expression, Expression, Expression))
            .ShouldBe([UnknownNoOrigin, NotNullExpression, NotNullExpression, NotNullExpression, UnknownNoOrigin]);
    }

    // The value is followed through every node that prints it, to the node that computes it.
    [Fact]
    public void Run_ExpressionPrintedThroughSeveralNodes_IsFollowedToWhereItIsComputed()
    {
        var join = Join(
            "Right",
            Scan("b", "b.id", "'x'::text"),
            Over("Hash", Scan("a", "a.id"), Inner) with
            {
                Output = ["a.id"],
            }
        ) with
        {
            Output = ["('x'::text)", "'k'::text"],
        };
        var plan = Over("Limit", Over("Sort", join) with { Output = ["('x'::text)", "'k'::text"] }) with
        {
            Output = ["('x'::text)", "'k'::text"],
        };

        AnswersOf(RunUngated(plan, Expression, Expression)).ShouldBe([UnknownNoOrigin, NotNullExpression]);
    }

    // A LEFT JOIN LATERAL count whose Subquery Scan the planner removed: the Aggregate on the extended side computes
    // it, and the root prints it in parentheses.
    [Fact]
    public void Run_CountComputedOnTheExtendedSide_KeepsNoOrigin()
    {
        var plan = Join(
            "Left",
            Scan("a", "a.id"),
            Over("Aggregate", Scan("b", "b.id")) with
            {
                Output = ["count(*)"],
            }
        ) with
        {
            Output = ["a.id", "(count(*))"],
        };

        AnswersOf(RunUngated(plan, OfA, Expression)).ShouldBe([NotNullCatalog, UnknownNoOrigin]);
    }

    // A table of reservations whose account_id is NOT NULL, for the scan "r".
    private static readonly PostgresTableRow ReservationsR = PostgresTables.TableOf(
        16711,
        "r",
        PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid, notNull: true),
        PostgresTables.Attribute(2, "account_id", PostgresRows.Int4.Oid, notNull: true)
    );

    private static WalkOutcome RunOverAAndR(PlanNode root, bool catalogFunctionsFirst = true)
    {
        var (columns, origins) = Built(false, Expression);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(false, false, catalogFunctionsFirst, PostgresTables.Of(TableA, TableB, ReservationsR), [])
        );
    }

    private const string CoalesceOfBothSides = "COALESCE(a.id, r.account_id)";

    // A full join of "a" and "r" that computes the COALESCE of a column of each side, with "a" its outer side.
    private static PlanNode FullJoinOfAAndR(PlanNode outer) =>
        Join(
            "Full",
            outer,
            Over("Hash", Scan("r", "r.id", "r.account_id"), Inner) with
            {
                Output = ["r.account_id"],
            }
        ) with
        {
            Output = [CoalesceOfBothSides],
        };

    // A row of a full join has a row of one side at least, so a COALESCE the join computes over a column of each side
    // that only the join extends is not NULL.
    [Fact]
    public void Run_CoalesceAtAFullJoin_IsAnswered()
    {
        var plan = Over("Sort", FullJoinOfAAndR(Scan("a", "a.id"))) with { Output = ["(" + CoalesceOfBothSides + ")"] };

        AnswersOf(RunOverAAndR(plan)).ShouldBe([NotNullExpression]);
        AnswersOf(RunOverAAndR(plan, catalogFunctionsFirst: false)).ShouldBe([UnknownNoOrigin]);
    }

    // The full join on the side a LEFT JOIN extends returns a row of NULL for a row it did not match.
    [Fact]
    public void Run_CoalesceAtAFullJoinUnderALeftJoin_KeepsNoOrigin()
    {
        var plan = Join("Left", Scan("b", "b.id"), FullJoinOfAAndR(Scan("a", "a.id"))) with
        {
            Output = ["(" + CoalesceOfBothSides + ")"],
        };

        AnswersOf(RunOverAAndR(plan)).ShouldBe([UnknownNoOrigin]);
    }

    // A join that null-extends under a side may make that side's column NULL, whichever side it extends.
    [Fact]
    public void Run_CoalesceAtAFullJoinOverAnExtendedSide_KeepsNoOrigin()
    {
        var left = Join("Left", Scan("a", "a.id"), Scan("b", "b.id")) with { Output = ["a.id"] };

        AnswersOf(RunOverAAndR(FullJoinOfAAndR(left))).ShouldBe([UnknownNoOrigin]);
    }

    // A node above the join that computes the COALESCE reads each column as the join extends it.
    [Fact]
    public void Run_CoalesceAboveAFullJoin_KeepsNoOrigin()
    {
        var join = FullJoinOfAAndR(Scan("a", "a.id")) with { Output = ["a.id", "r.account_id"] };

        AnswersOf(RunOverAAndR(Over("Result", join) with { Output = [CoalesceOfBothSides] }))
            .ShouldBe([UnknownNoOrigin]);
    }

    // A node that prints no Output may compute any value, so it must be preserved.
    [Fact]
    public void Run_ExpressionBesideAChildWithoutOutputThatIsExtended_KeepsNoOrigin() =>
        AnswersOf(RunUngated(Join("Left", Scan("a", "a.id"), Scan("b")) with { Output = ["'k'::text"] }, Expression))
            .ShouldBe([UnknownNoOrigin]);

    // A constant the root computes over an anti join is never NULL, whatever the anti join returns.
    [Fact]
    public void Run_ExpressionTheRootComputesOverAnAntiJoin_IsAnswered() =>
        AnswersOf(
                RunAs(
                    Scope(true),
                    Join("Anti", Scan("a", "a.id"), Scan("b", "b.id")) with
                    {
                        Output = ["'k'::text", "(b.id IS NULL)"],
                    },
                    Expression,
                    Expression
                )
            )
            .ShouldBe([NotNullExpression, NotNullExpression]);

    // The root computes the CASE, but takes ('x'::text) from the anti join's inner side, where a pulled-up constant
    // is computed and which the anti join returns NULL: every value an expression takes from below is followed too.
    [Fact]
    public void Run_ExpressionTheRootComputesFromAValueOfTheExtendedSide_KeepsNoOrigin()
    {
        var hash = Over("Hash", Scan("b", "b.user_id", "'x'::text"), Inner) with
        {
            Output = ["b.user_id", "('x'::text)"],
        };
        var plan = Join("Anti", Scan("a", "a.id"), hash) with
        {
            Output = ["CASE WHEN (a.id > 0) THEN ('x'::text) ELSE 'y'::text END", "COALESCE(('x'::text))", "'k'::text"],
        };

        AnswersOf(RunUngated(plan, Expression, Expression, Expression))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin, NotNullExpression]);
    }

    // The join computes the COALESCE from a sum of the side it extends, which it prints in parentheses: the sum may be
    // NULL, and the constant still proves the COALESCE; a CASE whose one result is the sum proves nothing.
    [Fact]
    public void Run_ExpressionWithAConstantBesideAValueOfTheExtendedSide_IsAnsweredByTheConstant()
    {
        var aggregate = Over("HashAggregate", Scan("b", "b.id", "b.opt")) with { Output = ["b.id", "sum(b.opt)"] };
        var plan = Join("Right", aggregate, Over("Hash", Scan("a", "a.id"), Inner) with { Output = ["a.id"] }) with
        {
            Output =
            [
                "COALESCE((sum(b.opt)), '0'::numeric)",
                "CASE WHEN (a.id > 0) THEN (sum(b.opt)) ELSE '0'::numeric END",
            ],
        };

        AnswersOf(RunUngated(plan, Expression, Expression)).ShouldBe([NotNullExpression, UnknownNoOrigin]);
    }
}
