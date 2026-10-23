using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Testing.NullabilityAnswers;
using static SqlSource.Tool.Tests.PostgresPlans;
using static SqlSource.Tool.Tests.PostgresWalkColumns;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// NullabilityWalk over the system columns of a table: where one is never NULL, and where the table may read rows from
// elsewhere.
public sealed class NullabilityWalkSystemColumnTests
{
    // A table whose NOT NULL columns the catalog answers, as "tables" gives it: no attribute is a system column.
    private static readonly PostgresTableRow Products = Table with { Oid = 16708, Name = "products" };

    // An INSERT into the table, with its RETURNING list as the root's Output.
    private static PlanNode InsertInto(PostgresTableRow table, params string[] output) =>
        Over("ModifyTable", Scan("x") with { NodeType = "Result", Alias = null }) with
        {
            Alias = table.Name,
            RelationName = table.Name,
            Schema = "public",
            Output = output,
        };

    private static WalkOutcome RunOver(PostgresTableRow table, PlanNode root)
    {
        var (columns, origins) = Built(false, Expression);
        return NullabilityWalk.Run(
            new PlanInstancesCache(root, null),
            columns,
            origins,
            new WalkScope(false, false, true, PostgresTables.Of(table), [])
        );
    }

    // A system column is never NULL in a row of a table, and "tables" reads no attribute of it.
    [Fact]
    public void Run_SystemColumnOfATable_IsAnsweredInAnExpression() =>
        AnswersOf(RunOver(Products, InsertInto(Products, "(products.xmax = 0)"))).ShouldBe([NotNullExpression]);

    // A partitioned table with no foreign partition keeps its NOT NULL in every partition, and every row has its system
    // columns.
    [Fact]
    public void Run_SystemColumnOfAPartitionedTable_IsAnsweredInAnExpression()
    {
        var parted = Products with { Relkind = "p", Relhassubclass = true };

        AnswersOf(RunOver(parted, InsertInto(parted, "(products.xmax = 0)"))).ShouldBe([NotNullExpression]);
    }

    // A system column of the side a join null-extends is NULL for a row the join did not match.
    [Theory]
    [InlineData("(a.xmax = 0)", false)]
    [InlineData("(b.xmax = 0)", null)]
    public void Run_SystemColumnThroughAnInstance_IsProvedOnlyWhereNoJoinExtendsIt(string entry, bool? nullable) =>
        AnswersOf(
                RunUngated(
                    Join("Left", Scan("a", "a.id", "a.xmax"), Scan("b", "b.id", "b.xmax")) with
                    {
                        Output = [entry],
                    },
                    Expression
                )
            )
            .ShouldBe([(nullable, nullable is null ? "no-origin" : "expression")]);

    // Nothing holds a foreign table's rows to anything.
    [Fact]
    public void Run_SystemColumnOfAForeignTable_KeepsNoOrigin() =>
        AnswersOf(RunOver(Foreign, Scan("ft", "(ft.xmax = 0)") with { NodeType = "Foreign Scan" }))
            .ShouldBe([UnknownNoOrigin]);

    // A table whose NOT NULL column the catalog would not answer false proves no system column either: an inheritance
    // child, a foreign partition or an INSTEAD rule brings rows from elsewhere.
    [Theory]
    [InlineData("children")]
    [InlineData("foreign-partition")]
    [InlineData("instead-rule")]
    public void Run_SystemColumnOfATableThatMayReadRowsFromElsewhere_KeepsNoOrigin(string reason)
    {
        var table = reason switch
        {
            "children" => Products with { Relhassubclass = true },
            "foreign-partition" => Products with { Relkind = "p", HasForeignPartition = true },
            _ => Products with { HasInsteadRule = true },
        };

        AnswersOf(RunOver(table, InsertInto(table, "(products.xmax = 0)"))).ShouldBe([UnknownNoOrigin]);
    }

    // Only the six names of the server's system columns are one, each of its own type.
    [Theory]
    [InlineData("(products.xmin = 0)", false)]
    [InlineData("((products.ctid)::text = 'x'::text)", null)]
    [InlineData("(products.oid = 0)", null)]
    [InlineData("(products.xmax = products.id)", false)]
    [InlineData("(products.xmax = products.opt)", null)]
    public void Run_SystemColumnInAnOperator_IsOfItsType(string entry, bool? nullable) =>
        AnswersOf(RunOver(Products, InsertInto(Products, entry)))
            .ShouldBe([(nullable, nullable is null ? "no-origin" : "expression")]);
}
