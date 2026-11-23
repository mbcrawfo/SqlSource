using System.Linq;
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

    // A column of no origin for each type of the row description, over "a" and "o".
    private static WalkOutcome RunTyped(PlanNode root, params long[] types)
    {
        var (columns, origins) = Built(false, [.. types.Select(static _ => Expression)]);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(false, false, true, PostgresTables.Of(TableA, OrdersO), types)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Run_ScalarSubPlanOfAPlainCount_IsAnswered(bool sorted)
    {
        var scan = ScanOfA("(SubPlan 1)", SubPlan("SubPlan 1", CountOfO()));
        var root = sorted ? Over("Sort", scan) with { Output = ["a.id", "((SubPlan 1))"] } : scan;

        AnswersOf(RunTyped(root, BuiltInTypes.Int4, BuiltInTypes.Int8)).ShouldBe([UnknownNoOrigin, NotNullExpression]);
    }

    // Before 17 a.id IN (SELECT count(*) ...) prints (SubPlan 1) too: its column is a boolean, which is no subquery's
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
    private static void ShouldKeepNoOrigin(long type, params PlanNode[] subPlans) =>
        AnswersOf(RunTyped(ScanOfA("(SubPlan 1)", subPlans), BuiltInTypes.Int4, type))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    [Fact]
    public void Run_ScalarSubPlanSortedByAGroupKey_KeepsNoOrigin() =>
        ShouldKeepNoOrigin(
            BuiltInTypes.Int8,
            SubPlan("SubPlan 1", CountOfO() with { Strategy = "Sorted", HasGroupKey = true })
        );

    [Fact]
    public void Run_ScalarSubPlanSortedWithoutAKey_KeepsNoOrigin() =>
        ShouldKeepNoOrigin(BuiltInTypes.Int8, SubPlan("SubPlan 1", CountOfO() with { Strategy = "Sorted" }));

    [Fact]
    public void Run_ScalarSubPlanWithGroupingSets_KeepsNoOrigin() =>
        ShouldKeepNoOrigin(BuiltInTypes.Int8, SubPlan("SubPlan 1", CountOfO() with { HasGroupingSets = true }));

    [Fact]
    public void Run_ScalarSubPlanWithAFilter_KeepsNoOrigin() =>
        ShouldKeepNoOrigin(BuiltInTypes.Int8, SubPlan("SubPlan 1", CountOfO() with { HasFilter = true }));

    [Fact]
    public void Run_ScalarSubPlanUnderALimit_KeepsNoOrigin() =>
        ShouldKeepNoOrigin(
            BuiltInTypes.Int8,
            SubPlan("SubPlan 1", Over("Limit", CountOfO()) with { Output = ["(count(*))"] })
        );

    [Fact]
    public void Run_ScalarSubPlanOfMax_KeepsNoOrigin() =>
        ShouldKeepNoOrigin(
            BuiltInTypes.Timestamptz,
            SubPlan("SubPlan 1", CountOfO() with { Output = ["max(o.placed_at)"] })
        );

    [Fact]
    public void Run_ScalarSubPlanWithTwoOutputs_KeepsNoOrigin() =>
        ShouldKeepNoOrigin(
            BuiltInTypes.Int8,
            SubPlan("SubPlan 1", CountOfO() with { Output = ["count(*)", "count(*)"] })
        );

    [Fact]
    public void Run_ScalarSubPlanFoundTwice_KeepsNoOrigin()
    {
        var count = CountOfO();

        ShouldKeepNoOrigin(BuiltInTypes.Int8, SubPlan("SubPlan 1", count), SubPlan("SubPlan 1", count));
    }

    [Fact]
    public void Run_ScalarSubPlanOfAnotherName_KeepsNoOrigin() =>
        ShouldKeepNoOrigin(BuiltInTypes.Int8, SubPlan("SubPlan 2", CountOfO()));

    [Fact]
    public void Run_ScalarSubPlanThatIsAnInitPlan_KeepsNoOrigin() =>
        ShouldKeepNoOrigin(
            BuiltInTypes.Int8,
            CountOfO() with
            {
                ParentRelationship = "InitPlan",
                SubplanName = "SubPlan 1",
            }
        );

    // A Plain count of "o" as an InitPlan of the name.
    private static PlanNode InitPlan(string name) =>
        CountOfO() with
        {
            ParentRelationship = "InitPlan",
            SubplanName = name,
        };

    // A scan of "a" whose second entry is the value, over a statement of the given number of parameters.
    private static WalkOutcome RunInitPlan(string value, long type, int parameters, params PlanNode[] initPlans)
    {
        var (columns, origins) = Built(false, Expression, Expression);
        return NullabilityWalk.Run(
            new PlanInstancesCache(ScanOfA(value, initPlans), null),
            columns,
            origins,
            new WalkScope(
                false,
                false,
                true,
                PostgresTables.Of(TableA, OrdersO),
                [BuiltInTypes.Int4, type],
                ParameterCount: parameters
            )
        );
    }

    // An uncorrelated subquery prints $k before 17, under an InitPlan named for what it returns, and its own name from
    // 17.  Either may be wrapped by a node above.
    [Theory]
    [InlineData("(InitPlan 1).col1", "InitPlan 1", 0)]
    [InlineData("(InitPlan 1).col1", "InitPlan 1", 3)]
    [InlineData("((InitPlan 1).col1)", "InitPlan 1", 0)]
    [InlineData("$0", "InitPlan 1 (returns $0)", 0)]
    [InlineData("$0", "InitPlan 1 (returns $0)", 2)]
    [InlineData("($0)", "InitPlan 1 (returns $0)", 0)]
    [InlineData("$3", "InitPlan 2 (returns $3)", 2)]
    public void Run_InitPlanOfAPlainCount_IsAnswered(string value, string name, int parameters) =>
        AnswersOf(RunInitPlan(value, BuiltInTypes.Int8, parameters, InitPlan(name)))
            .ShouldBe([UnknownNoOrigin, NotNullExpression]);

    // $k up to the statement's parameters may be the parameter, which prints the same.  Another column than the first,
    // a plan of another name or of several values, and an expression over the value say nothing.
    [Theory]
    [InlineData("$1", "InitPlan 2 (returns $1)", 1)]
    [InlineData("$2", "InitPlan 2 (returns $2)", 3)]
    [InlineData("$1", "InitPlan 2 (returns $0,$1)", 0)]
    [InlineData("$1", "InitPlan 2 (returns $11)", 0)]
    [InlineData("(InitPlan 1).col2", "InitPlan 1", 0)]
    [InlineData("(InitPlan 2).col1", "InitPlan 1", 0)]
    [InlineData("(InitPlan 1).col1", "InitPlan 1 (returns $0)", 0)]
    [InlineData("((InitPlan 1).col1 + 1)", "InitPlan 1", 0)]
    [InlineData("($0 + 1)", "InitPlan 1 (returns $0)", 0)]
    public void Run_ValueNoInitPlanProves_KeepsNoOrigin(string value, string name, int parameters) =>
        AnswersOf(RunInitPlan(value, BuiltInTypes.Int8, parameters, InitPlan(name)))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // An InitPlan prints an EXISTS and an ARRAY as a scalar subquery: a boolean or an array is in no family.
    [Theory]
    [InlineData(BuiltInTypes.Bool)]
    [InlineData(BuiltInTypes.Jsonb)]
    public void Run_InitPlanOfATypeInNoFamily_KeepsNoOrigin(long type) =>
        AnswersOf(RunInitPlan("(InitPlan 1).col1", type, 0, InitPlan("InitPlan 1")))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // Two InitPlans that return one parameter say nothing of which it is.
    [Fact]
    public void Run_InitPlanReturningAParameterTwoPlansReturn_KeepsNoOrigin() =>
        AnswersOf(
                RunInitPlan(
                    "$0",
                    BuiltInTypes.Int8,
                    0,
                    InitPlan("InitPlan 1 (returns $0)"),
                    InitPlan("InitPlan 2 (returns $0)")
                )
            )
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // A plan that may return no row, or one whose value may be NULL.
    [Fact]
    public void Run_InitPlanThatMayReturnNoRow_KeepsNoOrigin()
    {
        var filtered = InitPlan("InitPlan 1") with { HasFilter = true };
        var keyed = InitPlan("InitPlan 1") with { HasGroupKey = true };
        var max = InitPlan("InitPlan 1") with { Output = ["max(o.quantity)"] };

        foreach (var plan in new[] { filtered, keyed, max })
        {
            AnswersOf(RunInitPlan("(InitPlan 1).col1", BuiltInTypes.Int8, 0, plan))
                .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
        }
    }

    // An InitPlan's value on the side a join null-extends is NULL for a row the join did not match.
    [Theory]
    [InlineData("(InitPlan 1).col1", "InitPlan 1")]
    [InlineData("$0", "InitPlan 1 (returns $0)")]
    public void Run_InitPlanOnTheSideAJoinExtends_KeepsNoOrigin(string value, string name)
    {
        var evaluates = Scan("b", "b.id", value);
        var join = Join("Left", Scan("a", "a.id"), evaluates) with
        {
            Output = ["a.id", "(" + value + ")"],
            Plans = [Scan("a", "a.id"), evaluates with { ParentRelationship = "Inner" }, InitPlan(name)],
        };
        var (columns, origins) = Built(false, Expression, Expression);

        AnswersOf(
                NullabilityWalk.Run(
                    new PlanInstancesCache(join, null),
                    columns,
                    origins,
                    new WalkScope(
                        false,
                        false,
                        true,
                        PostgresTables.Of(TableA, OrdersO),
                        [BuiltInTypes.Int4, BuiltInTypes.Int8]
                    )
                )
            )
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

    // a.id IN (SELECT o.user_id FROM o), as 17 and later print it, over a subplan that scans "o" for the column and
    // o.user_id.
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

    // EXISTS, as 17 and later print it, starts false and turns true at the subplan's first row.
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
