using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The built-in types the describer names: the OID of each that it names by number, and the set of those that carry
/// a modifier, by their internal name.
/// </summary>
/// <remarks>
/// An OID of <c>pg_catalog</c> is fixed for every version of PostgreSQL.  Each constant's name, lower-cased, is its
/// type's <c>typname</c>, and <c>Catalog_BuiltInTypes_AreNamedSo</c> in <c>PlanOutputTests</c> holds every one to
/// <c>pg_type</c> by that.
/// </remarks>
internal static class BuiltInTypes
{
    public const long Bool = 16;
    public const long Int8 = 20;
    public const long Int2 = 21;
    public const long Int2vector = 22;
    public const long Int4 = 23;
    public const long Text = 25;
    public const long Oidvector = 30;
    public const long Float4 = 700;
    public const long Float8 = 701;
    public const long Bpchar = 1042;
    public const long Varchar = 1043;
    public const long Date = 1082;
    public const long Timestamp = 1114;
    public const long Timestamptz = 1184;
    public const long Numeric = 1700;

    /// <summary>
    /// The internal names of the types that carry a modifier: every built-in type with a typmod input.
    /// <see cref="TypeModifiers" />, <see cref="ColumnFacets" /> and <see cref="PostgresTypeNames" /> each handle
    /// every one in a switch of their own, and <c>BuiltInTypesTests</c> fails for a member one of them leaves out.
    /// <c>Catalog_TypesWithAModifier_AreTheBuiltInOnesWithATypmodInput</c> in <c>PlanOutputTests</c> holds the set to
    /// <c>pg_type</c>.
    /// </summary>
    public static IReadOnlyList<string> WithModifier { get; } =
    ["bpchar", "varchar", "bit", "varbit", "numeric", "time", "timetz", "timestamp", "timestamptz", "interval"];
}
