using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// The relations a plan scans: what <c>relations</c> is asked, for the walk to read the <c>NOT NULL</c> and the type
/// of a table that a set operation's branch scans.
/// </summary>
internal static class PlanRelations
{
    /// <summary>
    /// The schema and the name of every relation scan in the whole tree, through every <c>Parent Relationship</c>
    /// (<c>Outer</c>, <c>Inner</c>, <c>Member</c>, <c>Subquery</c>, <c>InitPlan</c> and <c>SubPlan</c>), in schema
    /// and name order, each once: empty when no scan prints both.
    /// </summary>
    public static IReadOnlyList<PostgresRelationName> Of(PlanNode root) =>
        [
            .. All(root)
                .Where(static node =>
                    PlanInstances.ScansARelation(node) && node.Schema is not null && node.RelationName is not null
                )
                .Select(static node => new PostgresRelationName(node.Schema!, node.RelationName!))
                .Distinct()
                .OrderBy(static relation => relation.Schema, StringComparer.Ordinal)
                .ThenBy(static relation => relation.Name, StringComparer.Ordinal),
        ];

    private static IEnumerable<PlanNode> All(PlanNode node) => node.Plans.SelectMany(All).Prepend(node);
}
