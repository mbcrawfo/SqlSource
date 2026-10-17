using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// The <c>reason</c> of <c>postgres.walk</c>: why a walk had no plan it could use, or, as <c>output-differs</c> on a
/// <c>walked</c> plan, why its expressions were not read.
/// </summary>
internal static class WalkReasons
{
    public const string ExplainFailed = "explain-failed";
    public const string Unreadable = "unreadable";
    public const string NoOutput = "no-output";
    public const string UnknownJoinType = "unknown-join-type";
    public const string DoubledAlias = "doubled-alias";
    public const string GroupingSets = "grouping-sets";
    public const string OutputDiffers = "output-differs";
}

/// <summary>Which side of a join is null-extended.</summary>
internal enum NullExtension
{
    /// <summary>Neither: a node that is no join, or a join that extends nothing.</summary>
    None,

    /// <summary>The <c>Inner</c> child.</summary>
    Inner,

    /// <summary>The <c>Outer</c> child.</summary>
    Outer,

    /// <summary>Both children.</summary>
    Both,
}

/// <summary>A node the walk visited, as the log says it.</summary>
/// <param name="Depth">How far under the root, which is at zero.</param>
/// <param name="Node">The node.</param>
/// <param name="NullExtends">Which side the node null-extends.</param>
internal sealed record VisitedNode(int Depth, PlanNode Node, NullExtension NullExtends);

/// <summary>What an alias of a plan names.</summary>
internal enum InstanceKind
{
    /// <summary>A scan of one relation, or the target of a <c>ModifyTable</c> at the root.</summary>
    Relation,

    /// <summary>A partitioned table, which scans as an <c>Append</c> of its partitions.</summary>
    Partitioned,

    /// <summary>A scan of another plan: a <c>Subquery Scan</c> or a <c>CTE Scan</c>.</summary>
    Plan,
}

/// <summary>A relation instance of a plan, by its kind.</summary>
/// <param name="Kind">What the alias names.</param>
/// <param name="NullExtended">Whether a join on the path from the root to it null-extends its side.</param>
/// <param name="Schema">The schema of a <see cref="InstanceKind.Relation" />'s relation, when the plan says it.</param>
/// <param name="Relation">The name of a <see cref="InstanceKind.Relation" />'s relation, when the plan says it.</param>
/// <param name="Parents">
/// The tables every partition of a <see cref="InstanceKind.Partitioned" /> instance is a partition of, by OID, nearest
/// first; empty for the other kinds.
/// </param>
/// <param name="Child">
/// The root of the plan a <see cref="InstanceKind.Plan" /> instance scans, or null for the other kinds and for a
/// scan whose plan cannot be told.
/// </param>
internal sealed record PlanInstance(
    InstanceKind Kind,
    bool NullExtended,
    string? Schema,
    string? Relation,
    IReadOnlyList<long> Parents,
    PlanNode? Child
);

/// <summary>
/// The relation instances of a plan, each by its alias, and whether a join on the path from the root to it
/// null-extends its side.
/// </summary>
/// <remarks>
/// <para>
/// Only a child whose <c>Parent Relationship</c> is <c>Outer</c> or <c>Inner</c> is followed.  An <c>InitPlan</c>,
/// a <c>SubPlan</c>, a <c>Member</c> and a <c>Subquery</c> are other plans: a CTE's plan hangs under whichever node
/// first needs it, and its scans are no instances of this plan's joins.
/// </para>
/// <para>
/// An instance is of one of three kinds.  A scan of a relation is one, and so is a <c>ModifyTable</c> at the root,
/// which names its target; an <c>UPDATE</c> and a <c>DELETE</c> scan the target under it with the same alias, and
/// that scan is the target itself and no second instance, and no join above it extends the target, since the row
/// returned is the one the statement acted on.  An <c>Append</c> whose members are the partitions of one table
/// (<see cref="PlanPartitions" />) is an instance of that table under the alias the nodes above print, and no set
/// operation.  A <c>Subquery Scan</c> and a <c>CTE Scan</c> are instances of another plan, which
/// <see cref="NullabilityWalk" /> walks as a plan of its own: the <c>Subquery</c> child of the one, and for the other
/// the one <c>InitPlan</c> of the whole tree named <c>CTE</c> and the CTE's name.
/// </para>
/// </remarks>
internal sealed class PlanInstances
{
    // A CTE's InitPlan is named for it: "CTE" and the CTE's name.
    private const string CtePlanPrefix = "CTE ";

    private static readonly IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>> NoPartitions =
        new Dictionary<PostgresRelationName, IReadOnlyList<long>>();

    // The nodes of a set operation, whose root prints its first branch: a UNION's Append, ordered or not, the SetOp of
    // an INTERSECT or an EXCEPT, and a recursive CTE's union.
    private static readonly HashSet<string> SetOperations =
    [
        with(StringComparer.Ordinal),
        PlanNodeTypes.Append,
        PlanNodeTypes.MergeAppend,
        PlanNodeTypes.SetOp,
        PlanNodeTypes.RecursiveUnion,
    ];

