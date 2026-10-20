using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres.Columns;

namespace SqlSource.Tool.Describing.Postgres.Walk;

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
/// <param name="ColumnTypes">
/// The type of each column as the row description gives it, by OID, in the order of the columns.  A list of another
/// length, an empty one for a walk that is to answer no column of a set operation from its branches, answers none.
/// </param>
internal sealed record WalkScope(
    bool Gated,
    bool OldAndNewMayBeNamed,
    bool CatalogFunctionsFirst,
    IReadOnlyDictionary<long, PostgresTableRow> Tables,
    IReadOnlyList<long> ColumnTypes
);

/// <summary>
/// The third layer of nullability: a column of a gated query is not nullable when its origin is <c>NOT NULL</c>
/// and the plan proves that no outer join null-extends its relation instance, and a column of no origin is not
/// nullable when its entry of the plan is an expression that can never be <c>NULL</c> (<see cref="PlanExpression" />)
/// computed where no outer join null-extends it, or when the root reaches a set operation whose inputs that it returns
/// rows of each prove it, as <c>NullabilityWalk.SetOperations.cs</c> says.
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
internal static partial class NullabilityWalk
{
    /// <summary>Walks a plan that was read.</summary>
    /// <param name="cache">The instances of the plan's plans, whose tree is the plan.</param>
    /// <param name="columns">
    /// The columns as they were built: for a gated query with the heuristic's answer, and for one that is not with the
    /// catalog's.
    /// </param>
    /// <param name="origins">Where each column comes from, or null, in the order of the columns.</param>
    /// <param name="scope">What the walk is asked to answer.</param>
    public static WalkOutcome Run(
        PlanInstancesCache cache,
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins,
        WalkScope scope
    )
    {
        var plans = new Plans(cache, scope);
        var root = plans.Tree;
        var instances = plans.InstancesOf(root);

        // A column of no origin of a set operation that the root reaches is answered from the operation's inputs.
        if (SetOperationTypesOf(plans, columns, origins) is { } types)
        {
            return RunSetOperation(plans, columns, origins, types);
        }

        // PostgreSQL prints the target list first and what a sort or a group added after it, so the entry of a
        // column is the one at its ordinal.  A UNION ALL's Append has no Output at all, and a column of one is not
        // walked here.
        if (root.Output is not { } entries || entries.Count < columns.Count)
        {
            return Unusable(WalkReasons.NoOutput, root, instances, columns);
        }

        if (instances.Reason is { } reason)
        {
            return Unusable(reason, root, instances, columns);
        }

        // A plan that may be another statement's is read for no expression.  A gated query's columns with an origin are
        // still walked, since column aliases print a column under another name, and a query that is not gated has
        // nothing else to read.
        var fits = OutputFits(entries, origins, instances);
        if (!fits && !scope.Gated)
        {
            return Unusable(WalkReasons.OutputDiffers, root, instances, columns);
        }

        var onlyAlias = plans.OnlyAliasOf(root);
        var expressions =
            fits && !instances.HasSetOperation ? new Expressions(plans, root, instances, onlyAlias) : null;

        var walked = new List<WalkedColumn>(columns.Count);
        for (var index = 0; index < columns.Count; index++)
        {
            var type = scope.ColumnTypes.Count == columns.Count ? scope.ColumnTypes[index] : TotalFunctions.UnknownType;
            walked.Add(
                origins[index] switch
                {
                    null => ExpressionOf(columns[index], entries[index], type, expressions),
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
    private static WalkedColumn ExpressionOf(SidecarColumn column, string entry, long type, Expressions? expressions) =>
        expressions?.NullableOf(entry, type) is { } nullable
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

            // The catalog's answer, by ColumnNullability's rule for a query that is not gated.
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

    // The plans of a walk, with what the walk is asked: the root's, and the plan of each scan of another plan, each
    // walked as a plan of its own.
    // A column through a scan is resolved by its origin, as the class's remarks say.  A column with an origin is a
    // plain reference to that column in the subquery, so its entry of the child plan's Output is among the
    // references to a relation instance of the origin's table and column; when every such reference agrees, the
    // column is what they are.
    private sealed class Plans(PlanInstancesCache cache, WalkScope scope)
    {
        /// <summary>
        /// How many plans deep the walk follows a column: a scan of another plan, or a set operation's input.
        /// </summary>
        public const int MaxDepth = 16;

        // The root of the whole plan.
        public PlanNode Tree => cache.Tree;

        public WalkScope Scope => scope;

        public PlanInstances InstancesOf(PlanNode root) => cache.Of(root);

        // The one alias a qualified reference may name at a ModifyTable that may return the old or the new row, or
        // null when any alias may be mapped.  A ModifyTable without an alias leaves none: no reference is empty.
        public string? OnlyAliasOf(PlanNode root) =>
            scope.OldAndNewMayBeNamed && root.NodeType == PlanNodeTypes.ModifyTable ? root.Alias ?? string.Empty : null;

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
