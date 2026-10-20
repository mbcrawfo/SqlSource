using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// A node of a plan as <c>EXPLAIN (VERBOSE, FORMAT JSON)</c> writes it: the keys the walk reads, and no other.
/// </summary>
/// <param name="NodeType"><c>Node Type</c>: <c>Hash Join</c>, <c>Seq Scan</c>.</param>
/// <param name="JoinType"><c>Join Type</c>, which a join alone has: <c>Left</c>, <c>Right Anti</c>.</param>
/// <param name="ParentRelationship">
/// <c>Parent Relationship</c>: <c>Outer</c> and <c>Inner</c> for the sides of a join and the one input of any
/// other node; <c>InitPlan</c>, <c>SubPlan</c>, <c>Member</c> and <c>Subquery</c> for another plan.  Null on the
/// root.
/// </param>
/// <param name="Output">
/// <c>Output</c>: the node's target list as text.  On the root it is the result's, in order, with what a sort or a
/// group added after it.
/// </param>
/// <param name="Alias"><c>Alias</c>, as the name is and not as it is printed: <c>Order</c> for <c>"Order"</c>.</param>
/// <param name="RelationName"><c>Relation Name</c>.</param>
/// <param name="Schema"><c>Schema</c>.</param>
/// <param name="SubplanName"><c>Subplan Name</c>.</param>
/// <param name="CteName"><c>CTE Name</c>.</param>
/// <param name="Command">
/// <c>Command</c>, which a <c>SetOp</c> alone has: <c>Intersect</c>, <c>Intersect All</c>, <c>Except</c>,
/// <c>Except All</c>.
/// </param>
/// <param name="HasGroupingSets">Whether the node has a <c>Grouping Sets</c> key, whatever it holds.</param>
/// <param name="Strategy">
/// <c>Strategy</c>, which an <c>Aggregate</c> has, <c>Plain</c>, <c>Sorted</c>, <c>Hashed</c> or <c>Mixed</c>, and a
/// <c>SetOp</c> too.
/// </param>
/// <param name="HasGroupKey">Whether the node has a <c>Group Key</c> that names a key at least.</param>
/// <param name="HasFilter">
/// Whether the node has a <c>Filter</c>, whatever it holds: an aggregate's <c>HAVING</c>.
/// </param>
/// <param name="SubplansRemoved">
/// <c>Subplans Removed</c>, which an <c>Append</c> or a <c>Merge Append</c> prints when partition pruning removed
/// members from the plan: their number, and 0 when the key is absent.  Such a node does not show every partition.
/// </param>
/// <param name="Plans"><c>Plans</c>: the children, empty when the node has none.</param>
internal sealed record PlanNode(
    string NodeType,
    string? JoinType,
    string? ParentRelationship,
    IReadOnlyList<string>? Output,
    string? Alias,
    string? RelationName,
    string? Schema,
    string? SubplanName,
    string? CteName,
    string? Command,
    bool HasGroupingSets,
    string? Strategy,
    bool HasGroupKey,
    bool HasFilter,
    int SubplansRemoved,
    IReadOnlyList<PlanNode> Plans
)
{
    // Each node is two levels of the text, and the reader's own limit of 64 would refuse a plan of thirty.
    private const int MaxDepth = 512;

    /// <summary>
    /// The root of the plan a <c>Subquery Scan</c> scans, its one child whose <c>Parent Relationship</c> is
    /// <c>Subquery</c>; null for any other node, and for a scan with none or with several.
    /// </summary>
    /// <remarks>Read each time and never kept, so that a copy made with <c>with</c> has its own.</remarks>
    public PlanNode? SubqueryChild
    {
        get
        {
            if (NodeType != PlanNodeTypes.SubqueryScan)
            {
                return null;
            }

            PlanNode? only = null;
            foreach (var child in Plans)
            {
                if (child.ParentRelationship != PlanRelationships.Subquery)
                {
                    continue;
                }

                if (only is not null)
                {
                    return null;
                }

                only = child;
            }

            return only;
        }
    }

    /// <summary>
    /// The children whose <c>Parent Relationship</c> is <c>Outer</c> or <c>Inner</c>, in order: the sides of a join,
    /// or the one input of any other node.
    /// </summary>
    public IEnumerable<PlanNode> JoinChildren =>
        Plans.Where(static child => child.ParentRelationship is PlanRelationships.Outer or PlanRelationships.Inner);

    /// <summary>
    /// The node and every node under it, through every <c>Parent Relationship</c>: the node first, then each child
    /// and all under it before the next child.
    /// </summary>
    public IEnumerable<PlanNode> SelfAndDescendants() =>
        Plans.SelectMany(static child => child.SelfAndDescendants()).Prepend(this);

    /// <summary>
    /// The one node of those at and under this one, through every <c>Parent Relationship</c>, that hangs here by the
    /// relationship under the <c>Subplan Name</c>; null when none does, or when several do, which says nothing.
    /// </summary>
    /// <param name="relationship"><c>InitPlan</c> or <c>SubPlan</c>.</param>
    /// <param name="subplanName">The name: <c>SubPlan 1</c>, or <c>CTE</c> and a CTE's name.</param>
    public PlanNode? OnlyPlanNamed(string relationship, string subplanName) =>
        SelfAndDescendants()
            .Where(node =>
                node.ParentRelationship == relationship
                && string.Equals(node.SubplanName, subplanName, StringComparison.Ordinal)
            )
            .Take(2)
            .ToArray()
            is [var only]
            ? only
            : null;

    /// <summary>
    /// The root of a plan's text, or null: the text is an array of one object whose <c>Plan</c> is the root, and
    /// anything else is a plan that cannot be used.  A key the walk does not read is ignored.
    /// </summary>
    public static PlanNode? Read(string text)
    {
        try
        {
            using var document = JsonDocument.Parse(text, new JsonDocumentOptions { MaxDepth = MaxDepth });
            var root = document.RootElement;
            return
                root.ValueKind == JsonValueKind.Array
                && root.GetArrayLength() == 1
                && root[0].ValueKind == JsonValueKind.Object
                && root[0].TryGetProperty("Plan", out var plan)
                ? NodeOf(plan)
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static PlanNode? NodeOf(JsonElement element)
    {
        if (
            element.ValueKind != JsonValueKind.Object
            || !TryText(element, "Node Type", out var nodeType)
            || nodeType is null
            || !TryText(element, "Join Type", out var joinType)
            || !TryText(element, "Parent Relationship", out var relationship)
            || !TryTexts(element, "Output", out var output)
            || !TryText(element, "Alias", out var alias)
            || !TryText(element, "Relation Name", out var relation)
            || !TryText(element, "Schema", out var schema)
            || !TryText(element, "Subplan Name", out var subplan)
            || !TryText(element, "CTE Name", out var cte)
            || !TryText(element, nameof(Command), out var command)
            || !TryText(element, nameof(Strategy), out var strategy)
            || !TryCount(element, "Subplans Removed", out var removed)
            || !TryNodes(element, out var plans)
        )
        {
            return null;
        }

        return new PlanNode(
            nodeType,
            joinType,
            relationship,
            output,
            alias,
            relation,
            schema,
            subplan,
            cte,
            command,
            element.TryGetProperty("Grouping Sets", out _),
            strategy,
            element.TryGetProperty("Group Key", out var key)
                && key.ValueKind == JsonValueKind.Array
                && key.GetArrayLength() > 0,
            element.TryGetProperty("Filter", out _),
            removed,
            plans
        );
    }

    // False when the key holds what is no string.  A key that is absent is null.
    private static bool TryText(JsonElement element, string key, out string? text)
    {
        text = null;
        if (!element.TryGetProperty(key, out var value))
        {
            return true;
        }

        text = value.ValueKind == JsonValueKind.String ? value.GetString() : null;
        return text is not null;
    }

    // False when the key holds what is no integer of zero or more.  A key that is absent is 0.
    private static bool TryCount(JsonElement element, string key, out int count)
    {
        count = 0;
        if (!element.TryGetProperty(key, out var value))
        {
            return true;
        }

        return value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out count) && count >= 0;
    }

    // False when the key holds what is no array of strings.  A key that is absent is null.
    private static bool TryTexts(JsonElement element, string key, out IReadOnlyList<string>? texts)
    {
        texts = null;
        if (!element.TryGetProperty(key, out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var read = new List<string>(value.GetArrayLength());
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String || item.GetString() is not { } text)
            {
                return false;
            }

            read.Add(text);
        }

        texts = read;
        return true;
    }

    // False when "Plans" holds what is no array of nodes.  A node without the key has no child.
    private static bool TryNodes(JsonElement element, out IReadOnlyList<PlanNode> nodes)
    {
        nodes = [];
        if (!element.TryGetProperty(nameof(Plans), out var value))
        {
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        var read = new List<PlanNode>(value.GetArrayLength());
        foreach (var item in value.EnumerateArray())
        {
            if (NodeOf(item) is not { } node)
            {
                return false;
            }

            read.Add(node);
        }

        nodes = read;
        return true;
    }
}
