using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Postgres.Columns;

namespace SqlSource.Tool.Describing.Postgres.Walk;

internal static partial class NullabilityWalk
{
    // A column of no origin at the top of a set operation, a UNION, an INTERSECT or an EXCEPT that the root reaches
    // through nodes that pass a child's column on, is answered from the set operation's inputs: false with
    // set-operation when the inputs the operation returns rows of prove it, and as it was built otherwise.  A UNION's
    // Append returns every member's rows, and so does any Append, a partitioned table's or one nested in another: every
    // member must prove it, and an Append that partition pruning removed a member of proves nothing, since the rows of
    // another execution may come from that one.  An INTERSECT returns rows that both inputs hold, as NULL equals NULL
    // there: one input proves it.  An EXCEPT returns rows of its left input: that one proves it.  PostgreSQL 14 plans
    // the last two as a SetOp over an Append whose members print a flag last, 0 for the left input and 1 for the right;
    // 18 as a SetOp with the left input as its Outer child and the right as its Inner.  A recursive CTE's union, and
    // any other shape, proves nothing.
    //
    // A branch's value counts only when its type carries to the result's (SetOperationTypes): the planner casts a
    // branch to the result's type, and the plan does not print the cast.  The result's type is the row description's,
    // which is a set operation's own type only at the top and where no node above prints a cast of its value: below
    // another set operation, or under a node that prints ((E)::T), an INTERSECT or an EXCEPT is of the common type of
    // its own inputs, which may be the type of an input that proves nothing, reached by a user's cast.  There it
    // proves the column only when each of its inputs does, and so each is of the result's family.
    //
    // A leaf scan of a set operation, a Subquery Scan aliased *SELECT* <k>, prints a reference to a column of its
    // branch by the column's name, which the plan does not otherwise print, and only its place says which.  It is
    // followed by place only when it is itself a member of a 14 SetOp's Append with a flag last: the flag makes the
    // planner's own leaf scan, built one entry for each column of the branch in order, and keeps it, so the member is
    // that scan.  Nowhere else does the alias prove it.  Under an Append the planner may have pulled a UNION ALL that a
    // subquery in FROM, a CTE or an inlined SQL function holds up into its query, and a branch it keeps as such a
    // scan prints the columns that query needs, in its order: SELECT s.b, s.a FROM (SELECT p.sku AS a, p.name AS b
    // ... UNION ALL (SELECT p2.sku, NULLIF(p2.name, 'x') ... LIMIT 5)) s prints "*SELECT* 2"."nullif",
    // "*SELECT* 2".sku over a branch that prints p2.sku first.  On 18 the planner drops its own leaf scan when it
    // adds nothing, and a SetOp's input may be a user's subquery of that very name, through a view, an inlined SQL
    // function or a U&"..." identifier the SQL does not show, which prints its columns in the order its query needs.
    // A constant the planner put in the scan's place is read where it is.
    //
    // Any other node that prints the value is walked as a plan of its own, as the walk walks the root: a reference by
    // the catalog's answer of an instance that no join of that plan extends, and an expression by PlanExpression.  A
    // set operation under a join, or one that a node above computes over, keeps its answer: the node is walked as a
    // plan, which holds a set operation.

    private const string LeafScanPrefix = "*SELECT* ";
    private const string LeftFlag = "0";
    private const string RightFlag = "1";

    // Nodes that never project: the row is the child's, and so is each column.
    private static readonly HashSet<string> NeverProject =
    [
        with(StringComparer.Ordinal),
        PlanNodeTypes.Sort,
        PlanNodeTypes.IncrementalSort,
        PlanNodeTypes.Limit,
        PlanNodeTypes.Unique,
        PlanNodeTypes.Materialize,
    ];

    /// <summary>
    /// Whether the root reaches an <c>Append</c>, a <c>Merge Append</c> or a <c>SetOp</c> through nodes that may pass
    /// a child's column on: a node with one <c>Outer</c> child, no join type, no grouping set, and that is no
    /// <c>ModifyTable</c>.  An <c>InitPlan</c> or a <c>SubPlan</c> computes a value and is no child of the row.
    /// </summary>
    public static bool ReachesSetOperation(PlanNode root)
    {
        for (var node = root; node is not null; node = OnlyChildOf(node))
        {
            if (PlanNodeTypes.Appends.Contains(node.NodeType) || node.NodeType == PlanNodeTypes.SetOp)
            {
                return true;
            }
        }

        return false;
    }

