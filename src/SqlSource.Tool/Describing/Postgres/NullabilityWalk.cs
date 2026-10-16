using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>A column after the walk, with what the walk read of it, as the log says it.</summary>
/// <param name="Column">The column as it will be recorded.</param>
/// <param name="Output">The column's entry of the root's <c>Output</c>, or null when there was none to read.</param>
/// <param name="Reference">The entry as a reference, or null for an expression and for a column not looked up.</param>
/// <param name="Mapped">Whether the reference named a relation instance of the plan.</param>
/// <param name="NullExtended">Whether a join null-extends that instance.</param>
internal sealed record WalkedColumn(
    SidecarColumn Column,
    string? Output,
    OutputReference? Reference,
    bool Mapped,
    bool NullExtended
);

/// <summary>What a walk gave: the plan's value, each column, and what the log says of how.</summary>
/// <param name="Plan"><c>walked</c>, or <c>unavailable</c> when the walk had no plan it could use.</param>
/// <param name="Reason">
/// Why it is <c>unavailable</c>, one of <see cref="WalkReasons" />, or null; for a plan that was <c>walked</c>,
/// <c>output-differs</c> when its expressions were not read.
/// </param>
/// <param name="Nodes">The nodes that were visited, none when there was no plan to read.</param>
/// <param name="Columns">Every column of the result, in order.</param>
internal sealed record WalkOutcome(
    string Plan,
    string? Reason,
    IReadOnlyList<VisitedNode> Nodes,
    IReadOnlyList<WalkedColumn> Columns
);

/// <summary>What a walk is asked to answer, beside the plan and the columns.</summary>
/// <param name="Gated">
/// Whether <see cref="OuterJoinGate" /> found a word in the query.  Only then does the plan answer a column with an
/// origin: a query without one is planned for its columns of no origin, and no outer join can have touched the rest.
/// </param>
/// <param name="OldAndNewMayBeNamed">
/// Whether the query's SQL names <c>OLD</c> or <c>NEW</c>, so that a reference at a root <c>ModifyTable</c> may be
/// PostgreSQL 18's old or new row.
/// </param>
/// <param name="CatalogFunctionsFirst">
/// Whether the session finds <c>pg_catalog</c>'s functions first (<see cref="PostgresSearchPath" />), so that a
/// function the plan prints bare is the catalog's.
/// </param>
/// <param name="Tables">
/// The answer of <c>tables</c>, by OID, where the column of a reference inside an expression is looked up.  A
/// relation it does not hold leaves the reference unproved.
/// </param>
/// <param name="Partitions">
/// The answer of <c>partitions</c>, the ancestors of each relation the members of the plan's <c>Append</c>s scan, or
/// null when it was not asked: every <c>Append</c> is then read as a set operation.
/// </param>
internal sealed record WalkScope(
    bool Gated,
    bool OldAndNewMayBeNamed,
    bool CatalogFunctionsFirst,
    IReadOnlyDictionary<long, PostgresTableRow> Tables,
    IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>>? Partitions = null
);

