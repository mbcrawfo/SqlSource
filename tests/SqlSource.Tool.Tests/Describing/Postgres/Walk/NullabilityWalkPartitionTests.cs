using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over a partitioned table, whose partitions are answered by "partitions" and "relations".
public sealed class NullabilityWalkPartitionTests
{
    // The relations scanned here, as "partitions" answers: t_1 and t_2, scanned as p_1 and p_2, are partitions of t.
    private static readonly Dictionary<PostgresRelationName, IReadOnlyList<long>> Partitions = new()
    {
        [new PostgresRelationName("public", "t_1")] = [Table.Oid],
        [new PostgresRelationName("public", "t_2")] = [Table.Oid],
    };

    private static PlanNode PartitionsOfT() =>
        Append(Member("p_1", "t_1", "p_1.id", "p_1.opt"), Member("p_2", "t_2", "p_2.id", "p_2.opt"));

    private static WalkOutcome RunWithPartitions(PlanNode root, params Asked[] asked)
    {
        var (columns, origins) = Built(asked);
        return NullabilityWalk.Run(new PlanInstancesCache(root, Partitions), columns, origins, Scope(true));
    }

    [Fact]
    public void Run_ColumnOfAPartitionedTable_IsAnsweredByItsSide()
    {
        var preserved = Join("Left", PartitionsOfT(), Scan("b")) with { Output = ["p.id", "p.opt", "b.id"] };
        var extended = Join("Left", Scan("b"), PartitionsOfT()) with { Output = ["p.id", "b.id"] };

        AnswersOf(RunWithPartitions(preserved, NotNull, Optional, OfB))
            .ShouldBe([NotNullCatalog, NullableCatalog, NullableOuterJoin]);
        AnswersOf(RunWithPartitions(extended, NotNull, OfB)).ShouldBe([NullableOuterJoin, NotNullCatalog]);
    }

    // Without the answer of "partitions" the Append is read as a set operation.
    [Fact]
    public void Run_PartitionedTableWithoutPartitions_KeepsTheHeuristic() =>
        AnswersOf(Run(Join("Left", PartitionsOfT(), Scan("b")) with { Output = ["p.id"] }, NotNull))
            .ShouldBe([NullableHeuristic]);

    // A partitioned table is no set operation: its expressions are read, and a reference to it is the table's.
    [Fact]
    public void Run_ExpressionOverAPartitionedTable_IsAnswered()
    {
        var plan = Over("Aggregate", PartitionsOfT()) with { Output = ["count(*)", "GREATEST(p.opt, p.id)"] };

        AnswersOf(RunWithPartitions(plan, Expression, Expression)).ShouldBe([NotNullExpression, NotNullExpression]);
    }

    // The partitions of t as "relations" answers them, by schema and name: like t, with a NOT NULL id.
    private static readonly PostgresTableRow PartitionOne = Table with { Oid = 16720, Name = "t_1" };

    private static readonly PostgresTableRow PartitionTwo = Table with { Oid = 16721, Name = "t_2" };

    // An expression of each kind over the partitioned table: a column's and a system column's.
    private static readonly string[] OverPartitions = ["(p.id = 0)", "(p.xmax = 0)"];

    // A walk of the expressions over t's partitions, with the rows the test gives in place of "tables" and "relations".
    private static WalkOutcome RunOverPartitions(params PostgresTableRow[] rows) =>
        RunOverPartitions(PartitionsOfT(), rows);

    private static WalkOutcome RunOverPartitions(PlanNode partitions, params PostgresTableRow[] rows)
    {
        var plan = Over("Aggregate", partitions) with { Output = OverPartitions };
        var (columns, origins) = Built(false, Expression, Expression);
        return NullabilityWalk.Run(
            new PlanInstancesCache(plan, Partitions),
            columns,
            origins,
            new WalkScope(false, false, true, PostgresTables.Of(rows), [])
        );
    }

    // "relations" asks for the relations the plan scans, which are the partitions and never their parent: with no row
    // of the parent, a reference is proved by every member's row.
    [Fact]
    public void Run_ExpressionOverAPartitionedTableWithoutItsRow_IsProvedByItsPartitions() =>
        AnswersOf(RunOverPartitions(PartitionOne, PartitionTwo)).ShouldBe([NotNullExpression, NotNullExpression]);

    // Every member must prove it: a member without a row, a foreign partition, whose remote rows nothing holds to its
    // NOT NULL, a partition whose column is nullable, or two partitions whose column is of two types each leave it.
    [Theory]
    [InlineData("missing")]
    [InlineData("foreign")]
    [InlineData("nullable")]
    [InlineData("another-type")]
    public void Run_ExpressionOverPartitionsThatDoNotAllProveIt_KeepsNoOrigin(string reason)
    {
        var (second, answers) = reason switch
        {
            "missing" => ((PostgresTableRow?)null, new[] { UnknownNoOrigin, UnknownNoOrigin }),
            "foreign" => (PartitionTwo with { Relkind = "f" }, [UnknownNoOrigin, UnknownNoOrigin]),
            "nullable" => (
                PartitionTwo with
                {
                    Columns = [PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid), Table.Columns[1]],
                },
                [UnknownNoOrigin, NotNullExpression]
            ),
            _ => (
                PartitionTwo with
                {
                    Columns =
                    [
                        PostgresTables.Attribute(1, "id", PostgresRows.Int8.Oid, notNull: true),
                        Table.Columns[1],
                    ],
                },
                [UnknownNoOrigin, NotNullExpression]
            ),
        };

        AnswersOf(RunOverPartitions(second is null ? [PartitionOne] : [PartitionOne, second])).ShouldBe(answers);
    }

    // An Append that partition pruning removed a member of shows not every partition: its members' rows prove nothing.
    [Fact]
    public void Run_ExpressionOverPartitionsWithSubplansRemoved_KeepsNoOrigin() =>
        AnswersOf(RunOverPartitions(PartitionsOfT() with { SubplansRemoved = 1 }, PartitionOne, PartitionTwo))
            .ShouldBe([UnknownNoOrigin, UnknownNoOrigin]);

    // The parent's own row, when a column names it, decides as it did: here it holds id nullable, which a partition
    // may make NOT NULL of its own, and the reference is not proved.
    [Fact]
    public void Run_ExpressionOverAPartitionedTableWithItsRow_IsDecidedByThatRow()
    {
        var parent = Table with
        {
            Relkind = "p",
            Columns = [PostgresTables.Attribute(1, "id", PostgresRows.Int4.Oid), Table.Columns[1]],
        };

        AnswersOf(RunOverPartitions(parent, PartitionOne, PartitionTwo)).ShouldBe([UnknownNoOrigin, NotNullExpression]);
    }
}
