using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over the names a plan prints for a column: an alias, a bare name, the target of a ModifyTable, the
// old and the new row, and a MERGE at the root.
public sealed class NullabilityWalkNameTests
{
    // PostgreSQL 18's old row is no instance: only the target's alias is a reference the walk proves.
    [Fact]
    public void Run_ReferenceToTheOldRow_IsNotProved()
    {
        var plan = Over("ModifyTable", Scan("t")) with
        {
            Alias = "t",
            RelationName = "t",
            Schema = "public",
            Output = ["COALESCE(old.id, NULL::integer)", "COALESCE(t.id, NULL::integer)"],
        };

        AnswersOf(RunAs(Scope(true, oldAndNewMayBeNamed: true), plan, Expression, Expression))
            .ShouldBe([UnknownNoOrigin, NotNullExpression]);
    }

    // MERGE ... RETURNING merge_action() on 18, over the join of the source and the target that null-extends.
    [Fact]
    public void Run_MergeActionAtARootModifyTable_IsAnswered()
    {
        var join = Join("Right", Scan("o", "o.id"), Scan("a", "a.id")) with { Output = ["o.id", "a.id"] };
        var plan = Over("ModifyTable", join) with
        {
            Alias = "o",
            RelationName = "o",
            Schema = "public",
            Output = ["MERGE_ACTION()"],
        };

        AnswersOf(RunOverOrders(plan)).ShouldBe([NotNullExpression]);
    }

    [Theory]
    [InlineData("Subquery Scan")]
    [InlineData("CTE Scan")]
    [InlineData("Function Scan")]
    [InlineData("Values Scan")]
    [InlineData("WorkTable Scan")]
    public void Run_AliasOfANodeThatIsNoScanOfARelation_IsUnmappable(string nodeType)
    {
        var plan = Join("Inner", Scan("a"), Scan("s") with { NodeType = nodeType }) with { Output = ["s.id"] };

        var outcome = Run(plan, NotNull);

        outcome.Plan.ShouldBe(SidecarValues.Plan.Walked);
        AnswersOf(outcome).ShouldBe([NullableHeuristic]);
    }

    // A CTE's own plan is another plan: its alias is reached from here through the CTE Scan alone.
    [Fact]
    public void Run_AliasUnderAnInitPlan_IsNotFound()
    {
        var plan = Over("CTE Scan", Scan("u"), "InitPlan") with { Alias = "s", Output = ["u.id"] };

        AnswersOf(Run(plan, NotNull)).ShouldBe([NullableHeuristic]);
    }

    // A single-relation query prints bare names.
    [Fact]
    public void Run_BareNameAtARootThatScansARelation_IsThatRelationsColumn() =>
        AnswersOf(Run(Scan("users", "id", "opt"), NotNull, Optional)).ShouldBe([NotNullCatalog, NullableCatalog]);

    // So does a Result that replaced a scan the planner proved empty, beside a scan that remains: there the name
    // says nothing of which relation.
    [Fact]
    public void Run_BareNameUnderAJoin_IsUnmappable() =>
        AnswersOf(Run(LeftJoin("a.id", "opt"), NotNull, Optional)).ShouldBe([NotNullCatalog, NullableHeuristic]);

    // A DELETE of one table prints bare names, and its ModifyTable is its target.
    [Fact]
    public void Run_BareNameAtARootModifyTable_IsTheTargetsColumn()
    {
        var plan = Over("ModifyTable", Scan("orders")) with { Alias = "orders", Output = ["id", "opt"] };

        AnswersOf(Run(plan, NotNull, Optional)).ShouldBe([NotNullCatalog, NullableCatalog]);
    }

    // A bare name under a join at a ModifyTable may be a column of a relation the planner proved empty and replaced
    // with a Result, which the join null-extends: here "id" is the left join's inner side, NULL on every row.
    [Fact]
    public void Run_BareNameAtARootModifyTableOverAJoin_IsUnmappable()
    {
        var plan = Over(
            "ModifyTable",
            Join("Inner", Join("Left", Scan("a"), Scan("x") with { NodeType = "Result", Alias = null }), Scan("t"))
        ) with
        {
            Alias = "t",
            Output = ["id", "t.id"],
        };

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([NullableHeuristic, NotNullCatalog]);
    }

    // A MERGE that inserts what the target lacks and deletes what the source lacks: the row returned is the
    // target's own, and the source's columns are NULL for a row that was deleted.
    [Fact]
    public void Run_TargetUnderAFullJoin_IsNotExtendedAndTheSourceIs()
    {
        var plan = Over("ModifyTable", Join("Full", Scan("t"), Scan("s"))) with
        {
            Alias = "t",
            Output = ["t.id", "s.id"],
        };

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);
    }

    // PostgreSQL 18 prints its old row as "old.id", and an inner scope may alias a table "old" as well: in a
    // statement that names OLD or NEW, only the target's alias is mapped at a ModifyTable.
    private static readonly PlanNode ModifyOverOld = Over("ModifyTable", Scan("old")) with
    {
        Alias = "ord2",
        Output = ["old.id", "ord2.id"],
    };

    [Fact]
    public void Run_AliasAtAModifyTableThatIsNotTheTargetsWhereOldOrNewMayBeNamed_KeepsTheHeuristic() =>
        AnswersOf(Run(ModifyOverOld, true, NotNull, NotNull)).ShouldBe([NullableHeuristic, NotNullCatalog]);

    // The flag is what decides: without it the same plan maps the alias.
    [Fact]
    public void Run_AliasAtAModifyTableThatIsNotTheTargetsWhereNeitherIsNamed_TakesTheCatalogsAnswer() =>
        AnswersOf(Run(ModifyOverOld, false, NotNull, NotNull)).ShouldBe([NotNullCatalog, NotNullCatalog]);

    // The old and the new row are a RETURNING list's alone.
    [Fact]
    public void Run_RootThatIsNoModifyTable_IsNotAffectedByOldOrNew() =>
        AnswersOf(Run(LeftJoin("a.id", "b.id"), true, NotNull, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);

    [Fact]
    public void Run_QuotedAlias_NamesTheInstanceOfThatName()
    {
        var plan = Join("Left", Scan("u"), Scan("Order")) with { Output = ["u.id", "\"Order\".id"] };

        AnswersOf(Run(plan, NotNull, NotNull)).ShouldBe([NotNullCatalog, NullableOuterJoin]);
    }

    // A plan of one relation prints bare names at every node, here a Limit over a Sort over the scan.
    [Fact]
    public void Run_BareNameAtARootOverOneScan_IsThatRelationsColumn() =>
        AnswersOf(Run(Over("Limit", Over("Sort", Scan("t"))) with { Output = ["id", "opt"] }, NotNull, Optional))
            .ShouldBe([NotNullCatalog, NullableCatalog]);

    // A relation the planner proved empty is a Result under the outer join, and its columns print bare though the
    // plan has another relation: a bare name in an expression is no column of that one.
    [Fact]
    public void Run_BareNameInAnExpressionOfAPlanOfSeveralRelations_IsUnproved()
    {
        var empty = Scan("x") with { NodeType = "Result", Alias = null, RelationName = null, Output = ["id", "opt"] };
        var plan = Join("Left", Scan("t", "t.id"), empty) with
        {
            Output = ["t.id", "COALESCE(id, id)", "CASE WHEN (t.id > 0) THEN id ELSE 0 END"],
        };

        AnswersOf(RunUngated(plan, NotNull, Expression, Expression))
            .ShouldBe([NotNullCatalog, UnknownNoOrigin, UnknownNoOrigin]);
    }
}
