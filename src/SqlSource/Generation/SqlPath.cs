using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
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
    /// The paths, each once ignoring case and in the order of <see cref="Comparer" />, which is also the order of a
    /// type's members.  Of two paths that differ only by case, the first is kept.  The generator and the
    /// <c>sqlsource</c> tool both make a project's list of <c>.sql</c> files with this.
    /// </summary>
    public static EquatableArray<string> ToSortedSet(IEnumerable<string> paths) =>
        new(paths.Distinct(Comparer).OrderBy(static path => path, Comparer).ToImmutableArray());

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
    public static bool Contains(EquatableArray<string> sortedPaths, string normalizedPath) =>
        IndexOf(sortedPaths, normalizedPath, static path => path) >= 0;

    /// <summary>
    /// The index in <paramref name="sortedItems" />, which is ordered by <see cref="Comparer" /> on the path that
    /// <paramref name="getPath" /> gives, of the item whose path is <paramref name="normalizedPath" />.  Negative
    /// when there is none.
    /// </summary>
    public static int IndexOf<T>(EquatableArray<T> sortedItems, string normalizedPath, Func<T, string> getPath)
        where T : IEquatable<T>
    {
        var low = 0;
        var high = sortedItems.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = Comparer.Compare(getPath(sortedItems[middle]), normalizedPath);
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
    /// The paths of <paramref name="sortedPaths" />, which is ordered by <see cref="Comparer" />, that are files of
    /// <paramref name="normalizedFolder" /> itself and not of a folder inside it, in the same order.
    /// </summary>
    /// <remarks>
    /// The order puts everything under a folder together, and the files of its subfolders among its own.  So this
    /// searches for the first path under the folder and walks from there, and steps over a subfolder with another
    /// search.  It costs the folder's own files and subfolders, not the number of paths, and compares in place.
    /// </remarks>
    public static ImmutableArray<string> FindInFolder(EquatableArray<string> sortedPaths, string normalizedFolder)
    {
        // Where the name of a file of the folder starts.  The root has no separator after it.
        var nameStart = normalizedFolder.Length == 0 ? 0 : normalizedFolder.Length + 1;
        ImmutableArray<string>.Builder? files = null;

        var index = Seek(sortedPaths, 0, normalizedFolder, normalizedFolder.Length, past: false);
        while (index < sortedPaths.Count)
        {
            var path = sortedPaths[index];
            if (CompareToFolder(path, normalizedFolder, normalizedFolder.Length) != 0)
            {
                break;
            }

            var subfolderEnd = path.IndexOf(Separator, nameStart);
            if (subfolderEnd < 0)
            {
                (files ??= ImmutableArray.CreateBuilder<string>()).Add(path);
                index++;
            }
            else
            {
                index = Seek(sortedPaths, index + 1, path, subfolderEnd, past: true);
            }
        }

        return files?.ToImmutable() ?? ImmutableArray<string>.Empty;
    }

    // The first index from start of a path that does not sort before the paths under the folder or, when past is
    // set, that sorts after them.  The folder is the first folderLength characters of folder.
    private static int Seek(EquatableArray<string> sortedPaths, int start, string folder, int folderLength, bool past)
    {
        var low = start;
        var high = sortedPaths.Count;
        while (low < high)
        {
            var middle = low + ((high - low) / 2);
            var order = CompareToFolder(sortedPaths[middle], folder, folderLength);
            if (order < 0 || (past && order == 0))
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

    // Zero when the path is under the folder, at any depth.  Otherwise negative or positive as the path sorts before
    // or after everything that is.  The folder is the first folderLength characters of folder, and nothing is copied.
    private static int CompareToFolder(string path, string folder, int folderLength)
    {
        if (folderLength == 0)
        {
            return 0;
        }

        var order = string.Compare(path, 0, folder, 0, folderLength, StringComparison.OrdinalIgnoreCase);
        if (order != 0)
        {
            return order;
        }

        // The path starts with the folder's name, and what follows decides.  Nothing, and the path is that name
        // alone, which sorts first.  Otherwise the next character against the separator: ignoring case moves no
        // character onto the separator or across it, so the two compare as they are.
        return path.Length == folderLength ? -1 : path[folderLength].CompareTo(Separator);
    }
}
