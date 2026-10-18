using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SqlSource.Settings;

/// <summary>
/// The rules of a map of type overrides: an array of entries sorted by key, ignoring case, with each key once.
/// </summary>
/// <remarks>
/// The levels merge per key, the exception to the rule that a level replaces the one below it whole: a level
/// replaces only the keys it names.  An entry of <c>default</c> stays in a merged map, so that it hides the key of
/// a level merged under it later, and only the map in effect leaves it out.
/// </remarks>
internal static class TypeOverrideMap
{
    private static readonly StringComparer Keys = StringComparer.OrdinalIgnoreCase;

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
            var order = Keys.Compare(over[high].Key, under[low].Key);
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

    /// <summary>The entry for <paramref name="key" />, ignoring case, or null.</summary>
    public static TypeOverride? Find(EquatableArray<TypeOverride> map, string key) =>
        Search(map, key) is var at and >= 0 ? map[at] : null;

    /// <summary>
    /// Where <paramref name="key" /> is in a list kept sorted by key, or the complement of where it would go, as
    /// <see cref="List{T}.BinarySearch(T)" /> gives it.
    /// </summary>
    public static int IndexOf(List<TypeOverride> entries, string key) => Search(entries, key);

    // Generic over the list, so that a map, which is a struct, is searched without being boxed.
    private static int Search<TList>(TList entries, string key)
        where TList : IReadOnlyList<TypeOverride>
    {
        var (low, high) = (0, entries.Count - 1);
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = Keys.Compare(entries[middle].Key, key);
            if (order == 0)
            {
                return middle;
            }

            if (order < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        return ~low;
    }
}
