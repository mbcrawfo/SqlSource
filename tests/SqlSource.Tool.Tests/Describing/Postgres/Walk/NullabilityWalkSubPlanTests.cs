using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over the subplans an expression prints: a scalar subplan, ANY and EXISTS, and a subplan that cannot
// be read.
public sealed class NullabilityWalkSubPlanTests
{
    // SELECT a.id, (SELECT count(*) FROM o WHERE ...) FROM a: a Plain Aggregate returns one row for any input.
    private static PlanNode CountOfO() => Aggregate("Plain", false, Scan("o", "o.user_id"), "count(*)");

    // A scan of "a" that evaluates the subplan in its Output.
    private static PlanNode ScanOfA(string entry, params PlanNode[] subPlans) =>
        Scan("a", "a.id", entry) with
        {
            Plans = subPlans,
        };

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_ScalarSubPlanOfAPlainCount_IsAnswered(bool sorted)
    {
        var scan = ScanOfA("(SubPlan 1)", SubPlan("SubPlan 1", CountOfO()));
        var root = sorted ? Over("Sort", scan) with { Output = ["a.id", "((SubPlan 1))"] } : scan;

        AnswersOf(RunTyped(root, BuiltInTypes.Int4, BuiltInTypes.Int8)).ShouldBe([UnknownNoOrigin, NotNullExpression]);
    }

