using System;

namespace SqlSource;

/// <summary>
/// The one binary search of a sorted list, by a key that each item gives.
/// </summary>
internal static class SortedSearch
{
    /// <summary>
    /// The index in <paramref name="sorted" />, which is ordered by <paramref name="comparer" /> on the key that
    /// <paramref name="keyOf" /> gives, of the item whose key equals <paramref name="key" /> by
    /// <paramref name="comparer" />.  Negative when there is none.
    /// </summary>
    public static int IndexOf<T>(EquatableArray<T> sorted, string key, Func<T, string> keyOf, StringComparer comparer)
        where T : IEquatable<T>
    {
        var low = 0;
        var high = sorted.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = comparer.Compare(keyOf(sorted[middle]), key);
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
}
