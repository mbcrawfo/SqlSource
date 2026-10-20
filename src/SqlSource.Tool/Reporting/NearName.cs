using System;

namespace SqlSource.Tool.Reporting;

/// <summary>
/// Tells whether a name a user typed is a misspelling of a name the tool knows.  The tool prints what a user typed
/// only when it is, since anything else may be a secret typed in the wrong place.
/// </summary>
internal static class NearName
{
    /// <summary>
    /// The most edits that still make a name near: an insertion, a deletion, a replacement or the swap of two
    /// neighbouring characters each count one.
    /// </summary>
    public const int MaxDistance = 2;

    /// <summary>
    /// Whether <paramref name="given"/> is at most <see cref="MaxDistance"/> edits from <paramref name="known"/>,
    /// by the optimal string alignment distance.
    /// </summary>
    public static bool IsNear(string given, string known, bool ignoreCase)
    {
        if (Math.Abs(given.Length - known.Length) > MaxDistance)
        {
            return false;
        }

        // Each row holds the distances from a prefix of given to every prefix of known.  The swap of two characters
        // looks two rows back.
        var beforePrevious = new int[known.Length + 1];
        var previous = new int[known.Length + 1];
        var current = new int[known.Length + 1];
        for (var j = 0; j <= known.Length; j++)
        {
            previous[j] = j;
        }

        for (var i = 1; i <= given.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= known.Length; j++)
            {
                var cost = Same(given[i - 1], known[j - 1], ignoreCase) ? 0 : 1;
                var distance = Math.Min(Math.Min(previous[j] + 1, current[j - 1] + 1), previous[j - 1] + cost);
                if (
                    i > 1
                    && j > 1
                    && Same(given[i - 1], known[j - 2], ignoreCase)
                    && Same(given[i - 2], known[j - 1], ignoreCase)
                )
                {
                    distance = Math.Min(distance, beforePrevious[j - 2] + 1);
                }

                current[j] = distance;
            }

            (beforePrevious, previous, current) = (previous, current, beforePrevious);
        }

        return previous[known.Length] <= MaxDistance;
    }

    private static bool Same(char left, char right, bool ignoreCase) =>
        left == right || (ignoreCase && char.ToUpperInvariant(left) == char.ToUpperInvariant(right));
}
