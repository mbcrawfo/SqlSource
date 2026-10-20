using System;
using System.Collections.Frozen;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>The values of <c>Node Type</c> the walk compares, as <c>EXPLAIN (FORMAT JSON)</c> writes them.</summary>
internal static class PlanNodeTypes
{
    public const string Append = "Append";
    public const string MergeAppend = "Merge Append";
    public const string SetOp = "SetOp";
    public const string RecursiveUnion = "Recursive Union";
    public const string SubqueryScan = "Subquery Scan";
    public const string CteScan = "CTE Scan";
    public const string ModifyTable = "ModifyTable";
    public const string Aggregate = "Aggregate";

    public const string SeqScan = "Seq Scan";
    public const string IndexScan = "Index Scan";
    public const string IndexOnlyScan = "Index Only Scan";
    public const string BitmapHeapScan = "Bitmap Heap Scan";
    public const string TidScan = "Tid Scan";
    public const string TidRangeScan = "Tid Range Scan";
    public const string SampleScan = "Sample Scan";
    public const string ForeignScan = "Foreign Scan";

    public const string Sort = "Sort";
    public const string IncrementalSort = "Incremental Sort";
    public const string Limit = "Limit";
    public const string Unique = "Unique";
    public const string Materialize = "Materialize";

    /// <summary>
    /// The nodes of an <c>Append</c>, ordered or not: of a <c>UNION</c>, of a partitioned table's partitions, or of
    /// a SetOp's inputs on PostgreSQL 14.
    /// </summary>
    public static readonly FrozenSet<string> Appends = FrozenSet.ToFrozenSet(
        [Append, MergeAppend],
        StringComparer.Ordinal
    );
}

/// <summary>The values of <c>Parent Relationship</c>.</summary>
internal static class PlanRelationships
{
    public const string Outer = "Outer";
    public const string Inner = "Inner";
    public const string Member = "Member";
    public const string Subquery = "Subquery";
    public const string InitPlan = "InitPlan";
    public const string SubPlan = "SubPlan";
}

/// <summary>The values of <c>Strategy</c> of an <c>Aggregate</c>.</summary>
internal static class AggregateStrategies
{
    public const string Plain = "Plain";
}

/// <summary>The values of <c>Join Type</c>.</summary>
internal static class PlanJoinTypes
{
    public const string Inner = "Inner";
    public const string Left = "Left";
    public const string Right = "Right";
    public const string Full = "Full";
    public const string Semi = "Semi";
    public const string Anti = "Anti";
    public const string RightSemi = "Right Semi";
    public const string RightAnti = "Right Anti";
}

/// <summary>The values of <c>Command</c>, which a <c>SetOp</c> alone has.</summary>
internal static class SetOperationCommands
{
    public const string Intersect = "Intersect";
    public const string IntersectAll = "Intersect All";
    public const string Except = "Except";
    public const string ExceptAll = "Except All";
}
