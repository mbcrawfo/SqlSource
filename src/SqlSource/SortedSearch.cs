using System;

namespace SqlSource;

/// <summary>
/// The one exact binary search of a sorted list, by a key that each item gives.
/// </summary>
internal static class SortedSearch
{
    // How the item at an index sorts against the key: negative before it, zero equal, positive after.  A struct, so
    // that a search allocates nothing.
    private interface IProbe
    {
        int CompareAt(int index);
    }

    /// <summary>
    /// The index in <paramref name="sorted" />, which is ordered by <paramref name="comparer" /> on the key that
    /// <paramref name="keyOf" /> gives, of the item whose key equals <paramref name="key" /> by
    /// <paramref name="comparer" />.  Negative when there is none.
    /// </summary>
    public static int IndexOf<T>(EquatableArray<T> sorted, string key, Func<T, string> keyOf, StringComparer comparer)
        where T : IEquatable<T> => Search(sorted.Count, new ComparerProbe<T>(sorted, key, keyOf, comparer));

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
        where T : IEquatable<T> => Search(sorted.Count, new PrefixProbe<T>(sorted, text, keyLength, keyOf, comparison));

    private static int Search<TProbe>(int count, TProbe probe)
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
