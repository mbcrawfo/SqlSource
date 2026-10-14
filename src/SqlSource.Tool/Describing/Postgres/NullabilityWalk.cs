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
/// <param name="Reason">Why it is <c>unavailable</c>, one of <see cref="WalkReasons" />, or null.</param>
/// <param name="Nodes">The nodes that were visited, none when there was no plan to read.</param>
/// <param name="Columns">Every column of the result, in order.</param>
internal sealed record WalkOutcome(
    string Plan,
    string? Reason,
    IReadOnlyList<VisitedNode> Nodes,
    IReadOnlyList<WalkedColumn> Columns
);

/// <summary>
/// The third layer of nullability: a column of a gated query is not nullable when its origin is <c>NOT NULL</c>
/// and the plan proves that no outer join null-extends its relation instance.
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
/// no plain reference, or that names what is no relation instance, a <c>Subquery Scan</c> or a <c>CTE Scan</c>,
/// degrades its column alone; a plan that cannot be used degrades every column.  A reference through a
/// <c>Subquery Scan</c> is not matched to the subquery's own plan by name: a column renamed to another's name
/// would be read as the other.
/// </para>
/// <para>
/// At a root <c>ModifyTable</c> PostgreSQL 18 prints the old and the new row of a <c>RETURNING</c> list as
/// <c>old.column</c> and <c>new.column</c>, or under the names <c>WITH (OLD AS ..., NEW AS ...)</c> gives them, and
/// a table of an inner scope, the <c>SELECT</c> of an <c>INSERT</c> or a subquery in <c>FROM</c>, can have the same
/// alias in the plan.  In a statement that names <c>OLD</c> or <c>NEW</c> a qualified reference there maps only to the
/// target's own alias, which the server never lets the old or the new row take.
/// </para>
/// </remarks>
internal static class NullabilityWalk
{
    /// <summary>Walks a plan that was read.</summary>
    /// <param name="root">The root of the plan.</param>
    /// <param name="columns">The columns as they were built for a gated query, with the heuristic's answer.</param>
    /// <param name="origins">Where each column comes from, or null, in the order of the columns.</param>
    /// <param name="oldAndNewMayBeNamed">
    /// Whether the query's SQL names <c>OLD</c> or <c>NEW</c>, so that a reference at a root <c>ModifyTable</c> may be
    /// PostgreSQL 18's old or new row.
    /// </param>
    public static WalkOutcome Run(
        PlanNode root,
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins,
        bool oldAndNewMayBeNamed
    )
    {
        var instances = PlanInstances.Find(root);

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

        // The one alias a qualified reference may name at a ModifyTable that may return the old or the new row, or
        // null when any alias may be mapped.  A ModifyTable without an alias leaves none: no reference is empty.
        var onlyAlias = oldAndNewMayBeNamed && root.NodeType == "ModifyTable" ? root.Alias ?? string.Empty : null;

        var walked = new List<WalkedColumn>(columns.Count);
        for (var index = 0; index < columns.Count; index++)
        {
            walked.Add(ColumnOf(columns[index], origins[index], entries[index], instances, onlyAlias));
        }

        return new WalkOutcome(SidecarValues.Plan.Walked, null, instances.Nodes, walked);
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

    private static string? EntryAt(PlanNode root, int index) =>
        root.Output is { } output && index < output.Count ? output[index] : null;

    private static WalkedColumn ColumnOf(
        SidecarColumn column,
        ColumnOrigin? origin,
        string entry,
        PlanInstances instances,
        string? onlyAlias
    )
    {
        // Nothing of the plan can make a column without an origin non-nullable: it is not looked up.
        if (origin is null)
        {
            return Kept(column, entry);
        }

        var reference = OutputReference.Parse(entry);
        var extended = false;
        var mapped = reference switch
        {
            null => false,
            { Alias: { } alias } => (onlyAlias is null || string.Equals(alias, onlyAlias, StringComparison.Ordinal))
                && instances.TryFind(alias, out extended),

            // A bare name is a column of the root's own relation, which no join extends: the root has no join under it.
            _ => instances.RootNamesAnInstance,
        };

        var (nullable, source) = (mapped, extended) switch
        {
            (true, true) => (true, SidecarValues.NullableSource.OuterJoin),

            // The catalog's answer: NOT NULL for a table, and unknown with "view" for a view.
            (true, false) => ColumnNullability.Of(origin, gated: false),
            _ => ColumnNullability.Of(origin, gated: true),
        };

        return new WalkedColumn(
            column with
            {
                Nullable = nullable,
                NullableSource = source,
            },
            entry,
            reference,
            mapped,
            mapped && extended
        );
    }
}