    private static readonly HashSet<string> RelationScans =
    [
        with(StringComparer.Ordinal),
        PlanNodeTypes.SeqScan,
        PlanNodeTypes.IndexScan,
        PlanNodeTypes.IndexOnlyScan,
        PlanNodeTypes.BitmapHeapScan,
        PlanNodeTypes.TidScan,
        PlanNodeTypes.TidRangeScan,
        PlanNodeTypes.SampleScan,
        PlanNodeTypes.ForeignScan,
    ];

    private readonly Dictionary<string, PlanInstance> _instances = [with(StringComparer.Ordinal)];
    private readonly List<VisitedNode> _nodes = [];
    private readonly Dictionary<PlanNode, bool> _extended = [with(ReferenceEqualityComparer.Instance)];
    private readonly PlanNode _tree;
    private readonly IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>> _partitions;
    private readonly string? _target;
    private bool _targetScanSeen;

    private PlanInstances(
        PlanNode root,
        PlanNode tree,
        IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>> partitions
    )
    {
        _tree = tree;
        _partitions = partitions;
        _target = root.NodeType == PlanNodeTypes.ModifyTable ? root.Alias : null;
        Visit(root, 0, extended: false);
        BareAlias = BareAliasOf(root);
    }

    /// <summary>
    /// Why the plan cannot be used, or null: a join type the walk does not know, an alias found twice, or a
    /// grouping set, which puts <c>NULL</c> into a grouping column with no join.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>Every node that was visited, in the order of the visit.</summary>
    public IReadOnlyList<VisitedNode> Nodes => _nodes;

    /// <summary>
    /// The alias of the relation a bare name in the root's <c>Output</c> is a column of, or null.  The root prints
    /// bare names when the plan has one relation: the root's own when it is a scan of a relation; the one relation
    /// scan it reaches through nodes of one <c>Outer</c> child each, a <c>Limit</c> over a <c>Sort</c> over a scan;
    /// and the target of a <c>ModifyTable</c>, unless a join under it null-extends: a <c>RETURNING</c> list prints a
    /// bare name for a relation the planner proved empty and replaced with a <c>Result</c> too, and under such a
    /// join that name may be a column the join null-extends.
    /// </summary>
    public string? BareAlias { get; }

    /// <summary>
    /// Whether a node that was visited is of a set operation, an <c>Append</c> or a <c>SetOp</c> for two, whose root
    /// prints the first branch's values: a value there says nothing of the other branches.  The <c>Append</c> of a
    /// partitioned table that <see cref="PlanPartitions" /> recognises is none.
    /// </summary>
    public bool HasSetOperation { get; private set; }

    /// <summary>
    /// The instances of a whole plan, with no answer of <c>partitions</c>: an <c>Append</c> of a partitioned table's
    /// partitions is read as a set operation.
    /// </summary>
    public static PlanInstances Find(PlanNode root) => new(root, root, NoPartitions);

    /// <summary>The instances of a plan, or of a plan inside another.</summary>
    /// <param name="root">The root of the plan whose instances are found.</param>
    /// <param name="tree">The root of the whole plan, where the <c>InitPlan</c> of a CTE is looked for.</param>
    /// <param name="partitions">The answer of <c>partitions</c>, by relation; empty when it was not asked.</param>
    public static PlanInstances Find(
        PlanNode root,
        PlanNode tree,
        IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>> partitions
    ) => new(root, tree, partitions);

    /// <summary>Whether the node scans one relation, a foreign table's included.</summary>
    public static bool ScansARelation(PlanNode node) => RelationScans.Contains(node.NodeType);

    /// <summary>
    /// Whether a join at the node or under it, through <c>Outer</c> and <c>Inner</c> children, null-extends a side.
    /// </summary>
    public static bool ExtendsUnder(PlanNode node) =>
        ExtendsOf(node) != NullExtension.None
        || node.Plans.Any(static child =>
            child.ParentRelationship is PlanRelationships.Outer or PlanRelationships.Inner && ExtendsUnder(child)
        );

    /// <summary>Whether the alias names an instance, and which.</summary>
    public bool TryFind(string alias, [NotNullWhen(true)] out PlanInstance? instance) =>
        _instances.TryGetValue(alias, out instance);

    /// <summary>Whether a join on the path from the root null-extends a node that was visited.</summary>
    public bool IsExtended(PlanNode node) => _extended.TryGetValue(node, out var extended) && extended;

    private static bool NamesARelation(PlanNode node, bool isRoot) =>
        node.Alias is not null
        && (RelationScans.Contains(node.NodeType) || (isRoot && node.NodeType == PlanNodeTypes.ModifyTable));