    // On 14 a.id IN (SELECT count(*) ...) prints (SubPlan 1) too: its column is a boolean, which is no subquery's
    // value of a family.
    [Fact]
    public void Run_ScalarSubPlanOfABooleanColumn_KeepsNoOrigin() =>
        AnswersOf(
                RunTyped(ScanOfA("(SubPlan 1)", SubPlan("SubPlan 1", CountOfO())), BuiltInTypes.Int4, BuiltInTypes.Bool)
            )
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // A subquery's value counts only as the whole column, where its type is the column's.
    [Fact]
    public void Run_ScalarSubPlanInsideAnExpression_KeepsNoOrigin() =>
        AnswersOf(
                RunTyped(
                    ScanOfA("((SubPlan 1) + 1)", SubPlan("SubPlan 1", CountOfO())),
                    BuiltInTypes.Int4,
                    BuiltInTypes.Int8
                )
            )
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // A subplan that may return no row, or a row whose value may be NULL, or one whose name is no single subplan.
    [Theory]
    [InlineData("group-key")]
    [InlineData("sorted-without-a-key")]
    [InlineData("grouping-sets")]
    [InlineData("filter")]
    [InlineData("limit")]
    [InlineData("max")]
    [InlineData("two-outputs")]
    [InlineData("found-twice")]
    [InlineData("other-name")]
    [InlineData("init-plan")]
    public void Run_ScalarSubPlanThatMayReturnNoRow_KeepsNoOrigin(string shape)
    {
        var count = CountOfO();
        PlanNode[] subPlans = shape switch
        {
            "group-key" => [SubPlan("SubPlan 1", count with { Strategy = "Sorted", HasGroupKey = true })],
            "sorted-without-a-key" => [SubPlan("SubPlan 1", count with { Strategy = "Sorted" })],
            "grouping-sets" => [SubPlan("SubPlan 1", count with { HasGroupingSets = true })],
            "filter" => [SubPlan("SubPlan 1", count with { HasFilter = true })],
            "limit" => [SubPlan("SubPlan 1", Over("Limit", count) with { Output = ["(count(*))"] })],
            "max" => [SubPlan("SubPlan 1", count with { Output = ["max(o.placed_at)"] })],
            "two-outputs" => [SubPlan("SubPlan 1", count with { Output = ["count(*)", "count(*)"] })],
            "found-twice" => [SubPlan("SubPlan 1", count), SubPlan("SubPlan 1", count)],
            "other-name" => [SubPlan("SubPlan 2", count)],
            _ => [count with { ParentRelationship = "InitPlan", SubplanName = "SubPlan 1" }],
        };
        var type = shape == "max" ? BuiltInTypes.Timestamptz : BuiltInTypes.Int8;

        AnswersOf(RunTyped(ScanOfA("(SubPlan 1)", subPlans), BuiltInTypes.Int4, type))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // A subquery evaluated on the side a join null-extends is NULL for a row the join did not match, and one evaluated
    // on the side it keeps is answered.
    [Theory]
    [InlineData(true, null)]
    [InlineData(false, false)]
    public void Run_ScalarSubPlanOnAJoinSide_IsAnsweredOnlyWhereKept(bool extended, bool? nullable)
    {
        var evaluates = Scan("b", "b.id", "(SubPlan 1)") with { Plans = [SubPlan("SubPlan 1", CountOfO())] };
        var join = extended ? Join("Left", Scan("a", "a.id"), evaluates) : Join("Left", evaluates, Scan("a", "a.id"));

        AnswersOf(RunTyped(join with { Output = ["a.id", "((SubPlan 1))"] }, BuiltInTypes.Int4, BuiltInTypes.Int8))
            .ShouldBe([UnknownNoOrigin, (nullable, nullable is null ? "no-origin" : "expression")]);
    }

    // a.id IN (SELECT o.user_id FROM o), as 18 prints it, over a subplan that scans "o" for the column and o.user_id.
    private static WalkOutcome RunAny(string entry, string column) =>
        RunTyped(
            ScanOfA(entry, SubPlan("SubPlan 2", Scan("o", column, "o.user_id"))),
            BuiltInTypes.Int4,
            BuiltInTypes.Bool
        );

    // An ANY is NULL only for a NULL on either side.
    [Theory]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", "o.user_id")]
    [InlineData("(ANY (a.id = (SubPlan 2).col1))", "o.user_id")]
    [InlineData("(ANY (a.id = (SubPlan 2).col2))", "o.note")]
    public void Run_AnyOverAProvedSubPlanColumn_IsNotNull(string entry, string column) =>
        AnswersOf(RunAny(entry, column)).ShouldBe([UnknownNoOrigin, NotNullExpression]);

    // A column of the subplan that may be NULL, a left side that is not proved, and a subplan or a column that is not
    // there.
    [Theory]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", "o.note")]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col1))", "o.parent_id")]
    [InlineData("(ANY (a.opt = (hashed SubPlan 2).col1))", "o.user_id")]
    [InlineData("(ANY (a.id = (hashed SubPlan 3).col1))", "o.user_id")]
    [InlineData("(ANY (a.id = (hashed SubPlan 2).col3))", "o.user_id")]
    public void Run_AnyOverASubPlanColumnNotProved_KeepsNoOrigin(string entry, string column) =>
        AnswersOf(RunAny(entry, column)).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // A column of the subplan is proved as its own plan proves it: here the subplan's join null-extends "o".
    [Fact]
    public void Run_AnyOverASubPlanColumnThatAJoinExtends_KeepsNoOrigin()
    {
        var join = Join("Left", Scan("b", "b.id"), Scan("o", "o.user_id")) with { Output = ["o.user_id"] };

        AnswersOf(
                RunTyped(
                    ScanOfA("(ANY (a.id = (hashed SubPlan 2).col1))", SubPlan("SubPlan 2", join)),
                    BuiltInTypes.Int4,
                    BuiltInTypes.Bool
                )
            )
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // A subplan that names itself is followed no deeper than the walk follows plans.
    [Fact]
    public void Run_SubPlanThatNamesItself_KeepsNoOrigin()
    {
        var entry = "(ANY (a.id = (SubPlan 2).col1))";
        var subPlan = SubPlan("SubPlan 2", Scan("o", "(ANY (o.user_id = (SubPlan 2).col1))"));

        AnswersOf(RunTyped(ScanOfA(entry, subPlan), BuiltInTypes.Int4, BuiltInTypes.Bool))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // In a statement that names OLD or NEW, the old or the new row may print under an alias a relation of the subplan
    // has, and no subplan is read.
    [Fact]
    public void Run_SubPlanWhenOldOrNewMayBeNamed_KeepsNoOrigin()
    {
        var (columns, origins) = Built(false, Expression, Expression);
        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(ScanOfA("(SubPlan 1)", SubPlan("SubPlan 1", CountOfO())), null),
            columns,
            origins,
            new WalkScope(false, true, true, PostgresTables.Of(TableA, OrdersO), [BuiltInTypes.Int4, BuiltInTypes.Int8])
        );

        AnswersOf(outcome).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
    }

    // EXISTS on 18 starts false and turns true at the subplan's first row.
    [Fact]
    public void Run_ExistsOfASubPlan_IsAnswered() =>
        AnswersOf(
                RunTyped(
                    ScanOfA("EXISTS(SubPlan 1)", SubPlan("SubPlan 1", Scan("o", "o.note"))),
                    BuiltInTypes.Int4,
                    BuiltInTypes.Bool
                )
            )
            .ShouldBe([UnknownNoOrigin, NotNullExpression]);
}
