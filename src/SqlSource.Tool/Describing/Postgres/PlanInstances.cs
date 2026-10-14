using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>Why a walk had no plan it could use, as the <c>reason</c> of <c>postgres.walk</c> says it.</summary>
internal static class WalkReasons
{
    public const string ExplainFailed = "explain-failed";
    public const string Unreadable = "unreadable";
    public const string NoOutput = "no-output";
    public const string UnknownJoinType = "unknown-join-type";
    public const string DoubledAlias = "doubled-alias";
    public const string GroupingSets = "grouping-sets";
}

/// <summary>A node the walk visited, as the log says it.</summary>
/// <param name="Depth">How far under the root, which is at zero.</param>
/// <param name="Node">The node.</param>
/// <param name="NullExtends">
/// Which side the node null-extends: <c>inner</c>, <c>outer</c>, <c>both</c>, or <c>none</c> for a node that is no
/// join or a join that extends nothing.
/// </param>
internal sealed record VisitedNode(int Depth, PlanNode Node, string NullExtends);

/// <summary>
/// The relation instances of a plan, each by its alias, and whether a join on the path from the root to it
/// null-extends its side: the first two steps of the walk.
/// </summary>
/// <remarks>
/// <para>
/// Only a child whose <c>Parent Relationship</c> is <c>Outer</c> or <c>Inner</c> is followed.  An <c>InitPlan</c>,
/// a <c>SubPlan</c>, a <c>Member</c> and a <c>Subquery</c> are other plans: a CTE's plan hangs under whichever node
/// first needs it, and its scans are no instances of this plan's joins.
/// </para>
/// <para>
/// A <c>ModifyTable</c> at the root names its target, which a <c>RETURNING</c> list refers to by that alias.  An
/// <c>UPDATE</c> and a <c>DELETE</c> scan the target under it with the same alias: that scan is the target itself
/// and no second instance, and no join above it extends the target, since the row returned is the one the
/// statement acted on.
/// </para>
/// </remarks>
internal sealed class PlanInstances
{
    private const string Outer = "Outer";
    private const string Inner = "Inner";
    private const string Both = "both";
    private const string None = "none";
    private const string ModifyTable = "ModifyTable";

    private static readonly HashSet<string> RelationScans =
    [
        with(StringComparer.Ordinal),
        "Seq Scan",
        "Index Scan",
        "Index Only Scan",
        "Bitmap Heap Scan",
        "Tid Scan",
        "Tid Range Scan",
        "Sample Scan",
    ];

    private readonly Dictionary<string, bool> _extended = [with(StringComparer.Ordinal)];
    private readonly List<VisitedNode> _nodes = [];
    private readonly string? _target;
    private bool _targetScanSeen;

    private PlanInstances(PlanNode root)
    {
        _target = root.NodeType == ModifyTable ? root.Alias : null;
        Visit(root, 0, extended: false);
        RootNamesAnInstance =
            NamesAnInstance(root, isRoot: true)
            && (root.NodeType != ModifyTable || _nodes.TrueForAll(static visited => visited.Node.JoinType is null));
    }

    /// <summary>
    /// Why the plan cannot be used, or null: a join type the walk does not know, an alias found twice, or a
    /// grouping set, which puts <c>NULL</c> into a grouping column with no join.
    /// </summary>
    public string? Reason { get; private set; }

    /// <summary>Every node that was visited, in the order of the visit.</summary>
    public IReadOnlyList<VisitedNode> Nodes => _nodes;

    /// <summary>
    /// Whether a bare name in the root's <c>Output</c> is a column of the root's relation, which is when the root is
    /// a scan of a relation, or a <c>ModifyTable</c> with no join under it.  A <c>RETURNING</c> list prints a bare
    /// name for a relation the planner proved empty and replaced with a <c>Result</c> too, and under a join that
    /// name may be a column a join null-extends.
    /// </summary>
    public bool RootNamesAnInstance { get; }

    /// <summary>The instances of a plan.</summary>
    public static PlanInstances Find(PlanNode root) => new(root);

    /// <summary>Whether the alias names an instance, and whether a join null-extends it.</summary>
    public bool TryFind(string alias, out bool nullExtended) => _extended.TryGetValue(alias, out nullExtended);

    private static bool NamesAnInstance(PlanNode node, bool isRoot) =>
        node.Alias is not null && (RelationScans.Contains(node.NodeType) || (isRoot && node.NodeType == ModifyTable));

    // Which side a join extends, "none" for a node that is no join, and null for a join type that is not known.  An
    // anti join extends too: the planner makes one of a LEFT JOIN b ... WHERE b.x IS NULL and returns b's columns NULL.
    private static string? ExtendsOf(PlanNode node) =>
        node.JoinType switch
        {
            null => None,
            "Left" or "Anti" => "inner",
            "Right" or "Right Anti" => "outer",
            "Full" => Both,
            "Inner" or "Semi" or "Right Semi" => None,
            _ => null,
        };

    private void Visit(PlanNode node, int depth, bool extended)
    {
        var extends = ExtendsOf(node);
        _nodes.Add(new VisitedNode(depth, node, extends ?? None));
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

        if (node.Alias is { } alias && NamesAnInstance(node, depth == 0))
        {
            Add(alias, depth, extended);
        }

        foreach (var child in node.Plans.Where(static child => child.ParentRelationship is Outer or Inner))
        {
            var side = child.ParentRelationship == Inner ? "inner" : "outer";
            Visit(child, depth + 1, extended || extends == Both || extends == side);
        }
    }

    private void Add(string alias, int depth, bool extended)
    {
        // The first scan of the target's alias under the ModifyTable is the target, which the root already named.
        if (depth > 0 && !_targetScanSeen && string.Equals(alias, _target, StringComparison.Ordinal))
        {
            _targetScanSeen = true;
            return;
        }

        if (!_extended.TryAdd(alias, extended))
        {
            Reason ??= WalkReasons.DoubledAlias;
        }
    }
}
