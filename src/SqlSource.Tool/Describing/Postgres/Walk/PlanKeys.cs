using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>The keys of a node of <c>EXPLAIN (VERBOSE, FORMAT JSON)</c> that the walk reads.</summary>
internal static class PlanKeys
{
    public const string Plan = "Plan";
    public const string Plans = "Plans";
    public const string NodeType = "Node Type";
    public const string JoinType = "Join Type";
    public const string ParentRelationship = "Parent Relationship";
    public const string Output = "Output";
    public const string Alias = "Alias";
    public const string RelationName = "Relation Name";
    public const string Schema = "Schema";
    public const string SubplanName = "Subplan Name";
    public const string CteName = "CTE Name";
    public const string Command = "Command";
    public const string Strategy = "Strategy";
    public const string SubplansRemoved = "Subplans Removed";
    public const string GroupingSets = "Grouping Sets";
    public const string GroupKey = "Group Key";
    public const string Filter = "Filter";

    /// <summary>Every key above.</summary>
    public static IReadOnlyList<string> All { get; } =
    [
        Plan,
        Plans,
        NodeType,
        JoinType,
        ParentRelationship,
        Output,
        Alias,
        RelationName,
        Schema,
        SubplanName,
        CteName,
        Command,
        Strategy,
        SubplansRemoved,
        GroupingSets,
        GroupKey,
        Filter,
    ];
}
