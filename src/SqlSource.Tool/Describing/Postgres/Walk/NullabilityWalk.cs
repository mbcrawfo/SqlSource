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
/// The rows of <c>tables</c>, by OID, with those <c>relations</c> answered for the relations the plan scans that
/// <c>tables</c> did not, where the column of a reference inside an expression is looked up.  A relation it does not
/// hold leaves the reference unproved.
/// </param>
/// <param name="ColumnTypes">
/// The type of each column as the row description gives it, by OID, in the order of the columns.  A list of another
/// length, an empty one for a walk that is to answer no column of a set operation from its branches, answers none.
/// </param>
/// <param name="CatalogAlone">
/// The names of <see cref="CatalogOverloads" /> that <c>overloads</c> answered no schema but <c>pg_catalog</c> holds,
/// and <c>pg_catalog</c> holds none of that is not built in, or null for none: a name it was not asked, and every
/// name of a call that failed, is held elsewhere.
/// </param>
internal sealed record WalkScope(
    bool Gated,
    bool OldAndNewMayBeNamed,
    bool CatalogFunctionsFirst,
    IReadOnlyDictionary<long, PostgresTableRow> Tables,
    IReadOnlyList<long> ColumnTypes,
    CatalogNames? CatalogAlone = null
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

    // Whether every reference the walk can map names the column the row description gives as its origin: a guard that
    // the plan and the row description agree, written when a pooler could give the plan of another statement of the
    // same name, which a describe in one transaction no longer meets.  A view's column
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