/// <summary>
/// The third layer of nullability: a column of a gated query is not nullable when its origin is <c>NOT NULL</c>
/// and the plan proves that no outer join null-extends its relation instance, and a column of no origin is not
/// nullable when its entry of the plan is an expression that can never be <c>NULL</c> (<see cref="PlanExpression" />)
/// computed where no outer join null-extends it.
/// </summary>
/// <remarks>
/// <para>
/// The root's <c>Output</c> is the result's target list in order, and a plain reference in it prints as
/// <c>alias.column</c>, which names the relation instance and so tells the two sides of a self-join apart.  The
/// planner's join strength reduction comes for free: a <c>LEFT JOIN</c> that a <c>WHERE</c> makes inner is an
/// <c>Inner</c> join in the plan.
/// </para>
/// <para>
/// Whatever the walk does not understand keeps the heuristic and is never a wrong <c>false</c>.  An entry that is
/// no plain reference, or that names no instance, degrades its column alone; a plan that cannot be used degrades every
/// column.  A reference through a <c>Subquery Scan</c> or a <c>CTE Scan</c> is resolved in the plan it scans by the
/// column's origin, and never by its place or its name: the scan prints the subquery's columns in the order the query
/// above needs them, and a column renamed to another's name would be read as the other.
/// </para>
/// <para>
/// At a root <c>ModifyTable</c> PostgreSQL 18 prints the old and the new row of a <c>RETURNING</c> list as
/// <c>old.column</c> and <c>new.column</c>, or under the names <c>WITH (OLD AS ..., NEW AS ...)</c> gives them, and
/// a table of an inner scope, the <c>SELECT</c> of an <c>INSERT</c> or a subquery in <c>FROM</c>, can have the same
/// alias in the plan.  In a statement that names <c>OLD</c> or <c>NEW</c> a qualified reference there maps only to the
/// target's own alias, which the server never lets the old or the new row take; so does one at the root of a plan a
/// scan scans, a data-modifying CTE's.
/// </para>
/// </remarks>
internal static class NullabilityWalk
{
    /// <summary>Walks a plan that was read.</summary>
    /// <param name="root">The root of the plan.</param>
    /// <param name="columns">
    /// The columns as they were built: for a gated query with the heuristic's answer, and for one that is not with the
    /// catalog's.
    /// </param>
    /// <param name="origins">Where each column comes from, or null, in the order of the columns.</param>
    /// <param name="scope">What the walk is asked to answer.</param>
    public static WalkOutcome Run(
        PlanNode root,
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins,
        WalkScope scope
    )
    {
        var plans = new Plans(root, scope);
        var instances = plans.InstancesOf(root);

        // PostgreSQL prints the target list first and what a sort or a group added after it, so the entry of a
        // column is the one at its ordinal.  A UNION ALL's Append has no Output at all.
        if (root.Output is not { } entries || entries.Count < columns.Count)
        {
            return Unusable(WalkReasons.NoOutput, root, instances, columns);
        }

        if (instances.Reason is { } reason)
        {
            return Unusable(reason, root, instances, columns);
        }

        // A plan that may be another statement's is read for no expression.  A gated query's columns with an origin are
        // walked as they were before expressions were read, since column aliases print a column under another name,
        // and a query that is not gated has nothing else to read.
        var fits = OutputFits(entries, origins, instances);
        if (!fits && !scope.Gated)
        {
            return Unusable(WalkReasons.OutputDiffers, root, instances, columns);
        }

        var onlyAlias = plans.OnlyAliasOf(root);
        var expressions =
            fits && !instances.HasSetOperation ? new Expressions(scope, root, instances, onlyAlias) : null;

        var walked = new List<WalkedColumn>(columns.Count);
        for (var index = 0; index < columns.Count; index++)
        {
            walked.Add(
                origins[index] switch
                {
                    null => ExpressionOf(columns[index], entries[index], expressions),
                    { } origin when scope.Gated => ColumnOf(
                        columns[index],
                        origin,
                        entries[index],
                        instances,
                        onlyAlias,
                        plans
                    ),
                    _ => Kept(columns[index], entries[index]),
                }
            );
        }

        return new WalkOutcome(
            SidecarValues.Plan.Walked,
            fits ? null : WalkReasons.OutputDiffers,
            instances.Nodes,
            walked
        );
    }

    /// <summary>
    /// The outcome of a walk that had no plan to read: the server gave none, or its text is no plan.  Every column
    /// keeps what it was built with.
    /// </summary>
    /// <param name="reason">One of <see cref="WalkReasons" />.</param>
    /// <param name="columns">The columns as they were built.</param>
    public static WalkOutcome Unavailable(string reason, IReadOnlyList<SidecarColumn> columns) =>
        new(SidecarValues.Plan.Unavailable, reason, [], [.. columns.Select(static column => Kept(column, null))]);

    // A plan that was read and cannot be used: every column keeps what it was built with, and the log is given
    // the nodes and the entries all the same.
    private static WalkOutcome Unusable(
        string reason,
        PlanNode root,
        PlanInstances instances,
        IReadOnlyList<SidecarColumn> columns
    ) =>
        new(
            SidecarValues.Plan.Unavailable,
            reason,
            instances.Nodes,
            [.. columns.Select((column, index) => Kept(column, EntryAt(root, index)))]
        );

    private static WalkedColumn Kept(SidecarColumn column, string? entry) => new(column, entry, null, false, false);

