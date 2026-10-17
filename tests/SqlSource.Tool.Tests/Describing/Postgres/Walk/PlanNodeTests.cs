using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// The plan as EXPLAIN (VERBOSE, FORMAT JSON) writes it, read into the keys the walk uses.
public sealed class PlanNodeTests
{
    // A LEFT JOIN the planner swapped, as PostgreSQL 18 wrote it, cut to a few keys and one that no reader knows.
    private const string Swapped = /*lang=json,strict*/
        """
        [
            {
                "Plan": {
                    "Node Type": "Hash Join",
                    "Parallel Aware": false,
                    "Join Type": "Right",
                    "Startup Cost": 13.15,
                    "Output": ["u.id", "o.total"],
                    "Inner Unique": true,
                    "Hash Cond": "(o.user_id = u.id)",
                    "Plans": [
                        {
                            "Node Type": "Seq Scan",
                            "Parent Relationship": "Outer",
                            "Relation Name": "orders",
                            "Schema": "public",
                            "Alias": "o",
                            "Output": ["o.id", "o.user_id", "o.total"]
                        },
                        {
                            "Node Type": "Hash",
                            "Parent Relationship": "Inner",
                            "Output": ["u.id"],
                            "Plans": [
                                {
                                    "Node Type": "CTE Scan",
                                    "Parent Relationship": "Outer",
                                    "CTE Name": "s",
                                    "Alias": "u",
                                    "Subplan Name": "CTE s"
                                }
                            ]
                        }
                    ]
                },
                "Planning Time": 0.1
            }
        ]
        """;

    [Fact]
    public void Read_Plan_ReadsEachKeyOfEveryNode()
    {
        var root = PlanNode.Read(Swapped).ShouldNotBeNull();

        root.NodeType.ShouldBe("Hash Join");
        root.JoinType.ShouldBe("Right");
        root.ParentRelationship.ShouldBeNull();
        root.Output.ShouldBe(["u.id", "o.total"]);
        root.Alias.ShouldBeNull();
        root.HasGroupingSets.ShouldBeFalse();
        root.Plans.Select(static child => child.NodeType).ShouldBe(["Seq Scan", "Hash"]);

        var scan = root.Plans[0];
        scan.ParentRelationship.ShouldBe("Outer");
        scan.Alias.ShouldBe("o");
        scan.RelationName.ShouldBe("orders");
        scan.Schema.ShouldBe("public");
        scan.JoinType.ShouldBeNull();
        scan.Plans.ShouldBeEmpty();

        var cte = root.Plans[1].Plans.ShouldHaveSingleItem();
        cte.NodeType.ShouldBe("CTE Scan");
        cte.CteName.ShouldBe("s");
        cte.SubplanName.ShouldBe("CTE s");
        cte.Output.ShouldBeNull();
    }

    [Theory]
    [InlineData("Intersect")]
    [InlineData("Except All")]
    public void Read_SetOp_KeepsItsCommand(string command)
    {
        var text = $$$"""[{"Plan": {"Node Type": "SetOp", "Command": "{{{command}}}", "Plans": []}}]""";

        PlanNode.Read(text).ShouldNotBeNull().Command.ShouldBe(command);
    }

    [Fact]
    public void Read_NodeWithoutACommand_HasNone() => PlanNode.Read(Swapped).ShouldNotBeNull().Command.ShouldBeNull();

