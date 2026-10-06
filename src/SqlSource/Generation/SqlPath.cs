using System;
using System.Collections.Generic;
using System.Text;

namespace SqlSource.Generation;

/// <summary>
/// Path handling as string work.  A generator may not touch the file system, and a project must build the same on
/// every operating system, so both separators are accepted and paths are compared ignoring case.
/// </summary>
internal static class SqlPath
{
    private const string SqlExtension = ".sql";

    private const char Separator = '/';

    private static readonly char[] Separators = [Separator, '\\'];

    /// <summary>
    /// Compares two normalised paths.
    /// </summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    public static bool IsSqlFile(string path) => path.EndsWith(SqlExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Returns <paramref name="path" /> with <c>/</c> as its only separator, without empty and <c>.</c> segments,
    /// and with each <c>..</c> resolved.  A root is not kept: <c>/a/b</c> and <c>a/b</c> are the same path.
    /// Returns null when a <c>..</c> has no segment before it.
    /// </summary>
    public static string? Normalize(string path)
    {
        var segments = new List<(int Start, int Length)>();
        var start = 0;
        while (start <= path.Length)
        {
            var end = path.IndexOfAny(Separators, start);
            if (end < 0)
            {
                end = path.Length;
            }

            var length = end - start;
            if (length == 2 && path[start] == '.' && path[start + 1] == '.')
            {
                if (segments.Count == 0)
                {
                    return null;
                }

                segments.RemoveAt(segments.Count - 1);
            }
            else if (length > 0 && !(length == 1 && path[start] == '.'))
            {
                segments.Add((start, length));
            }

            start = end + 1;
        }

        var normalized = new StringBuilder(path.Length);
        foreach (var (segmentStart, segmentLength) in segments)
        {
            if (normalized.Length > 0)
            {
                _ = normalized.Append(Separator);
            }

            _ = normalized.Append(path, segmentStart, segmentLength);
        }

        return normalized.ToString();
    }

    /// <summary>
    /// Joins <paramref name="relativePath" /> to a normalised folder and normalises the result.  The path is never
    /// treated as rooted: a leading separator is an empty segment.  Returns null when <see cref="Normalize" /> does.
    /// </summary>
    public static string? Combine(string normalizedFolder, string relativePath) =>
        Normalize(normalizedFolder + Separator + relativePath);

    /// <summary>
    /// The folder of a normalised path: everything before its last segment.  Empty when it has one segment.
    /// </summary>
    public static string GetFolder(string normalizedPath)
    {
        var separator = normalizedPath.LastIndexOf(Separator);
        return separator < 0 ? string.Empty : normalizedPath.Substring(0, separator);
    }

    /// <summary>
    /// The last segment of a path, normalised or not.
    /// </summary>
    public static string GetFileName(string path) => path.Substring(path.LastIndexOfAny(Separators) + 1);

    /// <summary>
    /// Whether <paramref name="sortedPaths" />, which is ordered by <see cref="Comparer" />, holds
    /// <paramref name="normalizedPath" />.
    /// </summary>
    public static bool Contains(EquatableArray<string> sortedPaths, string normalizedPath)
    {
        var low = 0;
        var high = sortedPaths.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = Comparer.Compare(sortedPaths[middle], normalizedPath);
            if (order == 0)
            {
                return true;
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

        return false;
    }
}
