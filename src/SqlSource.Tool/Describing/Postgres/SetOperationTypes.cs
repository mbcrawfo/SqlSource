using System.Collections.Frozen;
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.Postgres;

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
/// in <c>PlanOutputTests</c> pins that every ordered pair of a family has such a row on the servers the tool
/// supports.
/// </para>
/// </remarks>
internal static class SetOperationTypes
{
    // int2, int4, int8, numeric, float4, float8; text, varchar, bpchar; date, timestamp, timestamptz.
    private static readonly FrozenDictionary<long, int> FamilyOf = new Dictionary<long, int>
    {
        [21] = 1,
        [23] = 1,
        [20] = 1,
        [1700] = 1,
        [700] = 1,
        [701] = 1,
        [25] = 2,
        [1043] = 2,
        [1042] = 2,
        [1082] = 3,
        [1114] = 3,
        [1184] = 3,
    }.ToFrozenDictionary();

    /// <summary>
    /// Whether a value of the type <paramref name="from" /> reaches a column of the type <paramref name="to" /> with
    /// no cast that can return <c>NULL</c> for it: the types are equal, or both are in one family.
    /// </summary>
    public static bool Carries(long from, long to) =>
        from == to
        || (FamilyOf.TryGetValue(from, out var family) && FamilyOf.TryGetValue(to, out var other) && family == other);
}
