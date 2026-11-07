using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.DatabaseTests.TestHarness.EndToEnd;

// The lists that the list tests of every engine send: a collection of each kind, and counts of ids around the seed's.
internal static class ListArguments
{
    // The first of the ids that no account has.
    public const int Unused = 1000;

    // The values in a collection of the kind (array, List, HashSet), or in a lazy sequence that is none (lazy).
    public static IEnumerable<int> CollectionOf(string kind, int[] values)
    {
        List<int> list = [.. values];
        HashSet<int> set = [.. values];
        return kind switch
        {
            "array" => values,
            "List" => list,
            "HashSet" => set,
            "lazy" => values.Select(static id => id),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };
    }

    // A count of ids: the seed's 1 and 3, and then ids that no account has.
    public static int[] IdsOf(int count) => [1, 3, .. Enumerable.Range(Unused, count - 2)];
}
