using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.Postgres.Walk;
using Xunit;
using static SqlSource.Tool.Tests.PostgresPlans;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk;

// The instances of a walk's plans, found once for each root, with the answer of "partitions" the walk was given.
public sealed class PlanInstancesCacheTests
{
    private static readonly Dictionary<PostgresRelationName, IReadOnlyList<long>> Partitions = new()
    {
        [new PostgresRelationName("public", "parts_1")] = [16800],
        [new PostgresRelationName("public", "parts_2")] = [16800],
    };

    private static PlanNode PartsAppend() => Append(Member("p_1", "parts_1"), Member("p_2", "parts_2"));

    [Fact]
    public void Of_SameRootTwice_IsTheSameInstances()
    {
        var plan = Join("Left", Scan("a"), Over("Subquery Scan", Scan("b"), "Subquery") with { Alias = "s" });
        var cache = new PlanInstancesCache(plan, null);

        cache.Of(plan).ShouldBeSameAs(cache.Of(plan));
        cache.Of(plan.Plans[1]).ShouldBeSameAs(cache.Of(plan.Plans[1]));
        cache.Of(plan.Plans[1]).ShouldNotBeSameAs(cache.Of(plan));
    }

    // Two nodes equal by value are two plans, each found by its own node.
    [Fact]
    public void Of_RootsEqualByValue_AreFoundApart()
    {
        var sort = Over("Sort", Scan("a"));
        var copy = sort with { };
        var plan = Over("Append", sort) with { Plans = [sort, copy] };
        var cache = new PlanInstancesCache(plan, null);

        copy.ShouldBe(sort);
        cache.Of(copy).ShouldNotBeSameAs(cache.Of(sort));
    }

    [Fact]
    public void Of_PartitionsNotAsked_ReadsAnAppendAsASetOperation() =>
        new PlanInstancesCache(PartsAppend(), null).Of(PartsAppend()).HasSetOperation.ShouldBeTrue();

    [Fact]
    public void Of_PartitionsAnswered_ReadsAnAppendOfPartitionsAsTheirTable()
    {
        var plan = PartsAppend();
        var instances = new PlanInstancesCache(plan, Partitions).Of(plan);

        instances.HasSetOperation.ShouldBeFalse();
        instances.TryFind("p", out var parts).ShouldBeTrue();
        parts.Kind.ShouldBe(InstanceKind.Partitioned);
    }

    // A plan inside the tree finds a CTE's InitPlan anywhere in the whole tree.
    [Fact]
    public void Of_PlanInsideTheTree_LooksForACtesInitPlanInTheWholeTree()
    {
        var cte = Scan("c") with { ParentRelationship = "InitPlan", SubplanName = "CTE w" };
        var scan = Scan("x") with { NodeType = "CTE Scan", Alias = "w", CteName = "w", RelationName = null };
        var inner = Over("Sort", scan);
        var plan = Over("Limit", inner) with { Plans = [cte, inner with { ParentRelationship = Outer }] };
        var cache = new PlanInstancesCache(plan, null);

        cache.Of(plan.Plans[1]).TryFind("w", out var found).ShouldBeTrue();
        found.Child.ShouldBeSameAs(cte);
    }
}
