using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over a view's column, which is answered from the entry the plan prints for it.
public sealed class NullabilityWalkViewTests
{
    private static WalkOutcome RunOf(PlanNode root, params Asked[] asked)
    {
        var (columns, origins) = Built(false, asked);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(false, false, true, Tables, [.. ColumnTypes(asked.Length)])
        );
    }

    private static IEnumerable<long> ColumnTypes(int count)
    {
        for (var index = 0; index < count; index++)
        {
            yield return BuiltInTypes.Int4;
        }
    }

    // The view's first column, then a column of no origin, so that the two are no whole column list.
    private static readonly Asked[] Partial = [OfView, Expression];

    [Fact]
    public void Run_EntryOfANotNullColumn_IsProved() =>
        AnswersOf(RunOf(Scan("t", "t.id", "t.opt") with { Output = ["t.id", "1"] }, Partial))
            .ShouldBe([NotNullExpression, NotNullExpression]);

    [Fact]
    public void Run_EntryOfANullableColumn_KeepsTheView() =>
        AnswersOf(RunOf(Scan("t", "t.opt", "1"), Partial)).ShouldBe([UnknownView, NotNullExpression]);

    // A table's NOT NULL column is the catalog's to answer, though its entry would prove it too.
    [Fact]
    public void Run_NotNullColumnOfATable_KeepsTheCatalog() =>
        AnswersOf(RunOf(Scan("t", "t.id", "1"), NotNull, Expression)).ShouldBe([NotNullCatalog, NotNullExpression]);

    // What a statement writes through a view is not what the view's query reads.
    [Fact]
    public void Run_ViewColumnUnderARootModifyTable_KeepsTheView()
    {
        var root = Over(PlanNodeTypes.ModifyTable, Scan("t", "t.id", "1")) with
        {
            RelationName = "t",
            Alias = "t",
            Schema = "public",
            Output = ["t.id", "1"],
        };

        AnswersOf(RunOf(root, Partial)).ShouldBe([UnknownView, NotNullExpression]);
    }

    // The view's whole column list, each under its name and in its place, keeps the answers of the model named after
    // it.
    [Fact]
    public void Run_WholeColumnListOfTheView_KeepsTheView()
    {
        var (built, origins) = Built(false, OfView, new Asked(View, 2));
        var outcome = NullabilityWalk.Run(
            new PlanInstancesCache(Scan("t", "t.id", "t.id"), null),
            [built[0] with { Name = "id" }, built[1] with { Name = "opt" }],
            origins,
            new WalkScope(false, false, true, Tables, [.. ColumnTypes(2)])
        );

        AnswersOf(outcome).ShouldBe([UnknownView, UnknownView]);
    }
}
