using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// The instances of each plan of one walk, found once each: of the whole plan, and of each plan inside it that the
/// walk reads as a plan of its own, a scan's or a set operation's input.
/// </summary>
/// <remarks>
/// Built from the tree and the answer of <c>partitions</c> alone, which are all <see cref="PlanInstances" /> reads,
/// so that it can be built before <c>relations</c> is asked, which
/// <see cref="NullabilityWalk.MayAnswerFromBranches" /> decides, and used again by <see cref="NullabilityWalk.Run" />.
/// </remarks>
/// <param name="tree">The root of the whole plan, where the <c>InitPlan</c> of a CTE is looked for.</param>
/// <param name="partitions">
/// The answer of <c>partitions</c>, by relation, or null when it was not asked: every <c>Append</c> is then read as a
/// set operation.
/// </param>
internal sealed class PlanInstancesCache(
    PlanNode tree,
    IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>>? partitions
)
{
    private readonly Dictionary<PlanNode, PlanInstances> _found = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>The root of the whole plan.</summary>
    public PlanNode Tree => tree;

    /// <summary>The instances of the plan whose root is the node, the whole plan's or one inside it.</summary>
    public PlanInstances Of(PlanNode root)
    {
        if (!_found.TryGetValue(root, out var found))
        {
            found = PlanInstances.Find(root, tree, partitions ?? PlanInstances.NoPartitions);
            _found[root] = found;
        }

        return found;
    }
}