    // The key is read as present or absent: what it holds is the planner's own.
    [Theory]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Aggregate", "Grouping Sets": [{"Group Keys": [["u.status"], []]}]}}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Aggregate", "Grouping Sets": []}}]"""
    )]
    public void Read_NodeWithGroupingSets_SaysSo(string text) =>
        PlanNode.Read(text).ShouldNotBeNull().HasGroupingSets.ShouldBeTrue();

    [Theory]
    [InlineData("")]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData( /*lang=json,strict*/
        """{"Plan": {"Node Type": "Result"}}"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Result"}}, {"Plan": {"Node Type": "Result"}}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Planning Time": 0.1}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": []}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Output": ["a"]}}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": 7}}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Result", "Output": "a"}}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Result", "Output": [1]}}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Result", "Alias": 1}}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "SetOp", "Command": 1}}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Result", "Plans": {}}}]"""
    )]
    [InlineData( /*lang=json,strict*/
        """[{"Plan": {"Node Type": "Result", "Plans": [{"Alias": "u"}]}}]"""
    )]
    public void Read_TextThatIsNotAnArrayOfOnePlan_IsNull(string text) => PlanNode.Read(text).ShouldBeNull();

    // A chain of nodes, each the one child of the last.
    private static string Nested(int nodes) =>
        "[{\"Plan\": "
        + string.Concat(Enumerable.Repeat("{\"Node Type\": \"Materialize\", \"Plans\": [", nodes - 1))
        + /*lang=json,strict*/
        "{\"Node Type\": \"Result\"}"
        + string.Concat(Enumerable.Repeat("]}", nodes - 1))
        + "}]";

    // Each node is two levels of the text, and the reader's own limit is 64: a plan of many joins is read all the
    // same.
    [Fact]
    public void Read_PlanOfAHundredNestedNodes_IsRead()
    {
        var node = PlanNode.Read(Nested(100)).ShouldNotBeNull();

        var depth = 1;
        while (node.Plans.Count > 0)
        {
            node = node.Plans[0];
            depth++;
        }

        depth.ShouldBe(100);
        node.NodeType.ShouldBe("Result");
    }

    // Past the limit the text is no plan the walk can use, and nothing is thrown.
    [Fact]
    public void Read_PlanNestedPastTheLimit_IsNull() => PlanNode.Read(Nested(400)).ShouldBeNull();

    [Fact]
    public void SelfAndDescendants_Tree_IsTheNodeThenEachChildAndAllUnderItInOrder()
    {
        var a = Scan("a");
        var b = Scan("b");
        var sort = Over("Sort", b);
        var join = Join("Inner", a, sort);

        join.SelfAndDescendants()
            .Select(static node => node.Alias ?? node.NodeType)
            .ShouldBe(["Hash Join", "a", "Sort", "b"]);
        join.SelfAndDescendants().First().ShouldBeSameAs(join);
    }

    // Every relationship is followed: an InitPlan, a SubPlan, a Member and a Subquery as well as the sides.
    [Fact]
    public void SelfAndDescendants_ChildOfEveryRelationship_IsIncluded()
    {
        var root = Scan("r") with
        {
            NodeType = "Result",
            Plans =
            [
                Scan("i") with
                {
                    ParentRelationship = "InitPlan",
                },
                Scan("s") with
                {
                    ParentRelationship = "SubPlan",
                },
                Scan("m") with
                {
                    ParentRelationship = "Member",
                },
                Scan("q") with
                {
                    ParentRelationship = "Subquery",
                },
            ],
        };

        root.SelfAndDescendants().Select(static node => node.Alias).ShouldBe(["r", "i", "s", "m", "q"]);
    }

    [Fact]
    public void SelfAndDescendants_Leaf_IsTheNodeAlone()
    {
        var scan = Scan("a");

        scan.SelfAndDescendants().ShouldHaveSingleItem().ShouldBeSameAs(scan);
    }

    [Fact]
    public void SubqueryChild_SubqueryScanWithOneSubqueryChild_IsThatChild()
    {
        var scan = Over("Subquery Scan", Scan("u"), "Subquery");

        scan.SubqueryChild.ShouldBeSameAs(scan.Plans[0]);
    }

    // An InitPlan beside the Subquery child is no second one.
    [Fact]
    public void SubqueryChild_SubqueryScanWithAnInitPlanBeside_IsTheSubqueryChild()
    {
        var scan = Over("Subquery Scan", Scan("u"), "Subquery");
        var cte = Scan("c") with { ParentRelationship = "InitPlan" };

        (scan with { Plans = [cte, .. scan.Plans] }).SubqueryChild.ShouldBeSameAs(scan.Plans[0]);
    }

    [Fact]
    public void SubqueryChild_SubqueryScanWithNoSubqueryChild_IsNull() =>
        Over("Subquery Scan", Scan("u"), "InitPlan").SubqueryChild.ShouldBeNull();

    [Fact]
    public void SubqueryChild_SubqueryScanWithTwoSubqueryChildren_IsNull()
    {
        var scan = Over("Subquery Scan", Scan("u"), "Subquery");

        (scan with { Plans = [.. scan.Plans, .. scan.Plans] }).SubqueryChild.ShouldBeNull();
    }

    // Read on each call: a copy of another node type has none, though its original had one.
    [Fact]
    public void SubqueryChild_CopyOfAnotherNodeType_IsNull() =>
        (Over("Subquery Scan", Scan("u"), "Subquery") with { NodeType = "CTE Scan" }).SubqueryChild.ShouldBeNull();

    [Fact]
    public void JoinChildren_NodeWithChildrenOfEveryRelationship_IsItsOuterAndInnerOnesInOrder()
    {
        var node = Join("Inner", Scan("o"), Scan("i"));
        node = node with
        {
            Plans =
            [
                Scan("x") with
                {
                    ParentRelationship = "InitPlan",
                },
                node.Plans[0],
                Scan("s") with
                {
                    ParentRelationship = "SubPlan",
                },
                Scan("m") with
                {
                    ParentRelationship = "Member",
                },
                node.Plans[1],
                Scan("q") with
                {
                    ParentRelationship = "Subquery",
                },
            ],
        };

        node.JoinChildren.Select(static child => child.Alias).ShouldBe(["o", "i"]);
    }

    [Fact]
    public void JoinChildren_Leaf_IsEmpty() => Scan("a").JoinChildren.ShouldBeEmpty();
}