    // A column of no origin: the answer of its entry as an expression, or what it was built with.
    private static WalkedColumn ExpressionOf(SidecarColumn column, string entry, Expressions? expressions) =>
        expressions?.NullableOf(entry) is { } nullable
            ? new WalkedColumn(
                column with
                {
                    Nullable = nullable,
                    NullableSource = SidecarValues.NullableSource.Expression,
                },
                entry,
                null,
                false,
                false
            )
            : Kept(column, entry);

    // Whether every reference the walk can map names the column the row description gives as its origin.  A plan of
    // another statement, which a pooler can give for this one's name (TD-0049), names other columns.  A view's column
    // prints as the column of the table under it, under that column's name, so only a table's origin is compared, and
    // only by name: a partition prints under its own alias and a child under its parent's columns' names.  A scan of
    // another plan prints the subquery's names for its columns, which are no table's.
    private static bool OutputFits(
        IReadOnlyList<string> entries,
        IReadOnlyList<ColumnOrigin?> origins,
        PlanInstances instances
    )
    {
        for (var index = 0; index < origins.Count; index++)
        {
            if (
                origins[index] is { Table.Relkind: "r" or "p" } origin
                && OutputReference.Parse(entries[index]) is { } reference
                && (
                    reference.Alias is { } alias
                        ? instances.TryFind(alias, out var instance) && instance.Kind != InstanceKind.Plan
                        : instances.BareAlias is not null
                )
                && !string.Equals(reference.Column, origin.Attribute.Name, StringComparison.Ordinal)
            )
            {
                return false;
            }
        }

        return true;
    }

    private static string? EntryAt(PlanNode root, int index) =>
        root.Output is { } output && index < output.Count ? output[index] : null;

    private static WalkedColumn ColumnOf(
        SidecarColumn column,
        ColumnOrigin origin,
        string entry,
        PlanInstances instances,
        string? onlyAlias,
        Plans plans
    )
    {
        var reference = OutputReference.Parse(entry);
        var instance = reference switch
        {
            null => null,
            { Alias: { } alias } => (onlyAlias is null || string.Equals(alias, onlyAlias, StringComparison.Ordinal))
            && instances.TryFind(alias, out var named)
                ? named
                : null,

            // A bare name is a column of the plan's one relation, which no join extends.
            _ => instances.BareAlias is { } bare && instances.TryFind(bare, out var only) ? only : null,
        };

        var resolved = instance switch
        {
            null => Resolution.Unknown,
            { NullExtended: true } => Resolution.Extended,
            { Kind: InstanceKind.Plan, Child: var child } => plans.Resolve(child, origin, 1),
            _ => Resolution.Preserved,
        };

        var (nullable, source) = resolved switch
        {
            Resolution.Extended => (true, SidecarValues.NullableSource.OuterJoin),

            // The catalog's answer: NOT NULL for a table, and unknown with "view" for a view.
            Resolution.Preserved => ColumnNullability.Of(origin, gated: false),
            Resolution.Unknown or Resolution.NoCandidate => ColumnNullability.Of(origin, gated: true),
            _ => throw new InvalidOperationException($"Unknown resolution {resolved}."),
        };

        var mapped = resolved is Resolution.Preserved or Resolution.Extended;
        return new WalkedColumn(
            column with
            {
                Nullable = nullable,
                NullableSource = source,
            },
            entry,
            reference,
            mapped,
            resolved == Resolution.Extended
        );
    }

    // The entries of a plan read as expressions, with a reference inside one answered as the walk answers a column:
    // through an instance that no join extends, to a column of a relation that "tables" answered, by the catalog's
    // rule.  A bare name is the plan's one relation's (PlanInstances.BareAlias): a plan of several prints every name
    // with its alias but the columns of a relation the planner proved empty, a Result under an outer join, and
    // at a ModifyTable that may return the old or the new row only the target's alias is a relation.  An entry is read
    // in a plan where no join null-extends a side, or where the walk follows it to the node that computes it and no
    // join null-extends that node: a node prints a value computed under it in one pair of parentheses, at every node
    // up to the root.
    private sealed class Expressions(WalkScope scope, PlanNode root, PlanInstances instances, string? onlyAlias)
    {
        private const int MaxDepth = 256;

        private readonly bool _nothingExtends = instances.Nodes.All(static visited => visited.NullExtends == "none");

        public bool? NullableOf(string entry) =>
            _nothingExtends
                ? PlanExpression.NullableOf(entry, scope.CatalogFunctionsFirst, ProvedNotNull)
                : NullableAt(root, entry, 0);

