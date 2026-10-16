using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SqlSource.Tool.Describing.Postgres;

namespace SqlSource.Tool.Tests;

// Plans as a server would answer "explain", written by a test: a scan of a table, a join of two nodes, and a node
// over one child.  Text gives the plan as EXPLAIN (VERBOSE, FORMAT JSON) writes it, for a test of the session.
internal static class PostgresPlans
{
    public const string Outer = "Outer";

    public const string Inner = "Inner";

    // A scan of the table of the alias's name in public, with the alias.
    public static PlanNode Scan(string alias, params string[] output) =>
        new(
            "Seq Scan",
            null,
            null,
            output.Length == 0 ? null : output,
            alias,
            alias,
            "public",
            null,
            null,
            null,
            false,
            []
        );

    // A join whose first child is its outer side and whose second is its inner one.
    public static PlanNode Join(string joinType, PlanNode outer, PlanNode inner) =>
        new(
            "Hash Join",
            joinType,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            [outer with { ParentRelationship = Outer }, inner with { ParentRelationship = Inner }]
        );

    // A node that is no join over one child: a Sort, a Hash, a ModifyTable, a Subquery Scan.
    public static PlanNode Over(string nodeType, PlanNode child, string relationship = Outer) =>
        new(
            nodeType,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            false,
            [child with { ParentRelationship = relationship }]
        );

    public static string Text(PlanNode root) =>
        JsonSerializer.Serialize(new[] { new Dictionary<string, object?> { ["Plan"] = MapOf(root) } });

    private static Dictionary<string, object?> MapOf(PlanNode node)
    {
        var map = new Dictionary<string, object?> { ["Node Type"] = node.NodeType };
        Add(map, "Join Type", node.JoinType);
        Add(map, "Parent Relationship", node.ParentRelationship);
        Add(map, "Relation Name", node.RelationName);
        Add(map, "Schema", node.Schema);
        Add(map, "Alias", node.Alias);
        Add(map, "Subplan Name", node.SubplanName);
        Add(map, "CTE Name", node.CteName);
        Add(map, "Command", node.Command);
        Add(map, "Output", node.Output);
        if (node.HasGroupingSets)
        {
            map["Grouping Sets"] = System.Array.Empty<object>();
        }

        if (node.Plans.Count > 0)
        {
            map["Plans"] = node.Plans.Select(MapOf).ToArray();
        }

        return map;
    }

    private static void Add(Dictionary<string, object?> map, string key, object? value)
    {
        if (value is not null)
        {
            map[key] = value;
        }
    }
}
