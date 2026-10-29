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
        var probe = new KeyProbe<T>(entries, key, order, keyOf);
        var at = SortedSearch.LowerBound(entries.Count, probe);
        return at < entries.Count && probe.CompareAt(at) == 0 ? at : ~at;
    }

    /// <summary>
    /// The order of a map whose keys compare by their parts, as <paramref name="compare" /> compares them under a
    /// <see cref="StringComparison" />: ignoring case, then exactly to break a tie, so that keys that differ only by
    /// case are two keys.  Build it once, at a static field, so that a call allocates nothing for it.
    /// </summary>
    public static Comparison<string> OrderOf(Func<string, string, StringComparison, int> compare) =>
        (a, b) =>
        {
            var order = compare(a, b, StringComparison.OrdinalIgnoreCase);
            return order != 0 ? order : compare(a, b, StringComparison.Ordinal);
        };

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

    // A struct, so that SortedSearch does not box it.
    private readonly struct KeyProbe<T>(List<T> entries, string key, Comparison<string> order, Func<T, string> keyOf)
        : SortedSearch.IProbe
    {
        public int CompareAt(int index) => order(keyOf(entries[index]), key);
    }
}