        // What the entry of the node's Output says, read where it is computed: a child that prints the entry, or the
        // entry without the parentheses that wrap it whole, is where it comes from, and the walk goes on there; and an
        // entry that no child prints is the node's own, unknown if a join null-extends the node.  A child that prints
        // no Output, an Append or a Bitmap Index Scan, may compute any value, so it and every node under it must be
        // preserved.  Where the entry is computed, a value it takes from a child that computed it is in parentheses:
        // one that is not preserved is read as unknown, so that a pulled-up constant of the side a join extends,
        // ('x'::text) inside a CASE the root computes, proves nothing, while a COALESCE with a constant still does.
        private bool? NullableAt(PlanNode node, string entry, int depth)
        {
            if (depth > MaxDepth)
            {
                return null;
            }

            var children = node.Plans.Where(static child => child.ParentRelationship is "Outer" or "Inner").ToArray();
            if (children.Any(OpaqueAndNotPreserved))
            {
                return null;
            }

            bool? answer = null;
            foreach (var (child, printed) in Printing(children, entry))
            {
                var passed = NullableAt(child, printed, depth + 1);
                if (passed is null)
                {
                    return null;
                }

                answer = answer is null or false ? passed : answer;
            }

            if (answer is not null)
            {
                return answer;
            }

            return instances.IsExtended(node)
                ? null
                : PlanExpression.NullableOf(
                    entry,
                    scope.CatalogFunctionsFirst,
                    ProvedNotNull,
                    group => Printing(children, group).All(found => Preserved(found.Child, found.Printed, depth + 1))
                );
        }

        // Whether the entry of the node's Output is computed where no join null-extends, and so is every value it is
        // computed from, followed as NullableAt follows it.
        private bool Preserved(PlanNode node, string entry, int depth)
        {
            if (depth > MaxDepth)
            {
                return false;
            }

            var children = node.Plans.Where(static child => child.ParentRelationship is "Outer" or "Inner").ToArray();
            if (children.Any(OpaqueAndNotPreserved))
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

        private bool ProvedNotNull(OutputReference reference)
        {
            var alias = reference.Alias ?? (scope.OldAndNewMayBeNamed ? null : instances.BareAlias);
            if (
                alias is null
                || (onlyAlias is not null && !string.Equals(alias, onlyAlias, StringComparison.Ordinal))
                || !instances.TryFind(alias, out var instance)
                || instance.NullExtended
                || TableOf(instance) is not { } table
            )
            {
                return false;
            }

            var attribute = table.Columns.FirstOrDefault(column =>
                string.Equals(column.Name, reference.Column, StringComparison.Ordinal)
            );
            return attribute is not null
                && ColumnNullability.Of(new ColumnOrigin(table, attribute), gated: false).Nullable == false;
        }

        // The table "tables" answered of an instance: a relation's by its schema and its name, and a partitioned
        // table's by the nearest of the tables its partitions are partitions of, whose NOT NULL every partition keeps.
        // A scan of another plan has none.
        private PostgresTableRow? TableOf(PlanInstance instance) =>
            instance.Kind switch
            {
                InstanceKind.Relation => scope.Tables.Values.FirstOrDefault(row =>
                    string.Equals(row.Schema, instance.Schema, StringComparison.Ordinal)
                    && string.Equals(row.Name, instance.Relation, StringComparison.Ordinal)
                ),
                InstanceKind.Partitioned => instance
                    .Parents.Select(oid => scope.Tables.GetValueOrDefault(oid))
                    .FirstOrDefault(static row => row is not null),
                InstanceKind.Plan => null,
                _ => throw new InvalidOperationException($"Unknown kind {instance.Kind}."),
            };
    }

    // What a reference through a scan of another plan resolves to.
    private enum Resolution
    {
        // Nothing can be said: the column keeps the heuristic.
        Unknown,

        // The plan has no entry the column can be.
        NoCandidate,

        // Every entry the column can be is of an instance that no join extends.
        Preserved,

        // Every entry the column can be is of an instance that a join extends.
        Extended,
    }

