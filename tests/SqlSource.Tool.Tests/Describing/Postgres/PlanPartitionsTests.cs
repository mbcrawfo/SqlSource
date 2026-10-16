using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Which Appends of a plan may scan a partitioned table, what "partitions" is asked of them, and what their members
// must agree on.
public class PlanPartitionsTests
{
    private static PlanNode Member(string alias, string relation, params string[] output) =>
        Scan(alias, output) with
        {
            RelationName = relation,
            ParentRelationship = "Member",
        };

    private static PlanNode Append(params PlanNode[] members) => Over("Append", Scan("x")) with { Plans = members };

    private static readonly Dictionary<PostgresRelationName, IReadOnlyList<long>> Partitions = new()
    {
        [new PostgresRelationName("public", "e_2024")] = [100, 200],
        [new PostgresRelationName("public", "e_2025")] = [100, 200],
        [new PostgresRelationName("public", "other")] = [300, 200],
    };

    [Fact]
    public void MembersOf_AppendOfScansUnderOnePrefix_IsTheirRelationsInOrder() =>
        PlanPartitions
            .MembersOf(Over("Sort", Append(Member("e_2", "e_2025"), Member("e_1", "e_2024"))))
            .ShouldBe([new PostgresRelationName("public", "e_2024"), new PostgresRelationName("public", "e_2025")]);

    // An Append anywhere counts, the plan of a CTE or of a subquery included, and a relation is asked once.
    [Fact]
    public void MembersOf_AppendsInsideOtherPlans_AreAskedOnce()
    {
        var cte = Append(Member("e_1", "e_2024")) with { ParentRelationship = "InitPlan" };
        var scan = Over("Subquery Scan", Append(Member("f_1", "e_2024")), "Subquery");

        PlanPartitions
            .MembersOf(Join("Inner", scan, Scan("u")) with { Plans = [cte, .. Join("Inner", scan, Scan("u")).Plans] })
            .ShouldBe([new PostgresRelationName("public", "e_2024")]);
    }

    public static TheoryData<string> NoShape =>
        [
            Text(Append(Member("e_1", "e_2024"), Member("f_2", "e_2025"))),
            Text(Append(Member("e_1", "e_2024"), Member("e", "e_2025"))),
            Text(Append(Member("e_1", "e_2024"), Member("e_x", "e_2025"))),
            Text(
                Append(
                    Member("e_1", "e_2024"),
                    Over("Subquery Scan", Scan("s"), "Subquery") with
                    {
                        ParentRelationship = "Member",
                        Alias = "e_2",
                    }
                )
            ),
            Text(Append(Member("e_1", "e_2024"), Member("e_2", "e_2025") with { ParentRelationship = "Outer" })),
            Text(Over("Append", Scan("x")) with { Plans = [] }),
            Text(Over("Sort", Member("e_1", "e_2024"))),
        ];

    [Theory]
    [MemberData(nameof(NoShape))]
    public void MembersOf_AppendOfAnotherShape_IsEmpty(string plan) =>
        PlanPartitions.MembersOf(PlanNode.Read(plan).ShouldNotBeNull()).ShouldBeEmpty();

    [Fact]
    public void ShapeOf_AppendOfScansUnderOnePrefix_IsThePrefix()
    {
        PlanPartitions.ShapeOf(Append(Member("e_1", "e_2024"), Member("e_2", "e_2025"))).ShouldBe("e");
        PlanPartitions.ShapeOf(Append(Member("e_1_1", "e_2024"))).ShouldBe("e_1");
        PlanPartitions.ShapeOf(Append(Member("e_1", "e_2024")) with { NodeType = "Merge Append" }).ShouldBe("e");
    }

    [Fact]
    public void ParentsOf_MembersOfOneTableWithTheSameOutput_AreTheirCommonAncestors() =>
        PlanPartitions
            .ParentsOf(
                Append(
                    Member("e_1", "e_2024", "e_1.id", "(e_1.payload ->> 'e_1.x'::text)"),
                    Member("e_2", "e_2025", "e_2.id", "(e_2.payload ->> 'e_1.x'::text)")
                ),
                Partitions
            )
            .ShouldBe([100, 200]);

    // A constant that holds the alias's text is no alias.
    [Fact]
    public void ParentsOf_OutputsThatDifferInAConstant_AreNone() =>
        PlanPartitions
            .ParentsOf(
                Append(Member("e_1", "e_2024", "'e_1.x'::text"), Member("e_2", "e_2025", "'e_2.x'::text")),
                Partitions
            )
            .ShouldBeEmpty();

    [Fact]
    public void ParentsOf_QuotedAliases_AreReadAsTheirNames() =>
        PlanPartitions
            .ParentsOf(Append(Member("E_1", "e_2024", "\"E_1\".id"), Member("E_2", "e_2025", "\"E_2\".id")), Partitions)
            .ShouldBe([100, 200]);

    public static TheoryData<string> NotOneTable =>
        [
            Text(Append(Member("e_1", "e_2024", "e_1.id"), Member("e_2", "e_2025", "e_2.kind"))),
            Text(Append(Member("e_1", "e_2024", "e_1.id"), Member("e_2", "e_2025"))),
            Text(Append(Member("e_1", "e_2024"), Member("e_2", "missing"))),
        ];

    [Theory]
    [MemberData(nameof(NotOneTable))]
    public void ParentsOf_MembersThatDifferOrAreNotAnswered_AreNone(string plan) =>
        PlanPartitions.ParentsOf(PlanNode.Read(plan).ShouldNotBeNull(), Partitions).ShouldBeEmpty();

    // Members of two tables share only the ancestor above both.
    [Fact]
    public void ParentsOf_MembersOfTwoTablesUnderOneAncestor_AreThatAncestor() =>
        PlanPartitions.ParentsOf(Append(Member("e_1", "e_2024"), Member("e_2", "other")), Partitions).ShouldBe([200]);
}
