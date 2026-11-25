using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Statements;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over what the SQL of a VALUES statement says of its columns: it counts only for a plan that is a
// Values Scan under nodes that never project, with each column printed in its place.
public sealed class NullabilityWalkValuesTests
{
    private static PlanNode ValuesScan(params string[] output) =>
        Scan("*VALUES*", output) with
        {
            NodeType = PlanNodeTypes.ValuesScan,
        };

    private static WalkOutcome RunOf(PlanNode root, IReadOnlyList<bool>? neverNull, int columnCount = 2)
    {
        var (columns, origins) = Built(false, [.. Enumerable.Repeat(Expression, columnCount)]);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(
                false,
                OldAndNewNames.None,
                true,
                PostgresTables.Of(),
                [.. Enumerable.Repeat(BuiltInTypes.Int4, columnCount)],
                ValuesNeverNull: neverNull
            )
        );
    }

    [Theory]
    [InlineData("Values Scan")]
    [InlineData("Sort")]
    [InlineData("Incremental Sort")]
    [InlineData("Limit")]
    public void Run_ValuesScanUnderNodesThatNeverProject_AnswersItsLiteralColumns(string rootType)
    {
        var scan = ValuesScan("column1", "column2");
        var root =
            rootType == PlanNodeTypes.ValuesScan ? scan : Over(rootType, scan) with { Output = ["column1", "column2"] };

        AnswersOf(RunOf(root, [true, false])).ShouldBe([NotNullExpression, UnknownNoOrigin]);
    }

    [Fact]
    public void Run_EntryQualifiedByTheScansAlias_IsAnswered() =>
        AnswersOf(RunOf(ValuesScan("\"*VALUES*\".column1", "\"*VALUES*\".column2"), [true, true]))
            .ShouldBe([NotNullExpression, NotNullExpression]);

    // A plan that is no Values Scan, a node above it that projects, and a scan under a join.
    [Fact]
    public void Run_PlanThatIsNoValuesScan_KeepsNoOrigin() =>
        AnswersOf(RunOf(Scan("v", "column1", "column2"), [true, true])).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    [Fact]
    public void Run_ProjectingNodeOverAValuesScan_KeepsNoOrigin() =>
        AnswersOf(
                RunOf(
                    Over("Result", ValuesScan("column1", "column2")) with
                    {
                        Output = ["column1", "column2"],
                    },
                    [true, true]
                )
            )
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // The SQL's columns are the plan's only when each prints in its place.
    [Theory]
    [InlineData("column2", "column1")]
    [InlineData("column1", "column3")]
    [InlineData("x.column1", "x.column2")]
    [InlineData("(column1)", "column2")]
    public void Run_EntriesOutOfPlace_KeepNoOrigin(string first, string second) =>
        AnswersOf(RunOf(ValuesScan(first, second), [true, true])).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // The SQL gave another number of columns, or no answer, since the statement is no VALUES of that shape.
    [Fact]
    public void Run_AnswerOfAnotherLength_KeepsNoOrigin() =>
        AnswersOf(RunOf(ValuesScan("column1", "column2"), [true])).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    [Fact]
    public void Run_NoAnswerFromTheSql_KeepsNoOrigin() =>
        AnswersOf(RunOf(ValuesScan("column1", "column2"), null)).ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);
}
