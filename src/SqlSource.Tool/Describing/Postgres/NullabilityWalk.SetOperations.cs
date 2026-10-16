using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;

namespace SqlSource.Tool.Describing.Postgres;

internal static partial class NullabilityWalk
{
    // A column of no origin at the top of a set operation, a UNION, an INTERSECT or an EXCEPT that the root reaches
    // through nodes that pass a child's column on, is answered from the set operation's inputs: false with
    // set-operation when the inputs the operation returns rows of prove it, and as it was built otherwise.  A UNION's
    // Append returns every member's rows, and so does any Append, a partitioned table's or one nested in another: every
    // member must prove it.  An INTERSECT returns rows that both inputs hold, as NULL equals NULL there: one input
    // proves it.  An EXCEPT returns rows of its left input: that one proves it.  PostgreSQL 14 plans the last two as a
    // SetOp over an Append whose members print a flag last, 0 for the left input and 1 for the right; 18 as a SetOp
    // with the left input as its Outer child and the right as its Inner.  A recursive CTE's union, and any other shape,
    // proves nothing.
    //
    // A branch's value counts only when its type carries to the result's (SetOperationTypes): the planner casts a
    // branch to the result's type, and the plan does not print the cast.  The result's type is the row description's,
    // which is a nested set operation's own type only at the top: below another, an INTERSECT or an EXCEPT is of the
    // common type of its own inputs, which may be the type of an input that proves nothing, reached by a user's cast.
    // There it proves the column only when each of its inputs does, and so each is of the result's family.
    //
    // A leaf scan of a set operation, a Subquery Scan aliased *SELECT* <k>, prints a reference to a column of its
    // branch by the column's name, which the plan does not otherwise print, and only its place says which.  The scan is
    // followed by place only as an input of a SetOp, whose target list the planner builds one entry for each column of
    // the branch, in order.  Under an Append it never is: a UNION ALL that a subquery in FROM, a CTE or an inlined SQL
    // function holds is pulled up into its query, and a branch the planner keeps as a scan prints the columns that
    // query needs, in its order.  SELECT s.b, s.a FROM (SELECT p.sku AS a, p.name AS b ... UNION ALL (SELECT p2.sku,
    // NULLIF(p2.name, 'x') ... LIMIT 5)) s prints "*SELECT* 2"."nullif", "*SELECT* 2".sku over a branch that prints
    // p2.sku first, and nothing in the plan tells it from a scan in order.  A constant the planner put in the scan's
    // place is read where it is.  A query whose SQL holds the text *SELECT* may name a subquery of its own so, and no
    // scan is followed.
    //
    // Any other node that prints the value is walked as a plan of its own, as the walk walks the root: a reference by
    // the catalog's answer of an instance that no join of that plan extends, and an expression by PlanExpression.  A
    // set operation under a join, or one that a node above computes over, keeps its answer: the node is walked as a
    // plan, which holds a set operation.

    private const string Outer = "Outer";
    private const string Inner = "Inner";
    private const string Member = "Member";
    private const string SetOp = "SetOp";
    private const string LeafScanPrefix = "*SELECT* ";

    // Nodes that never project: the row is the child's, and so is each column.
    private static readonly HashSet<string> NeverProject =
    [
        with(StringComparer.Ordinal),
        "Sort",
        "Incremental Sort",
        "Limit",
        "Unique",
        "Materialize",
    ];

    private static readonly HashSet<string> Appends = [with(StringComparer.Ordinal), "Append", "Merge Append"];