    // The types to answer a set operation's columns with, when the walk takes that path: a column has no origin, there
    // is a type for each column, the root prints no Output or its plan holds a set operation, and the root reaches
    // one.  A root that prints Output must print an entry for each column, and one of another statement's plan is read
    // for none.
    private static IReadOnlyList<long>? SetOperationTypesOf(
        Plans plans,
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins
    )
    {
        var root = plans.Tree;
        var types = plans.Scope.ColumnTypes;
        var instances = plans.InstancesOf(root);
        return
            types.Count == columns.Count
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
    }

    // Each column of no origin from the set operation's inputs, and each with an origin as the walk answers it: by
    // its entry at a root that prints one in a gated query, and kept otherwise.  A root Append that is a partitioned
    // table's (PlanPartitions), where the plan holds no set operation, is a union of its partitions all the same; its
    // answer is an expression's, since the SQL holds no set operation.
    private static WalkOutcome RunSetOperation(
        Plans plans,
        IReadOnlyList<SidecarColumn> columns,
        IReadOnlyList<ColumnOrigin?> origins,
        IReadOnlyList<long> types
    )
    {
        var root = plans.Tree;
        var scope = plans.Scope;
        var instances = plans.InstancesOf(root);
        var branches = new SetOperations(plans);
        var onlyAlias = plans.OnlyAliasOf(root);
        var source = instances.HasSetOperation
            ? SidecarValues.NullableSource.SetOperation
            : SidecarValues.NullableSource.Expression;
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
                            NullableSource = source,
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
        [
            .. node.Plans.Where(static child =>
                child.ParentRelationship is not (PlanRelationships.InitPlan or PlanRelationships.SubPlan)
            ),
        ];

    // The one child of a node that may pass it on: one Outer child, no join type, no grouping set, which puts NULL
    // in a key, and no ModifyTable, whose RETURNING list is of the row it wrote.
    private static PlanNode? OnlyChildOf(PlanNode node) =>
        node.JoinType is null
        && !node.HasGroupingSets
        && node.NodeType != PlanNodeTypes.ModifyTable
        && RowChildrenOf(node) is [{ ParentRelationship: PlanRelationships.Outer } child]
            ? child
            : null;

    // The child of a node that never projects, or null.
    private static PlanNode? NeverProjectedChildOf(PlanNode node) =>
        NeverProject.Contains(node.NodeType) ? OnlyChildOf(node) : null;

    // The members of an Append, or none when a child is anything else.
    private static PlanNode[] MemberNodesOf(PlanNode append)
    {
        var children = RowChildrenOf(append);
        return children.All(static child => child.ParentRelationship == PlanRelationships.Member) ? children : [];
    }

    // What the node prints: its Output, or for an Append without one its first member's.
    private static IReadOnlyList<string>? PrintedBy(PlanNode node, int depth) =>
        node.Output
        ?? (
            PlanNodeTypes.Appends.Contains(node.NodeType)
            && depth < Plans.MaxDepth
            && MemberNodesOf(node) is [var first, ..]
                ? PrintedBy(first, depth + 1)
                : null
        );

    // How a node reaches the column: whether its type is the row description's, which holds only above every set
    // operation and every cast a node printed, and whether the node is itself a flagged member of a SetOp's Append on
    // 14, the leaf scan the planner built in the branch's order.
    private readonly record struct Reach(bool TypeIsTheColumns, bool FlaggedMember)
    {
        public static Reach Root => new(true, false);

        public static Reach Branch => new(false, false);

        public static Reach Flagged => new(false, true);
    }

    // The answer of a set operation's column at a node: whether every row the node returns holds a value at the
    // ordinal that is never NULL and of a type that carries to the column's.  False means unknown, never nullable.
    private sealed class SetOperations(Plans plans)
    {
        public bool NotNullAt(PlanNode root, int ordinal, long type) => ColumnAt(root, ordinal, type, Reach.Root, 0);

        private bool ColumnAt(PlanNode node, int ordinal, long type, Reach reach, int depth)
        {
            if (depth > Plans.MaxDepth)
            {
                return false;
            }

            if (PlanNodeTypes.Appends.Contains(node.NodeType))
            {
                return node.SubplansRemoved == 0
                    && MemberNodesOf(node) is [_, ..] members
                    && members.All(member => ColumnAt(member, ordinal, type, Reach.Branch, depth + 1));
            }

            if (node.NodeType == PlanNodeTypes.SetOp)
            {
                return SetOpAt(node, ordinal, type, reach, depth);
            }

            if (node.NodeType == PlanNodeTypes.RecursiveUnion)
            {
                return false;
            }

            if (NeverProjectedChildOf(node) is { } passed)
            {
                return ColumnAt(passed, ordinal, type, reach with { FlaggedMember = false }, depth);
            }

            if (PassedOn(node, ordinal, depth) is var (child, cast))
            {
                return ColumnAt(child, ordinal, type, new Reach(reach.TypeIsTheColumns && !cast, false), depth);
            }

            if (reach.FlaggedMember && LeafBranchOf(node, ordinal) is { } branch)
            {
                return ColumnAt(branch, ordinal, type, Reach.Branch, depth + 1);
            }

            return ValueAt(node, ordinal, type);
        }

        // INTERSECT: an input that proves it; EXCEPT: its left input.  Below another set operation, every input.
        private bool SetOpAt(PlanNode node, int ordinal, long type, Reach reach, int depth)
        {
            var intersect = node.Command is SetOperationCommands.Intersect or SetOperationCommands.IntersectAll;
            if (!intersect && node.Command is not (SetOperationCommands.Except or SetOperationCommands.ExceptAll))
            {
                return false;
            }

            if (InputsOf(node) is not var (inputs, left, flagged))
            {
                return false;
            }

            bool Proves(PlanNode input) =>
                ColumnAt(input, ordinal, type, flagged ? Reach.Flagged : Reach.Branch, depth + 1);
            if (!reach.TypeIsTheColumns)
            {
                return inputs.All(Proves);
            }

            return intersect ? inputs.Any(Proves) : Proves(left);
        }

        // The inputs of a SetOp, its left one, and whether they are flagged members: on 18 its Outer and its Inner
        // child; on 14 the two members of its one Outer Append, through nodes that never project, each with its flag
        // last, the left flagged 0 and the right 1.
        private static (PlanNode[] Inputs, PlanNode Left, bool Flagged)? InputsOf(PlanNode node)
        {
            var children = RowChildrenOf(node);
            if (
                children is [_, _]
                && children.FirstOrDefault(static child => child.ParentRelationship == PlanRelationships.Outer)
                    is { } outer
                && children.FirstOrDefault(static child => child.ParentRelationship == PlanRelationships.Inner)
                    is { } inner
            )
            {
                return ([outer, inner], outer, false);
            }

            if (children is not [{ ParentRelationship: PlanRelationships.Outer } only])
            {
                return null;
            }

            var append = only;
            while (NeverProjectedChildOf(append) is { } under)
            {
                append = under;
            }

            if (append.NodeType != PlanNodeTypes.Append || MemberNodesOf(append) is not [var first, var second])
            {
                return null;
            }

            return (FlagOf(first), FlagOf(second)) switch
            {
                (LeftFlag, RightFlag) => ([first, second], first, true),
                (RightFlag, LeftFlag) => ([first, second], second, true),
                _ => null,
            };
        }

        private static string? FlagOf(PlanNode member) => member.Output is [.., var last] ? last : null;

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

        // The branch of a flagged member that is a set operation's own leaf scan, when its entry at the ordinal is a
        // reference to its own alias.
        private static PlanNode? LeafBranchOf(PlanNode node, int ordinal) =>
            node.Alias is { } alias
            && IsLeafAlias(alias)
            && node.Output is { } output
            && ordinal < output.Count
            && OutputReference.Parse(output[ordinal]) is { Alias: { } named }
            && string.Equals(named, alias, StringComparison.Ordinal)
            && node.SubqueryChild is { } branch
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

            var expressions = new Expressions(plans, node, instances, plans.OnlyAliasOf(node));
            var entry = output[ordinal];
            if (OutputReference.Parse(entry) is { } reference)
            {
                return expressions.ProvedTypeOf(reference) is { } attribute
                    && SetOperationTypes.Carries(attribute, type);
            }

            return expressions.NullableOf(entry, TotalFunctions.UnknownType) == false
                && PlanExpression.TypeOf(entry, plans.Scope.CatalogFunctionsFirst) is { } constant
                && SetOperationTypes.Carries(constant, type);
        }
    }
}
