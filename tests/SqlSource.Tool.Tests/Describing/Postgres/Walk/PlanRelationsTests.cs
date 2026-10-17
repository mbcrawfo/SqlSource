using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// Which relations a plan scans, for "relations" to be asked of them.
public sealed class PlanRelationsTests
{
    private static PostgresRelationName Name(string schema, string name) => new(schema, name);

    // The members of a set operation are scans like any other, and the answer is in schema and name order.
    [Fact]
    public void Of_MembersOfAnAppend_AreTheirRelationsInOrder() =>
        PlanRelations
            .Of(
                Append(Member("o", "orders"), Member("u", "users"), Member("i", "invoices") with { Schema = "billing" })
            )
            .ShouldBe([Name("billing", "invoices"), Name("public", "orders"), Name("public", "users")]);

    // The leaf scan of a branch is under a Subquery Scan, which is the child of a member or of a join.
    [Fact]
    public void Of_ScanUnderASubqueryChild_IsFound() =>
        PlanRelations.Of(Over("Subquery Scan", Scan("users"), "Subquery")).ShouldBe([Name("public", "users")]);

    [Fact]
    public void Of_ScanInAnInitPlanAndInAnInnerChild_AreFound()
    {
        var cte = Over("Limit", Scan("orders")) with { ParentRelationship = "InitPlan" };
        var join = Join("Inner", Scan("users"), Scan("events"));

        PlanRelations
            .Of(join with { Plans = [cte, .. join.Plans] })
            .ShouldBe([Name("public", "events"), Name("public", "orders"), Name("public", "users")]);
    }

    [Fact]
    public void Of_ScanInASubPlan_IsFound() =>
        PlanRelations.Of(Over("Result", Scan("orders"), "SubPlan")).ShouldBe([Name("public", "orders")]);

    // A relation scanned twice, by two aliases or by two branches, is asked once.
    [Fact]
    public void Of_RelationScannedTwice_IsOnce() =>
        PlanRelations
            .Of(Append(Member("a", "users"), Member("b", "users"), Over("Sort", Member("c", "users"))))
            .ShouldBe([Name("public", "users")]);

    // A scan that prints no schema or no relation, and a node that is no scan, name nothing to ask.
    [Fact]
    public void Of_ScanWithoutASchemaOrARelation_IsNotFound() =>
        PlanRelations
            .Of(
                Append(
                    Member("a", "users") with
                    {
                        Schema = null,
                    },
                    Member("b", "orders") with
                    {
                        RelationName = null,
                    },
                    Member("f", "generate_series") with
                    {
                        NodeType = "Function Scan",
                    }
                )
            )
            .ShouldBeEmpty();

    [Fact]
    public void Of_ComparesNamesByOrdinal() =>
        PlanRelations
            .Of(Append(Member("a", "b"), Member("b", "B"), Member("c", "a")))
            .ShouldBe([Name("public", "B"), Name("public", "a"), Name("public", "b")]);

    // The one sort of what "relations" and "partitions" are asked: each once, by schema and then by name, by ordinal.
    [Fact]
    public void InOrder_Relations_AreEachOnceBySchemaThenNameByOrdinal() =>
        PlanRelations
            .InOrder([Name("b", "x"), Name("a", "y"), Name("a", "x"), Name("b", "x"), Name("B", "x"), Name("a", "X")])
            .ShouldBe([Name("B", "x"), Name("a", "X"), Name("a", "x"), Name("a", "y"), Name("b", "x")]);

    [Fact]
    public void InOrder_NoRelation_IsEmpty() => PlanRelations.InOrder([]).ShouldBeEmpty();
}