    /// <summary>
    /// Whether the root reaches an <c>Append</c>, a <c>Merge Append</c> or a <c>SetOp</c> through nodes that may pass
    /// a child's column on: a node with one <c>Outer</c> child, no join type and no grouping set.  An
    /// <c>InitPlan</c> or a <c>SubPlan</c> computes a value and is no child of the row.
    /// </summary>
    public static bool ReachesSetOperation(PlanNode root)
    {
        for (var node = root; node is not null; node = OnlyChildOf(node))
        {
            if (Appends.Contains(node.NodeType) || node.NodeType == SetOp)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Whether the walk may answer a column of no origin of the plan from a set operation's branches: the root reaches
    /// one, and prints no Output or its plan holds one that is no partitioned table's <c>Append</c>.
    /// </summary>
    /// <param name="root">The root of the plan.</param>
    /// <param name="partitions">The answer of <c>partitions</c>, or null when it was not asked.</param>
    public static bool MayAnswerFromBranches(
        PlanNode root,
        IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>>? partitions
    ) =>
        ReachesSetOperation(root)
        && (
            root.Output is null
            || PlanInstances
                .Find(root, root, partitions ?? new Dictionary<PostgresRelationName, IReadOnlyList<long>>())
                .HasSetOperation
        );

    // The types to answer a set operation's columns with, when the walk takes that path: a column has no origin, the
    // types are given, the root prints no Output or its plan holds a set operation, and the root reaches one.  A root
    // that prints Output must print an entry for each column, and one of another statement's plan is read for none.
    private static IReadOnlyList<long>? SetOperationTypesOf(
        PlanNode root,
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins,
        WalkScope scope,
        PlanInstances instances
    ) =>
        scope.ColumnTypes is { } types
        && types.Count == columns.Count
        && origins.Any(static origin => origin is null)
        && instances.Reason is null
        && (
            root.Output is null
            || (
                instances.HasSetOperation
                && root.Output.Count >= columns.Count
                && OutputFits(root.Output, origins, instances)
            )
        )
        && ReachesSetOperation(root)
            ? types
            : null;

    // Each column of no origin from the set operation's inputs, and each with an origin as the walk answers it: by
    // its entry at a root that prints one in a gated query, and kept otherwise.
    private static WalkOutcome RunSetOperation(
        PlanNode root,
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins,
        WalkScope scope,
        Plans plans,
        PlanInstances instances,
        IReadOnlyList<long> types
    )
    {
        var branches = new SetOperations(plans, scope);
        var onlyAlias = plans.OnlyAliasOf(root);
        var walked = new List<WalkedColumn>(columns.Count);
        for (var index = 0; index < columns.Count; index++)
        {
            var entry = EntryAt(root, index);
            walked.Add(
                origins[index] switch
                {
                    null when branches.NotNullAt(root, index, types[index]) => new WalkedColumn(
                        columns[index] with
                        {
                            Nullable = false,
                            NullableSource = SidecarValues.NullableSource.SetOperation,
                        },
                        entry,
                        null,
                        false,
                        false
                    ),
                    { } origin when scope.Gated && entry is not null => ColumnOf(
                        columns[index],
                        origin,
                        entry,
                        instances,
                        onlyAlias,
                        plans
                    ),
                    _ => Kept(columns[index], entry),
                }
            );
        }

        return new WalkOutcome(SidecarValues.Plan.Walked, null, instances.Nodes, walked);
    }

    // The children of a node that hold its rows: every one but an InitPlan or a SubPlan.
    private static PlanNode[] RowChildrenOf(PlanNode node) =>
        [.. node.Plans.Where(static child => child.ParentRelationship is not ("InitPlan" or "SubPlan"))];

    // The one child of a node that may pass it on: one Outer child, no join type, no grouping set, which puts NULL
    // in a key, and no ModifyTable, whose RETURNING list is of the row it wrote.
    private static PlanNode? OnlyChildOf(PlanNode node) =>
        node.JoinType is null
        && !node.HasGroupingSets
        && node.NodeType != "ModifyTable"
        && RowChildrenOf(node) is [{ ParentRelationship: Outer } child]
            ? child
            : null;

    // The child of a node that never projects, or null.
    private static PlanNode? NeverProjectedChildOf(PlanNode node) =>
        NeverProject.Contains(node.NodeType) ? OnlyChildOf(node) : null;

    // The members of an Append, or none when a child is anything else.
    private static PlanNode[] MembersOf(PlanNode append)
    {
        var children = RowChildrenOf(append);
        return children.All(static child => child.ParentRelationship == Member) ? children : [];
    }

    // What the node prints: its Output, or for an Append without one its first member's.
    private static IReadOnlyList<string>? PrintedBy(PlanNode node, int depth) =>
        node.Output
        ?? (
            Appends.Contains(node.NodeType) && depth < Plans.MaxDepth && MembersOf(node) is [var first, ..]
                ? PrintedBy(first, depth + 1)
                : null
        );

    // How a node reaches the column: whether its type is the row description's, which holds only above every set
    // operation and every cast a node printed, and whether the node is a SetOp's input, whose leaf scan the planner
    // built in the branch's order.
    private readonly record struct Reach(bool TypeIsTheColumns, bool InputOfASetOp)
    {
        public static Reach Root => new(true, false);

        public static Reach Branch => new(false, false);

        public static Reach Input => new(false, true);
    }

    // The answer of a set operation's column at a node: whether every row the node returns holds a value at the
    // ordinal that is never NULL and of a type that carries to the column's.  False means unknown, never nullable.
    private sealed class SetOperations(Plans plans, WalkScope scope)
    {
        public bool NotNullAt(PlanNode root, int ordinal, long type) => ColumnAt(root, ordinal, type, Reach.Root, 0);

        private bool ColumnAt(PlanNode node, int ordinal, long type, Reach reach, int depth)
        {
            if (depth > Plans.MaxDepth)
            {
                return false;
            }

            if (Appends.Contains(node.NodeType))
            {
                return MembersOf(node) is [_, ..] members
                    && members.All(member => ColumnAt(member, ordinal, type, Reach.Branch, depth + 1));
            }

            if (node.NodeType == SetOp)
            {
                return SetOpAt(node, ordinal, type, reach, depth);
            }

            if (node.NodeType == "Recursive Union")
            {
                return false;
            }

            if (NeverProjectedChildOf(node) is { } passed)
            {
                return ColumnAt(passed, ordinal, type, reach, depth);
            }

            if (PassedOn(node, ordinal, depth) is var (child, cast))
            {
                return ColumnAt(child, ordinal, type, new Reach(reach.TypeIsTheColumns && !cast, false), depth);
            }

            if (reach.InputOfASetOp && LeafBranchOf(node, ordinal) is { } branch)
            {
                return ColumnAt(branch, ordinal, type, Reach.Branch, depth + 1);
            }

            return ValueAt(node, ordinal, type);
        }

        // INTERSECT: an input that proves it; EXCEPT: its left input.  Below another set operation, every input.
        private bool SetOpAt(PlanNode node, int ordinal, long type, Reach reach, int depth)
        {
            var intersect = node.Command is "Intersect" or "Intersect All";
            if (!intersect && node.Command is not ("Except" or "Except All"))
            {
                return false;
            }

            if (InputsOf(node) is not var (inputs, left))
            {
                return false;
            }

            bool Proves(PlanNode input) => ColumnAt(input, ordinal, type, Reach.Input, depth + 1);
            if (!reach.TypeIsTheColumns)
            {
                return (intersect || left is not null) && inputs.All(Proves);
            }

            return intersect ? inputs.Any(Proves) : left is not null && Proves(left);
        }

        // The inputs of a SetOp and its left one: on 18 its Outer and its Inner child; on 14 the members of its one
        // Outer Append, through nodes that never project, each with its flag last, the left the one flagged 0.  The
        // left is null when the flags do not name one.
        private static (PlanNode[] Inputs, PlanNode? Left)? InputsOf(PlanNode node)
        {
            var children = RowChildrenOf(node);
            if (
                children is [_, _]
                && children.FirstOrDefault(static child => child.ParentRelationship == Outer) is { } outer
                && children.FirstOrDefault(static child => child.ParentRelationship == Inner) is { } inner
            )
            {
                return ([outer, inner], outer);
            }

            if (children is not [{ ParentRelationship: Outer } only])
            {
                return null;
            }

            var append = only;
            while (NeverProjectedChildOf(append) is { } under)
            {
                append = under;
            }

            if (append.NodeType != "Append" || MembersOf(append) is not [_, _, ..] members)
            {
                return null;
            }

            var flags = members.Select(static member => member.Output is [.., var last] ? last : null).ToArray();
            if (!flags.All(static flag => flag is "0" or "1"))
            {
                return null;
            }

            var left = members.Where((_, index) => flags[index] == "0").ToArray();
            return (members, left is [var one] ? one : null);
        }

        // The child of a node that projects, when its entry at the ordinal is the child's printed entry there, E,
        // or E as a value the child computed, (E), or that and its cast to the result's type, ((E)::T); and no other
        // entry of the child is any of those, nor the text the node printed, nor the text inside it.  Cast says the
        // third.
        private static (PlanNode Child, bool Cast)? PassedOn(PlanNode node, int ordinal, int depth)
        {
            if (
                OnlyChildOf(node) is not { } child
                || node.Output is not { } output
                || ordinal >= output.Count
                || PrintedBy(child, depth) is not { } printed
                || ordinal >= printed.Count
            )
            {
                return null;
            }

            var entry = output[ordinal];
            var value = printed[ordinal];
            var computed = "(" + value + ")";
            var unwrapped = PlanExpression.Unwrapped(entry);
            var operand = unwrapped is null ? null : PlanExpression.CastOperand(unwrapped);
            var cast =
                !string.Equals(entry, value, StringComparison.Ordinal)
                && !string.Equals(entry, computed, StringComparison.Ordinal);
            if (cast && !string.Equals(operand, value, StringComparison.Ordinal))
            {
                return null;
            }

            string?[] texts = [entry, value, computed, unwrapped, operand];
            for (var other = 0; other < printed.Count; other++)
            {
                if (other != ordinal && texts.Contains(printed[other], StringComparer.Ordinal))
                {
                    return null;
                }
            }

            return (child, cast);
        }

        // The branch of a set operation's own leaf scan, when its entry at the ordinal is a reference to its own
        // alias and the SQL cannot have written that alias.
        private PlanNode? LeafBranchOf(PlanNode node, int ordinal) =>
            !scope.SetOperationAliasMayBeWritten
            && node.NodeType == "Subquery Scan"
            && node.Alias is { } alias
            && IsLeafAlias(alias)
            && node.Output is { } output
            && ordinal < output.Count
            && OutputReference.Parse(output[ordinal]) is { Alias: { } named }
            && string.Equals(named, alias, StringComparison.Ordinal)
            && node.Plans.Where(static child => child.ParentRelationship == "Subquery").ToArray() is [var branch]
                ? branch
                : null;

        private static bool IsLeafAlias(string alias) =>
            alias.StartsWith(LeafScanPrefix, StringComparison.Ordinal)
            && alias.Length > LeafScanPrefix.Length
            && alias[LeafScanPrefix.Length..].All(char.IsAsciiDigit);

        // The node's entry read as the walk reads a root's: a plan that holds a set operation or that the walk
        // cannot use proves nothing.
        private bool ValueAt(PlanNode node, int ordinal, long type)
        {
            if (node.Output is not { } output || ordinal >= output.Count)
            {
                return false;
            }

            var instances = plans.InstancesOf(node);
            if (instances.Reason is not null || instances.HasSetOperation)
            {
                return false;
            }

            var expressions = new Expressions(scope, node, instances, plans.OnlyAliasOf(node));
            var entry = output[ordinal];
            if (OutputReference.Parse(entry) is { } reference)
            {
                return expressions.ProvedTypeOf(reference) is { } attribute
                    && SetOperationTypes.Carries(attribute, type);
            }

            return expressions.NullableOf(entry) == false
                && PlanExpression.TypeOf(entry, scope.CatalogFunctionsFirst) is { } constant
                && SetOperationTypes.Carries(constant, type);
        }
    }
}
