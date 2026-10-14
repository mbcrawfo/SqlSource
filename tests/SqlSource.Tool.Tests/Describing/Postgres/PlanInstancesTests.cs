using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// Steps 1 and 2 of the walk, over plans a test writes: which nodes name a relation instance, which of them a join
// above null-extends, and what makes a plan unusable.
public class PlanInstancesTests
{
    private static bool ExtendedIn(PlanInstances instances, string alias)
    {
        instances.TryFind(alias, out var extended).ShouldBeTrue(alias);
        return extended;
    }

    // "a" is the outer side and "b" the inner one.
    [Theory]
    [InlineData("Inner", false, false)]
    [InlineData("Left", false, true)]
    [InlineData("Right", true, false)]
    [InlineData("Full", true, true)]
    [InlineData("Semi", false, false)]
    [InlineData("Anti", false, false)]
    [InlineData("Right Semi", false, false)]
    [InlineData("Right Anti", false, false)]
    public void Find_JoinOfTwoScans_ExtendsTheSidesItsTypeSays(string joinType, bool outer, bool inner)
    {
        var instances = PlanInstances.Find(Join(joinType, Scan("a"), Scan("b")));

        instances.Reason.ShouldBeNull();
        ExtendedIn(instances, "a").ShouldBe(outer);
        ExtendedIn(instances, "b").ShouldBe(inner);
    }

    // A LEFT JOIN the planner swapped is a Right join with the sides swapped: the rule gives the same answer.
    [Fact]
    public void Find_SwappedLeftJoin_ExtendsTheOuterChild()
    {
        var instances = PlanInstances.Find(Join("Right", Scan("o"), Over("Hash", Scan("u"), Inner)));

        ExtendedIn(instances, "o").ShouldBeTrue();
        ExtendedIn(instances, "u").ShouldBeFalse();
    }

    // a LEFT JOIN b LEFT JOIN c: a join on the path is enough, however far above.
    [Fact]
    public void Find_ChainOfTwoLeftJoins_ExtendsEverythingUnderAnInnerSide()
    {
        var plan = Join("Left", Join("Left", Scan("a"), Scan("b")), Scan("c"));

        var instances = PlanInstances.Find(plan);

        ExtendedIn(instances, "a").ShouldBeFalse();
        ExtendedIn(instances, "b").ShouldBeTrue();
        ExtendedIn(instances, "c").ShouldBeTrue();
    }

    // a LEFT JOIN (b JOIN c): the inner join extends nothing, and the one above it both of its sides.
    [Fact]
    public void Find_InnerJoinUnderTheInnerSideOfALeftJoin_ExtendsBothOfItsScans()
    {
        var plan = Join("Left", Scan("a"), Join("Inner", Scan("b"), Scan("c")));

        var instances = PlanInstances.Find(plan);

        ExtendedIn(instances, "a").ShouldBeFalse();
        ExtendedIn(instances, "b").ShouldBeTrue();
        ExtendedIn(instances, "c").ShouldBeTrue();
    }

    [Theory]
    [InlineData("Seq Scan")]
    [InlineData("Index Scan")]
    [InlineData("Index Only Scan")]
    [InlineData("Bitmap Heap Scan")]
    [InlineData("Tid Scan")]
    [InlineData("Tid Range Scan")]
    [InlineData("Sample Scan")]
    public void Find_ScanOfARelation_NamesAnInstance(string nodeType)
    {
        var instances = PlanInstances.Find(Scan("u") with { NodeType = nodeType });

        ExtendedIn(instances, "u").ShouldBeFalse();
        instances.RootNamesAnInstance.ShouldBeTrue();
    }

    [Theory]
    [InlineData("Subquery Scan")]
    [InlineData("CTE Scan")]
    [InlineData("Function Scan")]
    [InlineData("Values Scan")]
    [InlineData("WorkTable Scan")]
    [InlineData("Foreign Scan")]
    [InlineData("Result")]
    public void Find_NodeThatIsNoScanOfARelation_NamesNone(string nodeType)
    {
        var instances = PlanInstances.Find(Scan("s") with { NodeType = nodeType });

        instances.TryFind("s", out _).ShouldBeFalse();
        instances.RootNamesAnInstance.ShouldBeFalse();
        instances.Reason.ShouldBeNull();
    }

    // The plan of a CTE hangs under whichever node first needs it: its scans are no instances of this plan's joins.
    [Theory]
    [InlineData("InitPlan")]
    [InlineData("SubPlan")]
    [InlineData("Member")]
    [InlineData("Subquery")]
    public void Find_ChildThatIsAnotherPlan_IsNotFollowed(string relationship)
    {
        var plan = Over("CTE Scan", Join("Bogus", Scan("u"), Scan("o")), relationship);

        var instances = PlanInstances.Find(plan);

        instances.TryFind("u", out _).ShouldBeFalse();
        instances.TryFind("o", out _).ShouldBeFalse();
        instances.Reason.ShouldBeNull();
        instances.Nodes.Count.ShouldBe(1);
    }

