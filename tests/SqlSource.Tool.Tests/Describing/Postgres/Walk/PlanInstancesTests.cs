using System.Collections.Generic;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// PlanInstances over plans a test writes: which nodes name a relation instance, which of them a join above
// null-extends, and what makes a plan unusable.
public sealed class PlanInstancesTests
{
    private static bool ExtendedIn(PlanInstances instances, string alias)
    {
        instances.TryFind(alias, out var instance).ShouldBeTrue(alias);
        return instance.NullExtended;
    }

    // The answer of "partitions": parts_1 and parts_2 are partitions of 16800, which is a partition of 16801.
    private static readonly Dictionary<PostgresRelationName, IReadOnlyList<long>> Partitions = new()
    {
        [new PostgresRelationName("public", "parts_1")] = [16800, 16801],
        [new PostgresRelationName("public", "parts_2")] = [16800, 16801],
        [new PostgresRelationName("public", "other")] = [16900],
        [new PostgresRelationName("public", "plain")] = [],
    };

    // The instances of a whole plan, with no answer of "partitions": an Append of a partitioned table's partitions is
    // read as a set operation.
    private static PlanInstances Find(PlanNode root) => PlanInstances.Find(root, root, PlanInstances.NoPartitions);

    private static PlanInstances WithPartitions(PlanNode root) => PlanInstances.Find(root, root, Partitions);

    // "a" is the outer side and "b" the inner one.
    [Theory]
    [InlineData("Inner", false, false)]
    [InlineData("Left", false, true)]
    [InlineData("Right", true, false)]
    [InlineData("Full", true, true)]
    [InlineData("Semi", false, false)]
    [InlineData("Anti", false, true)]
    [InlineData("Right Semi", false, false)]
    [InlineData("Right Anti", true, false)]
    public void Find_JoinOfTwoScans_ExtendsTheSidesItsTypeSays(string joinType, bool outer, bool inner)
    {
        var instances = Find(Join(joinType, Scan("a"), Scan("b")));

        instances.Reason.ShouldBeNull();
        ExtendedIn(instances, "a").ShouldBe(outer);
        ExtendedIn(instances, "b").ShouldBe(inner);
    }

    // A LEFT JOIN the planner swapped is a Right join with the sides swapped: the rule gives the same answer.
    [Fact]
    public void Find_SwappedLeftJoin_ExtendsTheOuterChild()
    {
        var instances = Find(Join("Right", Scan("o"), Over("Hash", Scan("u"), Inner)));

        ExtendedIn(instances, "o").ShouldBeTrue();
        ExtendedIn(instances, "u").ShouldBeFalse();
    }

    // a LEFT JOIN b LEFT JOIN c: a join on the path is enough, however far above.
    [Fact]
    public void Find_ChainOfTwoLeftJoins_ExtendsEverythingUnderAnInnerSide()
    {
        var plan = Join("Left", Join("Left", Scan("a"), Scan("b")), Scan("c"));

        var instances = Find(plan);

        ExtendedIn(instances, "a").ShouldBeFalse();
        ExtendedIn(instances, "b").ShouldBeTrue();
        ExtendedIn(instances, "c").ShouldBeTrue();
    }

