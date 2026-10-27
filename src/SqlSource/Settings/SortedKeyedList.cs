using System;
using System.Collections.Generic;
using System.Collections.Immutable;

namespace SqlSource.Settings;

/// <summary>
/// The rules that the type override and the column types share for their entries: a list kept sorted by key, each key
/// once, in the order a map gives as <c>order</c>, the key of an entry being <c>keyOf</c>.  The maps keep their own
/// keys, order and lookup, <see cref="TypeOverrides.TypeOverrideMap" /> and <see cref="ColumnTypes.ColumnTypeMap" />,
/// and pass their order and key here as static delegates, so a call allocates nothing for them.
/// </summary>
internal static class SortedKeyedList
{
    /// <summary>
    /// <paramref name="higher" /> over <paramref name="lower" />, both sorted: every key of either, with the entry of
    /// <paramref name="higher" /> for a key both have.  Null when both are, and either one itself when the other is.
    /// </summary>
    public static EquatableArray<T>? Over<T>(
        EquatableArray<T>? higher,
        EquatableArray<T>? lower,
        Comparison<string> order,
        Func<T, string> keyOf
    )
        where T : IEquatable<T>
    {
        if (higher is not { } over)
        {
            return lower;
        }

        if (lower is not { } under)
        {
            return over;
        }

        var merged = ImmutableArray.CreateBuilder<T>(over.Count + under.Count);
        var (high, low) = (0, 0);
        while (high < over.Count && low < under.Count)
        {
            var compared = order(keyOf(over[high]), keyOf(under[low]));
            merged.Add(compared <= 0 ? over[high] : under[low]);
            high += compared <= 0 ? 1 : 0;
            low += compared >= 0 ? 1 : 0;
        }

        for (; high < over.Count; high++)
        {
            merged.Add(over[high]);
        }

        for (; low < under.Count; low++)
        {
            merged.Add(under[low]);
        }

        return new EquatableArray<T>(merged.ToImmutable());
    }

    /// <summary>
    /// Where <paramref name="key" /> is in a sorted list, or the complement of where it would go, as
    /// <see cref="List{T}.BinarySearch(T)" /> gives it.
    /// </summary>
    public static int IndexOf<T>(List<T> entries, string key, Comparison<string> order, Func<T, string> keyOf)
    {
        var (low, high) = (0, entries.Count);
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (order(keyOf(entries[middle]), key) < 0)
            {
                low = middle + 1;
            }
            else
            {
                high = middle;
            }
        }

        return low < entries.Count && order(keyOf(entries[low]), key) == 0 ? low : ~low;
    }

    /// <summary>
    /// Adds <paramref name="entry" /> in its place in a sorted list and gives null, or, when the list has an entry of
    /// its key, gives that entry and leaves the list as it is: a marker's entries add up, and the first of a key
    /// stands.
    /// </summary>
    public static T? AddOrFind<T>(List<T> entries, T entry, Comparison<string> order, Func<T, string> keyOf)
        where T : class
    {
        var at = IndexOf(entries, keyOf(entry), order, keyOf);
        if (at >= 0)
        {
            return entries[at];
        }

        entries.Insert(~at, entry);
        return null;
    }
}
