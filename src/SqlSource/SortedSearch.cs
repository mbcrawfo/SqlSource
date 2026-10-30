using System;

namespace SqlSource;

/// <summary>
/// The one binary search of the generator's sorted lists, exact or a lower bound.  A caller with its own order gives a
/// probe.
/// </summary>
internal static class SortedSearch
{
    /// <summary>
    /// How the item at an index sorts against what is looked for.  An implementation is a <c>readonly struct</c>, so
    /// that a search allocates nothing.
    /// </summary>
    public interface IProbe
    {
        /// <summary>
        /// Negative when the item at <paramref name="index" /> sorts before what is looked for, zero when it is equal
        /// to it, positive when it sorts after.
        /// </summary>
        int CompareAt(int index);
    }

    /// <summary>
    /// The index in <paramref name="sorted" />, which is ordered by <paramref name="comparer" /> on the key that
    /// <paramref name="keyOf" /> gives, of the item whose key equals <paramref name="key" /> by
    /// <paramref name="comparer" />.  Negative when there is none.
    /// </summary>
    public static int IndexOf<T>(EquatableArray<T> sorted, string key, Func<T, string> keyOf, StringComparer comparer)
        where T : IEquatable<T> => IndexOf(sorted.Count, new ComparerProbe<T>(sorted, key, keyOf, comparer));

    /// <summary>
    /// The index in <paramref name="sorted" />, which is ordered by <paramref name="comparison" /> on the key that
    /// <paramref name="keyOf" /> gives, of the item whose key equals the first <paramref name="keyLength" />
    /// characters of <paramref name="text" /> by <paramref name="comparison" />.  Negative when there is none.  The key
    /// is compared in place, so a search for a part of a text allocates nothing.
    /// </summary>
    public static int IndexOf<T>(
        EquatableArray<T> sorted,
        string text,
        int keyLength,
        Func<T, string> keyOf,
        StringComparison comparison
    )
        where T : IEquatable<T> =>
        IndexOf(sorted.Count, new PrefixProbe<T>(sorted, text, keyLength, keyOf, comparison));

    /// <summary>
    /// An index in <c>[0, <paramref name="count" />)</c> whose <see cref="IProbe.CompareAt" /> is zero, or -1 when
    /// there is none.
    /// </summary>
    public static int IndexOf<TProbe>(int count, TProbe probe)
        where TProbe : struct, IProbe
    {
        var low = 0;
        var high = count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = probe.CompareAt(middle);
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

        return -1;
    }

    /// <summary>
    /// The first index in <c>[0, <paramref name="count" />)</c> whose <see cref="IProbe.CompareAt" /> is not
    /// negative, or <paramref name="count" /> when there is none.
    /// </summary>
    public static int LowerBound<TProbe>(int count, TProbe probe)
        where TProbe : struct, IProbe => LowerBound(0, count, probe);

    /// <summary>
    /// The first index in <c>[<paramref name="start" />, <paramref name="count" />)</c> whose
    /// <see cref="IProbe.CompareAt" /> is not negative, or <paramref name="count" /> when there is none, which is
    /// <paramref name="start" /> when it equals <paramref name="count" />.
    /// </summary>
    public static int LowerBound<TProbe>(int start, int count, TProbe probe)
        where TProbe : struct, IProbe
    {
        var low = start;
        var high = count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            if (probe.CompareAt(middle) < 0)
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

    private readonly struct ComparerProbe<T>(
        EquatableArray<T> sorted,
        string key,
        Func<T, string> keyOf,
        StringComparer comparer
    ) : IProbe
        where T : IEquatable<T>
    {
        public int CompareAt(int index) => comparer.Compare(keyOf(sorted[index]), key);
    }

    private readonly struct PrefixProbe<T>(
        EquatableArray<T> sorted,
        string text,
        int keyLength,
        Func<T, string> keyOf,
        StringComparison comparison
    ) : IProbe
        where T : IEquatable<T>
    {
        // string.Compare takes at most keyLength characters of each, so an item that starts with the key, and is
        // longer, compares equal to it and is then put after it by its length.
        public int CompareAt(int index)
        {
            var item = keyOf(sorted[index]);
            var order = string.Compare(item, 0, text, 0, keyLength, comparison);
            return order != 0 ? order : item.Length - keyLength;
        }
    }
}