    // Which side a join extends, None for a node that is no join, and null for a join type that is not known.  An
    // anti join extends too: the planner makes one of a LEFT JOIN b ... WHERE b.x IS NULL and returns b's columns NULL.
    private static NullExtension? ExtendsOf(PlanNode node) =>
        node.JoinType switch
        {
            null => NullExtension.None,
            PlanJoinTypes.Left or PlanJoinTypes.Anti => NullExtension.Inner,
            PlanJoinTypes.Right or PlanJoinTypes.RightAnti => NullExtension.Outer,
            PlanJoinTypes.Full => NullExtension.Both,
            PlanJoinTypes.Inner or PlanJoinTypes.Semi or PlanJoinTypes.RightSemi => NullExtension.None,
            _ => null,
        };

    private string? BareAliasOf(PlanNode root)
    {
        if (root.NodeType == PlanNodeTypes.ModifyTable)
        {
            return
                root.Alias is not null && _nodes.TrueForAll(static visited => visited.NullExtends == NullExtension.None)
                ? root.Alias
                : null;
        }

        var node = root;
        while (
            !RelationScans.Contains(node.NodeType)
            && node.NodeType != PlanNodeTypes.ModifyTable
            && node.Plans is [{ ParentRelationship: PlanRelationships.Outer } only]
        )
        {
            node = only;
        }

        return NamesARelation(node, isRoot: false) ? node.Alias : null;
    }

    private void Visit(PlanNode node, int depth, bool extended)
    {
        var extends = ExtendsOf(node);
        _nodes.Add(new VisitedNode(depth, node, extends ?? NullExtension.None));
        _extended[node] = extended;
        if (extends is null)
        {
            Reason ??= WalkReasons.UnknownJoinType;
        }

        // EXPLAIN (FORMAT JSON) writes every aggregate as "Aggregate", with its strategy apart, and one that mixes
        // hashing and sorting has a "Grouping Sets" key like any other: the key is the one thing to look for.
        if (node.HasGroupingSets)
        {
            Reason ??= WalkReasons.GroupingSets;
        }

        if (PartitionedOf(node) is { } partitioned)
        {
            Add(
                partitioned.Alias,
                depth,
                new PlanInstance(InstanceKind.Partitioned, extended, null, null, partitioned.Parents, null)
            );
        }
        else if (SetOperations.Contains(node.NodeType))
        {
            HasSetOperation = true;
        }
        else if (node.Alias is { } alias && NamesARelation(node, depth == 0))
        {
            Add(
                alias,
                depth,
                new PlanInstance(InstanceKind.Relation, extended, node.Schema, node.RelationName, [], null)
            );
        }
        else if (node.Alias is { } scan && node.NodeType is PlanNodeTypes.SubqueryScan or PlanNodeTypes.CteScan)
        {
            Add(scan, depth, new PlanInstance(InstanceKind.Plan, extended, null, null, [], ChildOf(node)));
        }

        foreach (
            var child in node.Plans.Where(static child =>
                child.ParentRelationship is PlanRelationships.Outer or PlanRelationships.Inner
            )
        )
        {
            var side = child.ParentRelationship == PlanRelationships.Inner ? NullExtension.Inner : NullExtension.Outer;
            Visit(child, depth + 1, extended || extends == NullExtension.Both || extends == side);
        }
    }

    // The alias and the parent tables of an Append of a partitioned table's partitions, or null.
    private (string Alias, IReadOnlyList<long> Parents)? PartitionedOf(PlanNode node) =>
        PlanPartitions.ShapeOf(node) is { } alias && PlanPartitions.ParentsOf(node, _partitions) is [_, ..] parents
            ? (alias, parents)
            : null;

    // The root of the plan a scan of another plan scans: a Subquery Scan's one Subquery child, and the one InitPlan of
    // the whole tree named for a CTE Scan's CTE.  A CTE's InitPlan hangs where its query level is, which may be
    // above the scan, so the whole tree is searched; a name found twice, a CTE of the same name at two levels, says
    // nothing.
    private PlanNode? ChildOf(PlanNode node)
    {
        if (node.NodeType == PlanNodeTypes.SubqueryScan)
        {
            return
                node.Plans.Where(static child => child.ParentRelationship == PlanRelationships.Subquery).ToArray()
                    is [var only]
                ? only
                : null;
        }

        var name = CtePlanPrefix + node.CteName;
        return
            node.CteName is not null
            && All(_tree)
                .Where(child =>
                    child.ParentRelationship == PlanRelationships.InitPlan
                    && string.Equals(child.SubplanName, name, StringComparison.Ordinal)
                )
                .Take(2)
                .ToArray()
                is [var plan]
            ? plan
            : null;
    }

    private void Add(string alias, int depth, PlanInstance instance)
    {
        // The first scan of the target's alias under the ModifyTable is the target, which the root already named.
        if (depth > 0 && !_targetScanSeen && string.Equals(alias, _target, StringComparison.Ordinal))
        {
            _targetScanSeen = true;
            return;
        }

        if (!_instances.TryAdd(alias, instance))
        {
            Reason ??= WalkReasons.DoubledAlias;
        }
    }

    private static IEnumerable<PlanNode> All(PlanNode node) => node.Plans.SelectMany(All).Prepend(node);
}