    // The plans of a walk: the root's, and the plan of each scan of another plan, each walked as a plan of its own.
    // A column through a scan is resolved by its origin and never by its place or its name: a Subquery Scan prints the
    // subquery's columns in the order the query above needs them, and the plan prints no name of the subquery's
    // columns.  A column with an origin is a plain reference to that column in the subquery, so its entry of the
    // child plan's Output is among the references to a relation instance of the origin's table and column; when
    // every such reference agrees, the column is what they are.
    private sealed class Plans(PlanNode tree, WalkScope scope)
    {
        private const int MaxDepth = 16;

        private static readonly IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>> NoPartitions =
            new Dictionary<PostgresRelationName, IReadOnlyList<long>>();

        private readonly Dictionary<PlanNode, PlanInstances> _instances = [with(ReferenceEqualityComparer.Instance)];

        public PlanInstances InstancesOf(PlanNode root)
        {
            if (!_instances.TryGetValue(root, out var found))
            {
                found = PlanInstances.Find(root, tree, scope.Partitions ?? NoPartitions);
                _instances[root] = found;
            }

            return found;
        }

        // The one alias a qualified reference may name at a ModifyTable that may return the old or the new row, or
        // null when any alias may be mapped.  A ModifyTable without an alias leaves none: no reference is empty.
        public string? OnlyAliasOf(PlanNode root) =>
            scope.OldAndNewMayBeNamed && root.NodeType == "ModifyTable" ? root.Alias ?? string.Empty : null;

        // The column of the origin in the plan of the child, as the entries of its root's Output that it can be say.
        public Resolution Resolve(PlanNode? child, ColumnOrigin origin, int depth)
        {
            if (child is null || depth > MaxDepth || child.Output is not { } entries)
            {
                return Resolution.Unknown;
            }

            var instances = InstancesOf(child);
            if (instances.Reason is not null || instances.HasSetOperation)
            {
                return Resolution.Unknown;
            }

            var onlyAlias = OnlyAliasOf(child);
            var found = Resolution.NoCandidate;
            foreach (var entry in entries)
            {
                // An expression is never the column, which is a plain reference in the subquery, unless the planner
                // made the reference a parameter or a placeholder, (u.id), a LATERAL reference to the query above for
                // one: an expression that names the column's name, or holds a parameter, says nothing can be said.  So
                // does a bare name, or an alias that is no instance of this plan.
                if (OutputReference.Parse(entry) is not { } reference)
                {
                    if (PlanExpression.MayNameColumn(entry, origin.Attribute.Name))
                    {
                        return Resolution.Unknown;
                    }

                    continue;
                }

                if (
                    reference.Alias is not { } alias
                    || (onlyAlias is not null && !string.Equals(alias, onlyAlias, StringComparison.Ordinal))
                    || !instances.TryFind(alias, out var instance)
                )
                {
                    return Resolution.Unknown;
                }

                found = Combine(found, Candidate(instance, reference, origin, depth));
                if (found == Resolution.Unknown)
                {
                    return found;
                }
            }

            return found;
        }

        private static Resolution Combine(Resolution found, Resolution next) =>
            (found, next) switch
            {
                (Resolution.NoCandidate, _) => next,
                (_, Resolution.NoCandidate) => found,
                _ when found == next => found,
                _ => Resolution.Unknown,
            };

        // What one reference of the child's Output says of the column.  A scan of a plan inside is resolved in turn,
        // for every one of its references, since which of its columns this one is cannot be told either.
        private Resolution Candidate(PlanInstance instance, OutputReference reference, ColumnOrigin origin, int depth)
        {
            var named = instance.Kind switch
            {
                InstanceKind.Relation => string.Equals(instance.Schema, origin.Table.Schema, StringComparison.Ordinal)
                    && string.Equals(instance.Relation, origin.Table.Name, StringComparison.Ordinal),
                InstanceKind.Partitioned => instance.Parents.Contains(origin.Table.Oid),
                InstanceKind.Plan => true,
                _ => throw new InvalidOperationException($"Unknown kind {instance.Kind}."),
            };
            if (
                !named
                || (
                    instance.Kind != InstanceKind.Plan
                    && !string.Equals(reference.Column, origin.Attribute.Name, StringComparison.Ordinal)
                )
            )
            {
                return Resolution.NoCandidate;
            }

            var resolved =
                instance.Kind == InstanceKind.Plan ? Resolve(instance.Child, origin, depth + 1) : Resolution.Preserved;
            return resolved == Resolution.Preserved && instance.NullExtended ? Resolution.Extended : resolved;
        }
    }
}
