using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SqlSource.Settings;

/// <summary>
/// The rules of a map of type overrides: an array of entries sorted by key, with each key once.  Keys are compared by
/// their parts, as <see cref="TypeKeys.TrySplit" /> gives them, and by the text of a part, so that <c>"a.b".c</c> and
/// <c>a."b.c"</c> are two keys and <c>public."status"</c> and <c>public.status</c> are one.  The order is that of the
/// keys ignoring case, then that of the keys exactly, so that keys that differ only by case are two keys and sit
/// together.
/// </summary>
/// <remarks>
/// The levels merge per key, the exception to the rule that a level replaces the one below it whole: a level
/// replaces only the keys it names.  An entry of <c>default</c> stays in a merged map, so that it hides the key of
/// a level merged under it later, and only the map in effect leaves it out.
/// </remarks>
internal static class TypeOverrideMap
{
    /// <summary>
    /// <paramref name="higher" /> over <paramref name="lower" />: every key of either, with the entry of
    /// <paramref name="higher" /> for a key both have.  Null when both are, and either one itself when the other is.
    /// </summary>
    public static EquatableArray<TypeOverride>? Over(
        EquatableArray<TypeOverride>? higher,
        EquatableArray<TypeOverride>? lower
    )
    {
        if (higher is not { } over)
        {
            return lower;
        }

        if (lower is not { } under)
        {
            return over;
        }

        var merged = ImmutableArray.CreateBuilder<TypeOverride>(over.Count + under.Count);
        var (high, low) = (0, 0);
        while (high < over.Count && low < under.Count)
        {
            var order = Order(over[high].Key, under[low].Key);
            merged.Add(order <= 0 ? over[high] : under[low]);
            high += order <= 0 ? 1 : 0;
            low += order >= 0 ? 1 : 0;
        }

        for (; high < over.Count; high++)
        {
            merged.Add(over[high]);
        }

        for (; low < under.Count; low++)
        {
            merged.Add(under[low]);
        }

        return new EquatableArray<TypeOverride>(merged.ToImmutable());
    }

    /// <summary>The map in effect: <paramref name="merged" /> without its entries of <c>default</c>.</summary>
    public static EquatableArray<TypeOverride> InEffect(EquatableArray<TypeOverride>? merged)
    {
        if (merged is not { } map)
        {
            return EquatableArray<TypeOverride>.Empty;
        }

        var kept = 0;
        foreach (var entry in map)
        {
            kept += entry.Type is null ? 0 : 1;
        }

        if (kept == map.Count)
        {
            return map;
        }

        var inEffect = ImmutableArray.CreateBuilder<TypeOverride>(kept);
        for (var index = 0; index < map.Count; index++)
        {
            if (map[index].Type is not null)
            {
                inEffect.Add(map[index]);
            }
        }

        return new EquatableArray<TypeOverride>(inEffect.MoveToImmutable());
    }

    /// <summary>
    /// The entry for <paramref name="key" />, a type's key as it is written, or null: the entry whose key is equal
    /// to it exactly or, failing that, the first in the order of the map whose key is equal to it ignoring case and
    /// whose quoted parts are equal to the key's exactly.
    /// </summary>
    public static TypeOverride? Find(EquatableArray<TypeOverride> map, string key)
    {
        var at = LowerBound(map, key, ignoringCaseOnly: true);
        return at < map.Count ? BestOfEqualIgnoringCase(map, at, key) : null;
    }

    /// <summary>
    /// Where <paramref name="key" /> is in a list kept sorted by key, or the complement of where it would go, as
    /// <see cref="List{T}.BinarySearch(T)" /> gives it.
    /// </summary>
    public static int IndexOf(List<TypeOverride> entries, string key)
    {
        var at = LowerBound(entries, key, ignoringCaseOnly: false);
        return at < entries.Count && Order(entries[at].Key, key) == 0 ? at : ~at;
    }

    // Walks the entries from at, the first that is not less than key ignoring case, while they are equal to it ignoring
    // case.  An entry equal to key exactly wins, and failing that the first whose quoted parts are equal to key's.
    private static TypeOverride? BestOfEqualIgnoringCase(EquatableArray<TypeOverride> map, int at, string key)
    {
        TypeOverride? match = null;
        for (; at < map.Count && Compare(map[at].Key, key, StringComparison.OrdinalIgnoreCase) == 0; at++)
        {
            if (Compare(map[at].Key, key, StringComparison.Ordinal) == 0)
            {
                return map[at];
            }

            if (match is null && QuotedPartsAreEqual(map[at].Key, key))
            {
                match = map[at];
            }
        }

        return match;
    }

    // Whether each part of entryKey that is quoted has the text of the same part of key exactly.  The two are equal
    // ignoring case, so they have the same parts.
    private static bool QuotedPartsAreEqual(string entryKey, string key)
    {
        if (
            !TypeKeys.TrySplit(entryKey.AsSpan(), out var entrySchema, out var entryName, out _)
            || !TypeKeys.TrySplit(key.AsSpan(), out var schema, out var name, out _)
        )
        {
            return true;
        }

        return PartMatches(entrySchema, schema) && PartMatches(entryName, name);
    }

    private static bool PartMatches(ReadOnlySpan<char> entryPart, ReadOnlySpan<char> part) =>
        !TypeKeys.IsQuoted(entryPart) || TypeKeys.TextOf(entryPart).SequenceEqual(TypeKeys.TextOf(part));

    // The order of the map: ignoring case, then exactly to break a tie.
    private static int Order(string a, string b)
    {
        var order = Compare(a, b, StringComparison.OrdinalIgnoreCase);
        return order != 0 ? order : Compare(a, b, StringComparison.Ordinal);
    }

    // Compares the keys as lists of their parts, [name] or [schema, name], lexicographically by the text of each part,
    // a shorter list first, and then a key that is not an array before one that is.  A key that is not well formed
    // does not reach the map, and would be compared as a whole.
    private static int Compare(string a, string b, StringComparison comparison)
    {
        if (
            !TypeKeys.TrySplit(a.AsSpan(), out var schemaA, out var nameA, out var isArrayA)
            || !TypeKeys.TrySplit(b.AsSpan(), out var schemaB, out var nameB, out var isArrayB)
        )
        {
            return string.Compare(a, b, comparison);
        }

        var order = ComparePart(schemaA.IsEmpty ? nameA : schemaA, schemaB.IsEmpty ? nameB : schemaB, comparison);
        if (order == 0 && (schemaA.IsEmpty || schemaB.IsEmpty))
        {
            order = schemaB.IsEmpty.CompareTo(schemaA.IsEmpty);
        }
        else if (order == 0)
        {
            order = ComparePart(nameA, nameB, comparison);
        }

        return order != 0 ? order : isArrayA.CompareTo(isArrayB);
    }

    private static int ComparePart(ReadOnlySpan<char> a, ReadOnlySpan<char> b, StringComparison comparison) =>
        TypeKeys.TextOf(a).CompareTo(TypeKeys.TextOf(b), comparison);

    // The index of the first entry that is not less than key, in the order of the map or, with ignoringCaseOnly, in
    // the order of the keys ignoring case.  Generic over the list, so that a map, which is a struct, is searched
    // without being boxed.
    private static int LowerBound<TList>(TList entries, string key, bool ignoringCaseOnly)
        where TList : IReadOnlyList<TypeOverride>
    {
        var (low, high) = (0, entries.Count);
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            var order = ignoringCaseOnly
                ? Compare(entries[middle].Key, key, StringComparison.OrdinalIgnoreCase)
                : Order(entries[middle].Key, key);
            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low;
    }
}
