using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing.Postgres.Columns;

namespace SqlSource.Tool.Describing.Postgres.Walk;

internal static partial class NullabilityWalk
{
    // The entries of a plan read as expressions, with a reference inside one answered as the walk answers a column:
    // through an instance that no join extends, to a column of a relation that "tables" or "relations" answered, by the
    // catalog's rule.  A bare name is the plan's one relation's (PlanInstances.BareAlias): a plan of several prints
    // every name with its alias but the columns of a relation the planner proved empty, a Result under an outer join,
    // and at a ModifyTable that may return the old or the new row only the target's alias is a relation.  An entry is
    // read in a plan where no join null-extends a side, or where the walk follows it to the node that computes it and
    // no join null-extends that node: a node prints a value computed under it in one pair of parentheses, at every
    // node up to the root.  An aggregate is read only where the walk follows the entry to the node that computes it.  A
    // subquery's SubPlan is found by its name in the whole plan and read as a plan of its own, as deep as
    // Plans.MaxDepth allows.
    private sealed class Expressions(
        Plans plans,
        PlanNode root,
        PlanInstances instances,
        string? onlyAlias,
        int plansFollowed = 0
    )
    {
        private const int MaxDepth = 256;

        private readonly WalkScope _scope = plans.Scope;

        // The plan of each SubPlan this plan's entries named, by its name, read once; null for one that is not read.
        private readonly Dictionary<string, Expressions?> _subPlans = [with(StringComparer.Ordinal)];

        private readonly bool _nothingExtends = instances.Nodes.All(static visited =>
            visited.NullExtends == NullExtension.None
        );

        // The root of the plan the entries are of.
        public PlanNode Root => root;

        // What the entry of a column of the type, by the row description, says.  Where nothing extends, the entry is
        // read at the root as it is, and followed to the node that computes it only when that gives nothing: the
        // root's reading knows no node's groups, and answers no aggregate.
        public bool? NullableOf(string entry, long columnType)
        {
            var context = ContextOf(entry, columnType);
            return _nothingExtends
                ? PlanExpression.NullableOf(entry, context) ?? NullableAt(root, entry, context)
                : NullableAt(root, entry, context);
        }

        // The type of an entry of the root's Output when the walk proves it is not NULL, read as NullableOf reads it:
        // a reference's declared type, or the type of an expression the reader proves.  A subquery's value is none
        // here, since no column's type is known.
        private long? ValueTypeOf(string entry)
        {
            if (OutputReference.Parse(entry) is { } reference)
            {
                return ProvedTypeOf(reference);
            }

            var context = ContextOf(entry, TotalFunctions.UnknownType);
            return (_nothingExtends ? PlanExpression.ProvedTypeOf(entry, context) : null)
                ?? ProvedTypeAt(root, entry, context);
        }

        // What the reader is told of an entry, wherever the walk reads it: the references it proves, and the subplans.
        private ExpressionContext ContextOf(string entry, long columnType) =>
            new(
                _scope.CatalogFunctionsFirst,
                ProvedTypeOf,
                ScalarSubPlanTypeOf: name => IsWhole(entry, name) ? ScalarSubPlanTypeOf(name, columnType) : null,
                SubPlanColumnTypeOf: SubPlanColumnTypeOf
            );

        // Whether the entry is the subplan's value and nothing else, once the parentheses that wrap it whole are off.
        private static bool IsWhole(string entry, string name)
        {
            var inner = entry;
            while (PlanExpression.Unwrapped(inner) is { } unwrapped)
            {
                inner = unwrapped;
            }

            return string.Equals(inner, name, StringComparison.Ordinal);
        }

        // The type of a scalar subquery that is the whole column, when its plan always returns one row whose value is
        // proved of the column's type, and that type is in a family of SetOperationTypes.  A Plain Aggregate with no
        // Group Key, no Grouping Sets and no Filter returns one row whatever its input.  The type is what tells the
        // subquery from the other subplans on PostgreSQL 14, which prints each as (SubPlan n): an EXISTS, an IN, an
        // ANY, an ALL and a comparison of rows are boolean, and an ARRAY an array, none in a family.
        private long? ScalarSubPlanTypeOf(string name, long columnType)
        {
            if (
                !SetOperationTypes.InFamily(columnType)
                || SubPlanOf(name) is not { } subPlan
                || subPlan.Root
                    is not {
                        NodeType: PlanNodeTypes.Aggregate,
                        Strategy: AggregateStrategies.Plain,
                        HasGroupKey: false,
                        HasGroupingSets: false,
                        HasFilter: false,
                        Output: [var value],
                    }
            )
            {
                return null;
            }

            return subPlan.ValueTypeOf(value) == columnType ? columnType : null;
        }

        // The type of a column, by its number from 1, of the root of the subplan of the name, when its plan proves it
        // not NULL in every row.
        private long? SubPlanColumnTypeOf(string name, int column) =>
            column > 0 && SubPlanOf(name) is { Root.Output: { } output } subPlan && column <= output.Count
                ? subPlan.ValueTypeOf(output[column - 1])
                : null;

        // The entries of the SubPlan of the name, read as a plan of its own: one node of the whole plan has that name,
        // its plan holds no set operation and is one the walk can use, and the walk is no more than Plans.MaxDepth
        // plans deep.  In a statement that may name OLD or NEW none is read, since a subplan has no target whose alias
        // alone may be mapped, and the old or the new row may print under an alias that a relation of the subplan has.
        private Expressions? SubPlanOf(string name)
        {
            if (plansFollowed >= Plans.MaxDepth || _scope.OldAndNewMayBeNamed)
            {
                return null;
            }

            if (!_subPlans.TryGetValue(name, out var found))
            {
                found =
                    plans.Tree.OnlyPlanNamed(PlanRelationships.SubPlan, name) is { } subRoot
                    && plans.InstancesOf(subRoot) is { Reason: null, HasSetOperation: false } subInstances
                        ? new Expressions(plans, subRoot, subInstances, plans.OnlyAliasOf(subRoot), plansFollowed + 1)
                        : null;
                _subPlans[name] = found;
            }

            return found;
        }

        // What the entry of the node's Output says, read where it is computed (ComputedAt): unknown when a reading is,
        // and true when one is.
        private bool? NullableAt(PlanNode node, string entry, ExpressionContext context)
        {
            if (ComputedAt(node, entry, context, 0) is not { } readings)
            {
                return null;
            }

            bool? answer = null;
            foreach (var (computed, at) in readings)
            {
                if (PlanExpression.NullableOf(computed, at) is not { } read)
                {
                    return null;
                }

                answer = answer == true || read;
            }

            return answer;
        }

        // The type of the entry of the node's Output, read where it is computed (ComputedAt), when every reading
        // proves it of one type.
        private long? ProvedTypeAt(PlanNode node, string entry, ExpressionContext context)
        {
            if (ComputedAt(node, entry, context, 0) is not { } readings)
            {
                return null;
            }

            long? type = null;
            foreach (var (computed, at) in readings)
            {
                if (PlanExpression.ProvedTypeOf(computed, at) is not { } read || (type is { } seen && seen != read))
                {
                    return null;
                }

                type = read;
            }

            return type;
        }

        // Where the entry of the node's Output is computed, each with the entry as it is printed there and what the
        // reader is told there; or null when that is unknown.  A child that prints the entry, or the entry without the
        // parentheses that wrap it whole, is where it comes from, and the walk goes on there; and an entry that no
        // child prints is the node's own, unknown if a join null-extends the node.  A child that prints no Output, an
        // Append or a Bitmap Index Scan, may compute any value, so it and every node under it must be preserved.
        // Where the entry is computed, a value it takes from a child that computed it is in parentheses: one that is
        // not preserved is read as unknown, so that a pulled-up constant of the side a join extends, ('x'::text)
        // inside a CASE the root computes, proves nothing, while a COALESCE with a constant still does.  There an
        // aggregate is the node's own, over a group that has a row when the node is an Aggregate with a Group Key and
        // no Grouping Sets, unless it is inside a value a child printed, which the child computed under its own
        // grouping.
        private List<(string Entry, ExpressionContext Context)>? ComputedAt(
            PlanNode node,
            string entry,
            ExpressionContext context,
            int depth
        )
        {
            if (FollowedChildrenOf(node, depth) is not { } children)
            {
                return null;
            }

            var printing = Printing(children, entry).ToArray();
            if (printing.Length == 0)
            {
                return instances.IsExtended(node)
                    ? null
                    :
                    [
                        (
                            entry,
                            context with
                            {
                                GroupPreserved = group =>
                                    Printing(children, group)
                                        .All(found => Preserved(found.Child, found.Printed, depth + 1)),
                                GroupFromChild = group => Printing(children, group).Any(),
                                GroupsHaveRows = GroupsHaveRows(node),
                            }
                        ),
                    ];
            }

            var readings = new List<(string Entry, ExpressionContext Context)>();
            foreach (var (child, printed) in printing)
            {
                if (ComputedAt(child, printed, context, depth + 1) is not { } under)
                {
                    return null;
                }

                readings.AddRange(under);
            }

            return readings;
        }

        // Whether each group of the node has a row: an Aggregate with a Group Key makes its groups of the rows that
        // share a key, while one with no key returns a row for an input of none, and Grouping Sets may hold the empty
        // set.
        private static bool GroupsHaveRows(PlanNode node) =>
            node.NodeType == PlanNodeTypes.Aggregate && node.HasGroupKey && !node.HasGroupingSets;

        // Whether the entry of the node's Output is computed where no join null-extends, and so is every value it is
        // computed from, followed as ComputedAt follows it.
        private bool Preserved(PlanNode node, string entry, int depth)
        {
            if (FollowedChildrenOf(node, depth) is not { } children)
            {
                return false;
            }

            var printing = Printing(children, entry).ToArray();
            if (printing.Length > 0)
            {
                return printing.All(found => Preserved(found.Child, found.Printed, depth + 1));
            }

            return !instances.IsExtended(node)
                && PlanExpression
                    .Groups(entry)
                    .All(group =>
                        Printing(children, group).All(found => Preserved(found.Child, found.Printed, depth + 1))
                    );
        }

        // The children an entry of the node is followed into, its Outer and Inner ones; or null, which ComputedAt and
        // Preserved read as unknown, when the walk is too deep or a child that prints no Output is not preserved.
        private PlanNode[]? FollowedChildrenOf(PlanNode node, int depth)
        {
            if (depth > MaxDepth)
            {
                return null;
            }

            PlanNode[] children = [.. node.JoinChildren];
            return children.Any(OpaqueAndNotPreserved) ? null : children;
        }

        // A child that prints no Output, and is extended or has a join under it that extends.
        private bool OpaqueAndNotPreserved(PlanNode child) =>
            child.Output is null && (instances.IsExtended(child) || PlanInstances.ExtendsUnder(child));

        // Each child that prints the value, and how: as it is, wrapped whole in parentheses, or without the
        // parentheses that wrap it whole.
        private static IEnumerable<(PlanNode Child, string Printed)> Printing(PlanNode[] children, string value)
        {
            var inner = PlanExpression.Unwrapped(value);
            var wrapped = "(" + value + ")";
            foreach (var child in children)
            {
                if (child.Output is not { } output)
                {
                    continue;
                }

                if (output.Contains(value, StringComparer.Ordinal))
                {
                    yield return (child, value);
                }
                else if (output.Contains(wrapped, StringComparer.Ordinal))
                {
                    yield return (child, wrapped);
                }
                else if (inner is not null && output.Contains(inner, StringComparer.Ordinal))
                {
                    yield return (child, inner);
                }
            }
        }

        /// <summary>
        /// The declared type of the column a reference names, a domain where the column is one, when the walk proves
        /// that it is not <c>NULL</c>; or null.  A system column, which "tables" reads no attribute of and no user
        /// column may be named as, is of its own type when the catalog's answer of a <c>NOT NULL</c> column of its
        /// table would be not nullable.
        /// </summary>
        public long? ProvedTypeOf(OutputReference reference)
        {
            var alias = reference.Alias ?? (_scope.OldAndNewMayBeNamed ? null : instances.BareAlias);
            if (
                alias is null
                || (onlyAlias is not null && !string.Equals(alias, onlyAlias, StringComparison.Ordinal))
                || !instances.TryFind(alias, out var instance)
                || instance.NullExtended
                || TableOf(instance) is not { } table
            )
            {
                return null;
            }

            var attribute = table.Columns.FirstOrDefault(column =>
                string.Equals(column.Name, reference.Column, StringComparison.Ordinal)
            );
            if (attribute is null)
            {
                return ColumnNullability.KeepsNotNull(table) ? SystemColumnTypeOf(reference.Column) : null;
            }

            return ColumnNullability.Of(new ColumnOrigin(table, attribute), gated: false).Nullable == false
                ? attribute.TypeOid
                : null;
        }

        // The type of a system column of a table by its name, or null for any other name.
        private static long? SystemColumnTypeOf(string name) =>
            name switch
            {
                "tableoid" => BuiltInTypes.Oid,
                "ctid" => BuiltInTypes.Tid,
                "xmin" or "xmax" => BuiltInTypes.Xid,
                "cmin" or "cmax" => BuiltInTypes.Cid,
                _ => null,
            };

        // The table "tables" answered of an instance: a relation's by its schema and its name, and a partitioned
        // table's by the nearest of the tables its partitions are partitions of, whose NOT NULL every partition keeps.
        // A scan of another plan has none.
        private PostgresTableRow? TableOf(PlanInstance instance) =>
            instance.Kind switch
            {
                InstanceKind.Relation => _scope.Tables.Values.FirstOrDefault(row =>
                    string.Equals(row.Schema, instance.Schema, StringComparison.Ordinal)
                    && string.Equals(row.Name, instance.Relation, StringComparison.Ordinal)
                ),
                InstanceKind.Partitioned => instance
                    .Parents.Select(oid => _scope.Tables.GetValueOrDefault(oid))
                    .FirstOrDefault(static row => row is not null),
                InstanceKind.Plan => null,
                _ => throw new InvalidOperationException($"Unknown kind {instance.Kind}."),
            };
    }
}
