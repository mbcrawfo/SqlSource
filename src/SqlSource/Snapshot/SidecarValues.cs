namespace SqlSource.Snapshot;

/// <summary>
/// The values the format design lists, which the tool writes.  A reader keeps a provenance value it does not know,
/// so none of these lists is closed.
/// </summary>
internal static class SidecarValues
{
    /// <summary>The engines whose type shape this version knows.  They are the dialects' canonical names.</summary>
    public static class Engine
    {
        public const string Postgres = "postgres";
        public const string SqlServer = "mssql";
    }

    public static class ResultKind
    {
        public const string Rows = "rows";
        public const string None = "none";
    }

    public static class Plan
    {
        public const string NotNeeded = "not-needed";
        public const string Walked = "walked";
        public const string Unavailable = "unavailable";
        public const string Skipped = "skipped";
    }

    public static class TableMatch
    {
        public const string Matched = "matched";
        public const string NoOrigin = "no-origin";
        public const string SeveralTables = "several-tables";
        public const string NamesDiffer = "names-differ";
        public const string ColumnsDiffer = "columns-differ";
        public const string OrderDiffers = "order-differs";
        public const string NullabilityDiffers = "nullability-differs";
    }

    public static class TypeSource
    {
        public const string Inferred = "inferred";
        public const string InferredFromCopies = "inferred-from-copies";
        public const string Declared = "declared";
    }

    public static class NullableSource
    {
        public const string Server = "server";
        public const string Catalog = "catalog";
        public const string OuterJoin = "outer-join";
        public const string View = "view";
        public const string NoOrigin = "no-origin";
        public const string Heuristic = "heuristic";
    }

    /// <summary>The kinds of a PostgreSQL type.</summary>
    public static class Kind
    {
        public const string Base = "base";
        public const string Array = "array";
        public const string Domain = "domain";
        public const string Enum = "enum";
        public const string Range = "range";
        public const string Multirange = "multirange";
        public const string Composite = "composite";
    }
}