    [Fact]
    public void Find_JoinTypeTheWalkDoesNotKnow_IsUnusable() =>
        PlanInstances.Find(Join("Sideways", Scan("a"), Scan("b"))).Reason.ShouldBe(WalkReasons.UnknownJoinType);

    [Fact]
    public void Find_AliasFoundTwice_IsUnusable() =>
        PlanInstances.Find(Join("Inner", Scan("a"), Scan("a"))).Reason.ShouldBe(WalkReasons.DoubledAlias);

    [Fact]
    public void Find_NodeWithGroupingSets_IsUnusable()
    {
        var plan = Over("Aggregate", Scan("u")) with { HasGroupingSets = true };

        PlanInstances.Find(plan).Reason.ShouldBe(WalkReasons.GroupingSets);
    }

    [Fact]
    public void Find_MixedAggregate_IsUnusable() =>
        PlanInstances.Find(Over("MixedAggregate", Scan("u"))).Reason.ShouldBe(WalkReasons.GroupingSets);

    // The grouping sets of a subquery's own plan are found too, when the planner flattened the scan away.
    [Fact]
    public void Find_GroupingSetsUnderAJoin_IsUnusable()
    {
        var grouped = Over("Aggregate", Scan("o")) with { HasGroupingSets = true };

        PlanInstances.Find(Join("Left", Scan("u"), grouped)).Reason.ShouldBe(WalkReasons.GroupingSets);
    }

    // An UPDATE or a DELETE scans its target under the ModifyTable with the ModifyTable's own alias: one instance,
    // which no join extends, since the row returned is the one the statement acted on.
    [Fact]
    public void Find_RootModifyTableOverAScanOfItsOwnAlias_IsOneInstanceAndUsable()
    {
        var plan = Over("ModifyTable", Join("Full", Scan("t"), Scan("s"))) with { Alias = "t" };

        var instances = PlanInstances.Find(plan);

        instances.Reason.ShouldBeNull();
        instances.RootNamesAnInstance.ShouldBeTrue();
        ExtendedIn(instances, "t").ShouldBeFalse();
        ExtendedIn(instances, "s").ShouldBeTrue();
    }

    // An INSERT has a Result under it and no scan of its target.
    [Fact]
    public void Find_RootModifyTableWithoutAScanOfItsTarget_NamesItsTarget()
    {
        var plan = Over("ModifyTable", Scan("x") with { NodeType = "Result", Alias = null }) with { Alias = "orders" };

        ExtendedIn(PlanInstances.Find(plan), "orders").ShouldBeFalse();
    }

    [Fact]
    public void Find_TargetsAliasScannedTwiceUnderTheModifyTable_IsUnusable()
    {
        var plan = Over("ModifyTable", Join("Inner", Scan("t"), Scan("t"))) with { Alias = "t" };

        PlanInstances.Find(plan).Reason.ShouldBe(WalkReasons.DoubledAlias);
    }

    // Only the root's ModifyTable is a target: one further down is no scan of a relation.
    [Fact]
    public void Find_ModifyTableThatIsNotTheRoot_NamesNoInstance()
    {
        var plan = Over("Result", Over("ModifyTable", Scan("t")) with { Alias = "t" });

        var instances = PlanInstances.Find(plan);

        instances.Reason.ShouldBeNull();
        ExtendedIn(instances, "t").ShouldBeFalse();
        instances.RootNamesAnInstance.ShouldBeFalse();
    }

    // What the log says of each node, in the order of the visit.
    [Fact]
    public void Find_Plan_KeepsEachNodeItVisitedWithItsDepthAndWhatItExtends()
    {
        var plan = Over("Sort", Join("Left", Scan("u"), Join("Full", Scan("o"), Scan("i"))));

        var nodes = PlanInstances.Find(plan).Nodes;

        nodes
            .Select(static visited =>
                $"{visited.Depth} {visited.Node.NodeType} {visited.Node.Alias} {visited.NullExtends}"
            )
            .ShouldBe([
                "0 Sort  none",
                "1 Hash Join  inner",
                "2 Seq Scan u none",
                "2 Hash Join  both",
                "3 Seq Scan o none",
                "3 Seq Scan i none",
            ]);
    }

    [Fact]
    public void Find_RightJoin_SaysItExtendsItsOuterSide() =>
        PlanInstances.Find(Join("Right", Scan("a"), Scan("b"))).Nodes[0].NullExtends.ShouldBe("outer");
}
