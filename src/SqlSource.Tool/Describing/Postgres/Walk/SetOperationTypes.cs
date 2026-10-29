using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// Whether a branch of a set operation hands its value to the result's column with no cast that could return
/// <c>NULL</c> for it.
/// </summary>
/// <remarks>
/// <para>
/// The planner coerces a branch to the result's type with an implicit cast that the plan does not print at the
/// branch: <c>SELECT u.id … UNION ALL SELECT o.id …</c> reads <c>integer</c> into a <c>bigint</c> column, and the
/// branch prints <c>"*SELECT* 1".id</c> bare.  A user's cast, or a cast of a domain, may return <c>NULL</c> for a
/// value that is not, and the plan would not show it.  So a value counts only when its type is the result's, or both
/// are in one family of built-in types in which every ordered pair has a cast the server ships.  A type that is in
/// no family carries only to itself.
/// </para>
/// <para>
/// <c>pg_cast</c> holds one row for a pair, so no user can add another cast between two of these types, and none of
/// the built-in casts returns <c>NULL</c> for a value that is not.  <c>Catalog_CastFamilies_AreBuiltInBothWays</c>
/// in <c>CatalogPinTests</c> pins that every ordered pair of a family has such a row on the servers the tool
/// supports.
/// </para>
/// </remarks>
internal static class SetOperationTypes
{
    /// <summary>
    /// The families: number, text and time.  <c>Catalog_CastFamilies_AreBuiltInBothWays</c> reads them from here.
    /// </summary>
    internal static IReadOnlyList<IReadOnlyList<long>> Families { get; } =
    [
        [
            BuiltInTypes.Int2,
            BuiltInTypes.Int4,
            BuiltInTypes.Int8,
            BuiltInTypes.Numeric,
            BuiltInTypes.Float4,
            BuiltInTypes.Float8,
        ],
        [BuiltInTypes.Text, BuiltInTypes.Varchar, BuiltInTypes.Bpchar],
        [BuiltInTypes.Date, BuiltInTypes.Timestamp, BuiltInTypes.Timestamptz],
    ];

    // Each type of a family, with the family's index.
    private static readonly FrozenDictionary<long, int> FamilyOf = Families
        .SelectMany(static (family, index) => family.Select(oid => (oid, index)))
        .ToFrozenDictionary(static pair => pair.oid, static pair => pair.index);

    /// <summary>
    /// Whether a value of the type <paramref name="from" /> reaches a column of the type <paramref name="to" /> with
    /// no cast that can return <c>NULL</c> for it: the types are equal, or both are in one family.
    /// </summary>
    public static bool Carries(long from, long to) =>
        from == to
        || (FamilyOf.TryGetValue(from, out var family) && FamilyOf.TryGetValue(to, out var other) && family == other);

    /// <summary>Whether the type is in one of the <see cref="Families" />.</summary>
    public static bool InFamily(long type) => FamilyOf.ContainsKey(type);
}