    // a LEFT JOIN (b JOIN c): the inner join extends nothing, and the one above it both of its sides.
    [Fact]
    public void Find_InnerJoinUnderTheInnerSideOfALeftJoin_ExtendsBothOfItsScans()
    {
        var plan = Join("Left", Scan("a"), Join("Inner", Scan("b"), Scan("c")));

        var instances = Find(plan);

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
    [InlineData("Foreign Scan")]
    public void Find_ScanOfARelation_NamesAnInstance(string nodeType)
    {
        var instances = Find(Scan("u") with { NodeType = nodeType });

        ExtendedIn(instances, "u").ShouldBeFalse();
        instances.TryFind("u", out var instance).ShouldBeTrue();
        (instance.Kind, instance.Schema, instance.Relation).ShouldBe((InstanceKind.Relation, "public", "u"));
        instances.BareAlias.ShouldBe("u");
    }

    [Theory]
    [InlineData("Function Scan")]
    [InlineData("Values Scan")]
    [InlineData("WorkTable Scan")]
    [InlineData("Result")]
    public void Find_NodeThatIsNoScanOfARelation_NamesNone(string nodeType)
    {
        var instances = Find(Scan("s") with { NodeType = nodeType });

        instances.TryFind("s", out _).ShouldBeFalse();
        instances.BareAlias.ShouldBeNull();
        instances.Reason.ShouldBeNull();
    }

    // A foreign join pushed down to the remote server is one Foreign Scan with no relation and no alias.
    [Fact]
    public void Find_ForeignScanOfAPushedDownJoin_NamesNone()
    {
        var instances = Find(Scan("f") with { NodeType = "Foreign Scan", Alias = null, RelationName = null });

        instances.Nodes.Count.ShouldBe(1);
        instances.BareAlias.ShouldBeNull();
    }

    // A Subquery Scan is an instance of the plan of its Subquery child, which is not visited.
    [Fact]
    public void Find_SubqueryScan_IsAnInstanceOfItsChildsPlan()
    {
        var child = Join("Left", Scan("u"), Scan("o"));
        var plan = Over("Sort", Over("Subquery Scan", child, "Subquery") with { Alias = "s" });

        var instances = Find(plan);

        instances.TryFind("s", out var scan).ShouldBeTrue();
        scan.Kind.ShouldBe(InstanceKind.Plan);
        scan.Child.ShouldBeSameAs(plan.Plans[0].Plans[0]);
        instances.TryFind("u", out _).ShouldBeFalse();
        instances.BareAlias.ShouldBeNull();
    }

    // A CTE Scan is an instance of the one InitPlan of the whole tree named for its CTE, wherever it hangs.
    [Fact]
    public void Find_CteScan_IsAnInstanceOfItsCtesInitPlan()
    {
        var cte = Scan("o") with { ParentRelationship = "InitPlan", SubplanName = "CTE recent" };
        var scans = Join(
            "Left",
            Scan("r") with
            {
                NodeType = "CTE Scan",
                CteName = "recent",
                RelationName = null,
            },
            Scan("n") with
            {
                NodeType = "CTE Scan",
                CteName = "recent",
                RelationName = null,
            }
        );
        var plan = Over("Sort", scans) with { Plans = [cte, .. Over("Sort", scans).Plans] };

        var instances = Find(plan);

        instances.TryFind("r", out var r).ShouldBeTrue();
        instances.TryFind("n", out var n).ShouldBeTrue();
        (r.Kind, r.NullExtended, n.NullExtended).ShouldBe((InstanceKind.Plan, false, true));
        r.Child.ShouldBeSameAs(cte);
        n.Child.ShouldBeSameAs(cte);
    }

    // Two InitPlans of one name, a CTE of the same name at two levels, say nothing of which a scan is of.
    [Fact]
    public void Find_CteScanOfANameTwoInitPlansHave_HasNoChild()
    {
        var cte = Scan("o") with { ParentRelationship = "InitPlan", SubplanName = "CTE recent" };
        var scan = Scan("r") with { NodeType = "CTE Scan", CteName = "recent", RelationName = null };
        var plan = Over("Sort", scan) with
        {
            Plans = [cte, cte with { Alias = "o2" }, scan with { ParentRelationship = "Outer" }],
        };

        Find(plan).TryFind("r", out var r).ShouldBeTrue();
        r.Child.ShouldBeNull();
    }

    // A child that is another plan, an InitPlan, a SubPlan, a Member or a Subquery, is not followed: its scans are no
    // instances of this plan's joins.
    [Theory]
    [InlineData("InitPlan")]
    [InlineData("SubPlan")]
    [InlineData("Member")]
    [InlineData("Subquery")]
    public void Find_ChildThatIsAnotherPlan_IsNotFollowed(string relationship)
    {
        var plan = Over("CTE Scan", Join("Bogus", Scan("u"), Scan("o")), relationship);

        var instances = Find(plan);

        instances.TryFind("u", out _).ShouldBeFalse();
        instances.TryFind("o", out _).ShouldBeFalse();
        instances.Reason.ShouldBeNull();
        instances.Nodes.Count.ShouldBe(1);
    }

    [Fact]
    public void Find_JoinTypeTheWalkDoesNotKnow_IsUnusable() =>
        Find(Join("Sideways", Scan("a"), Scan("b"))).Reason.ShouldBe(WalkReasons.UnknownJoinType);

    [Fact]
    public void Find_AliasFoundTwice_IsUnusable() =>
        Find(Join("Inner", Scan("a"), Scan("a"))).Reason.ShouldBe(WalkReasons.DoubledAlias);

    [Fact]
    public void Find_NodeWithGroupingSets_IsUnusable()
    {
        var plan = Over("Aggregate", Scan("u")) with { HasGroupingSets = true };

        Find(plan).Reason.ShouldBe(WalkReasons.GroupingSets);
    }

    // The grouping sets of a subquery's own plan are found too, when the planner flattened the scan away.
    [Fact]
    public void Find_GroupingSetsUnderAJoin_IsUnusable()
    {
        var grouped = Over("Aggregate", Scan("o")) with { HasGroupingSets = true };

        Find(Join("Left", Scan("u"), grouped)).Reason.ShouldBe(WalkReasons.GroupingSets);
    }

    // An UPDATE or a DELETE scans its target under the ModifyTable with the ModifyTable's own alias: one instance,
    // which no join extends, since the row returned is the one the statement acted on.
    [Fact]
    public void Find_RootModifyTableOverAScanOfItsOwnAlias_IsOneInstanceAndUsable()
    {
        var plan = Over("ModifyTable", Join("Full", Scan("t"), Scan("s"))) with { Alias = "t" };

        var instances = Find(plan);

        instances.Reason.ShouldBeNull();
        instances.BareAlias.ShouldBeNull();
        ExtendedIn(instances, "t").ShouldBeFalse();
        ExtendedIn(instances, "s").ShouldBeTrue();
    }

    // A RETURNING list prints a bare name for a relation the planner proved empty and replaced with a Result, which
    // no node names: under a join that null-extends a bare name says nothing of which relation it is a column of.
    // Only such a join can hide one, so under any other a bare name is the target's.
    [Theory]
    [InlineData("Inner", "t")]
    [InlineData("Semi", "t")]
    [InlineData("Left", null)]
    [InlineData("Anti", null)]
    public void Find_RootModifyTableOverAJoin_NamesItsTargetByABareNameUnlessTheJoinExtends(
        string joinType,
        string? bare
    )
    {
        var plan = Over("ModifyTable", Join(joinType, Scan("t"), Scan("a"))) with { Alias = "t" };

        var instances = Find(plan);

        instances.BareAlias.ShouldBe(bare);
        ExtendedIn(instances, "t").ShouldBeFalse();
    }

    // A plan of one relation prints bare names at every node: a root that reaches one scan through nodes of one Outer
    // child each names it.
    [Fact]
    public void BareAlias_RootOverOneScanThroughSingleChildren_IsTheScans() =>
        Find(Over("Limit", Over("Sort", Over("Aggregate", Scan("u"))))).BareAlias.ShouldBe("u");

    [Fact]
    public void BareAlias_RootOverAJoin_IsNull() =>
        Find(Over("Sort", Join("Inner", Scan("a"), Scan("b")))).BareAlias.ShouldBeNull();

    // A node of two children, an InitPlan beside the one input, is no chain.
    [Fact]
    public void BareAlias_NodeWithAnInitPlan_IsNull()
    {
        var sort = Over("Sort", Scan("u"));
        var plan = sort with { Plans = [Scan("i") with { ParentRelationship = "InitPlan" }, .. sort.Plans] };

        Find(plan).BareAlias.ShouldBeNull();
    }

    // An INSERT has a Result under it and no scan of its target.
    [Fact]
    public void Find_RootModifyTableWithoutAScanOfItsTarget_NamesItsTarget()
    {
        var plan = Over("ModifyTable", Scan("x") with { NodeType = "Result", Alias = null }) with { Alias = "orders" };

        ExtendedIn(Find(plan), "orders").ShouldBeFalse();
    }

    [Fact]
    public void Find_TargetsAliasScannedTwiceUnderTheModifyTable_IsUnusable()
    {
        var plan = Over("ModifyTable", Join("Inner", Scan("t"), Scan("t"))) with { Alias = "t" };

        Find(plan).Reason.ShouldBe(WalkReasons.DoubledAlias);
    }

    // Only the root's ModifyTable is a target: one further down is no scan of a relation.
    [Fact]
    public void Find_ModifyTableThatIsNotTheRoot_NamesNoInstance()
    {
        var plan = Over("Result", Over("ModifyTable", Scan("t")) with { Alias = "t" });

        var instances = Find(plan);

        instances.Reason.ShouldBeNull();
        ExtendedIn(instances, "t").ShouldBeFalse();
        instances.BareAlias.ShouldBeNull();
    }

    // What the log says of each node, in the order of the visit.
    [Fact]
    public void Find_Plan_KeepsEachNodeItVisitedWithItsDepthAndWhatItExtends()
    {
        var plan = Over("Sort", Join("Left", Scan("u"), Join("Full", Scan("o"), Scan("i"))));

        var nodes = Find(plan).Nodes;

        nodes
            .Select(static visited =>
                $"{visited.Depth} {visited.Node.NodeType} {visited.Node.Alias} {visited.NullExtends}"
            )
            .ShouldBe([
                "0 Sort  None",
                "1 Hash Join  Inner",
                "2 Seq Scan u None",
                "2 Hash Join  Both",
                "3 Seq Scan o None",
                "3 Seq Scan i None",
            ]);
    }

    [Fact]
    public void Find_RightJoin_SaysItExtendsItsOuterSide() =>
        Find(Join("Right", Scan("a"), Scan("b"))).Nodes[0].NullExtends.ShouldBe(NullExtension.Outer);

    [Fact]
    public void Find_AntiJoin_SaysItExtendsItsInnerSide() =>
        Find(Join("Anti", Scan("a"), Scan("b"))).Nodes[0].NullExtends.ShouldBe(NullExtension.Inner);

    // A set operation prints its first branch at the root: an Append under a UNION's Aggregate or under a Sort, and a
    // SetOp over an Append on 14 or over its two branches as Outer and Inner on 18.
    [Fact]
    public void HasSetOperation_AppendOrSetOpUnderTheRoot_IsTrue()
    {
        var append = Over("Append", Scan("a"), "Member") with
        {
            Plans =
            [
                Scan("a") with
                {
                    ParentRelationship = "Member",
                },
                Scan("b") with
                {
                    ParentRelationship = "Member",
                },
            ],
        };
        var setOp = Join("Inner", Scan("a"), Scan("b")) with { NodeType = "SetOp", JoinType = null };

        Find(Over("Aggregate", append)).HasSetOperation.ShouldBeTrue();
        Find(Over("Sort", append)).HasSetOperation.ShouldBeTrue();
        Find(setOp).HasSetOperation.ShouldBeTrue();
        Find(Over("Recursive Union", Scan("a"))).HasSetOperation.ShouldBeTrue();
        Find(Over("Merge Append", Scan("a"))).HasSetOperation.ShouldBeTrue();
    }

    [Fact]
    public void HasSetOperation_JoinOfTwoScans_IsFalse() =>
        Find(Join("Inner", Scan("a"), Scan("b"))).HasSetOperation.ShouldBeFalse();

    [Fact]
    public void IsExtended_NodeUnderTheSideAJoinExtends_IsTrue()
    {
        var plan = Join("Left", Scan("a"), Over("Hash", Scan("b"), Inner));

        var instances = Find(plan);

        instances.IsExtended(plan).ShouldBeFalse();
        instances.IsExtended(plan.Plans[0]).ShouldBeFalse();
        instances.IsExtended(plan.Plans[1]).ShouldBeTrue();
        instances.IsExtended(plan.Plans[1].Plans[0]).ShouldBeTrue();
    }

    [Fact]
    public void ExtendsUnder_JoinThatExtendsUnderTheNode_IsTrue()
    {
        PlanInstances.ExtendsUnder(Over("Sort", Join("Left", Scan("a"), Scan("b")))).ShouldBeTrue();
        PlanInstances.ExtendsUnder(Over("Sort", Join("Inner", Scan("a"), Scan("b")))).ShouldBeFalse();
    }

    // An instance is under the child its scan is reached from through Outer and Inner children, the child itself
    // included, and an alias of no instance is under none.
    [Fact]
    public void IsUnder_InstanceOfASide_IsUnderThatChild()
    {
        var initPlan = Scan("c") with { ParentRelationship = "InitPlan", SubplanName = "InitPlan 1" };
        var outer = Over("Sort", Scan("a")) with { Plans = [Scan("a") with { ParentRelationship = Outer }, initPlan] };
        var plan = Join("Full", outer, Over("Hash", Scan("r"), Inner));
        var instances = Find(plan);

        instances.IsUnder("a", plan.Plans[0]).ShouldBeTrue();
        instances.IsUnder("a", plan.Plans[1]).ShouldBeFalse();
        instances.IsUnder("r", plan.Plans[1]).ShouldBeTrue();
        instances.IsUnder("r", plan.Plans[1].Plans[0]).ShouldBeTrue();
        instances.IsUnder("r", plan.Plans[0]).ShouldBeFalse();
        instances.IsUnder("a", plan).ShouldBeTrue();
        instances.IsUnder("c", plan.Plans[0]).ShouldBeFalse();
        instances.IsUnder("x", plan).ShouldBeFalse();
    }

    // The Append of a partitioned table's partitions is one instance of the table under the alias the nodes above
    // print, with the tables every partition is a partition of, the relations its members scan, and no set operation.
    [Fact]
    public void Find_AppendOfPartitionsOfOneTable_IsAPartitionedInstance()
    {
        var plan = Join(
            "Left",
            Scan("u"),
            Append(Member("p_1", "parts_1", "p_1.id", "p_1.kind"), Member("p_2", "parts_2", "p_2.id", "p_2.kind"))
        );

        var instances = WithPartitions(plan);

        instances.HasSetOperation.ShouldBeFalse();
        instances.TryFind("p", out var partitioned).ShouldBeTrue();
        (partitioned.Kind, partitioned.NullExtended).ShouldBe((InstanceKind.Partitioned, true));
        partitioned.Parents.ShouldBe([16800, 16801]);
        partitioned.Members.ShouldBe([
            new PostgresRelationName("public", "parts_1"),
            new PostgresRelationName("public", "parts_2"),
        ]);
        instances.TryFind("p_1", out _).ShouldBeFalse();
    }

    // Members that scan no column print no Output, which is the same for each.
    [Fact]
    public void Find_AppendOfPartitionsWithoutOutput_IsAPartitionedInstance() =>
        WithPartitions(Over("Aggregate", Append(Member("p_1", "parts_1"), Member("p_2", "parts_2"))))
            .TryFind("p", out _)
            .ShouldBeTrue();

    // A UNION ALL of two partitions whose branches print other columns is a set operation, whose root prints the
    // first branch; so is an Append whose members are of no one table, or that "partitions" was not asked of.
    [Fact]
    public void Find_AppendThatIsNotOneTablesPartitions_IsASetOperation()
    {
        PlanNode[] plans =
        [
            Append(Member("p_1", "parts_1", "p_1.id", "p_1.kind"), Member("p_2", "parts_2", "p_2.id", "p_2.id")),
            Append(Member("p_1", "parts_1", "p_1.id"), Member("p_2", "other", "p_2.id")),
            Append(Member("p_1", "parts_1", "p_1.id"), Member("p_2", "plain", "p_2.id")),
            Append(Member("p_1", "parts_1", "p_1.id"), Member("p_2", "missing", "p_2.id")),
            Append(Member("p_1", "parts_1", "p_1.id"), Member("q_2", "parts_2", "q_2.id")),
        ];

        foreach (var plan in plans)
        {
            var instances = WithPartitions(Over("Sort", plan));
            instances.HasSetOperation.ShouldBeTrue();
            instances.TryFind("p", out _).ShouldBeFalse();
        }

        Find(Append(Member("p_1", "parts_1"), Member("p_2", "parts_2"))).HasSetOperation.ShouldBeTrue();
    }
}
