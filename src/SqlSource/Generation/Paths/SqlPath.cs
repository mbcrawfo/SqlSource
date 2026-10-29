using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using SqlSource.Snapshot;

namespace SqlSource.Generation.Paths;

/// <summary>
/// Path handling as string work.  A generator may not touch the file system, and a project must build the same on
/// every operating system, so both separators are accepted and paths are compared ignoring case.
/// </summary>
internal static class SqlPath
{
    /// <summary>The end of the path of a query file.</summary>
    public const string SqlExtension = ".sql";

    /// <summary>
    /// The end of the path of a sidecar: that of a query file and <see cref="SidecarFormat.Extension" />.
    /// </summary>
    public const string SidecarExtension = SqlExtension + SidecarFormat.Extension;

    private const char Separator = '/';

    private const byte Once = 1;

    private const byte Twice = 2;

    private static readonly char[] Separators = [Separator, '\\'];

    private static bool IsAsciiLetter(char c) => c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z');

    /// <summary>
    /// Compares two normalised paths.  Its order is that of <see cref="Comparison" />.
    /// </summary>
    public static StringComparer Comparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// The comparison of <see cref="Comparer" />, for a search that compares a part of a path in place.
    /// </summary>
    public const StringComparison Comparison = StringComparison.OrdinalIgnoreCase;

    public static bool IsSqlFile(string path) => path.EndsWith(SqlExtension, StringComparison.OrdinalIgnoreCase);

    public static bool IsSidecarFile(string path) =>
        path.EndsWith(SidecarExtension, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// The normalised path of the <c>.sql</c> file whose sidecar is at <paramref name="sidecarPath" />: the
    /// sidecar's normalised path without <c>.json</c>.  Null when <see cref="Normalize" /> is, or when the
    /// normalised path is not a sidecar's.
    /// </summary>
    public static string? SqlPathOfSidecar(string sidecarPath) =>
        Normalize(sidecarPath) is { } normalized && IsSidecarFile(normalized)
            ? normalized.Substring(0, normalized.Length - SidecarFormat.Extension.Length)
            : null;

    /// <summary>
    /// Whether <paramref name="sidecarPath" />, normalised, is <paramref name="normalizedSqlPath" /> with
    /// <c>.json</c> after it, spelled exactly so: the path at which the tool reads and writes the sidecar of that
    /// file.
    /// </summary>
    public static bool IsSidecarPathOf(string sidecarPath, string normalizedSqlPath) =>
        Normalize(sidecarPath) is { } normalized
        && normalized.Length == normalizedSqlPath.Length + SidecarFormat.Extension.Length
        && normalized.StartsWith(normalizedSqlPath, StringComparison.Ordinal)
        && normalized.EndsWith(SidecarFormat.Extension, StringComparison.Ordinal);

    /// <summary>
    /// The paths, each once ignoring case and in the order of <see cref="Comparer" />, which is also the order of a
    /// type's members.  Of two paths that differ only by case, the first is kept.  The generator and the
    /// <c>sqlsource</c> tool both make a project's list of <c>.sql</c> files with this.
    /// </summary>
    public static EquatableArray<string> ToSortedSet(IEnumerable<string> paths) =>
        new(paths.Distinct(Comparer).OrderBy(static path => path, Comparer).ToImmutableArray());

    /// <summary>
    /// Whether <paramref name="path" /> starts at a root: at a separator, either one, or at an ASCII drive letter and
    /// <c>:</c>.  Read from the text alone, the same on every operating system.
    /// </summary>
    public static bool IsRooted(string path) =>
        path.Length > 0
        && (Array.IndexOf(Separators, path[0]) >= 0 || (path.Length > 1 && path[1] == ':' && IsAsciiLetter(path[0])));

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
    /// Whether <paramref name="normalizedPath" /> is under <paramref name="normalizedFolder" />, at any depth: it is
    /// the folder's segments and more, compared by <see cref="Comparison" />, so <c>a/objx/b</c> is not under
    /// <c>a/obj</c>.  Every path is under the root, which is empty.  It compares in place and allocates nothing.
    /// </summary>
    public static bool IsInFolder(string normalizedPath, string normalizedFolder) =>
        CompareToFolder(normalizedPath, normalizedFolder, normalizedFolder.Length) == 0;

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
        where T : IEquatable<T> => SortedSearch.IndexOf(sortedItems, normalizedPath, getPath, Comparer);

    /// <summary>
    /// The index in <paramref name="sortedItems" />, which is ordered by <see cref="Comparer" /> on the path that
    /// <paramref name="getPath" /> gives, of the item whose path is the first <paramref name="length" /> characters of
    /// <paramref name="normalizedPath" />, such as one of its folders.  Negative when there is none.  It compares in
    /// place and allocates nothing.
    /// </summary>
    public static int IndexOf<T>(
        EquatableArray<T> sortedItems,
        string normalizedPath,
        int length,
        Func<T, string> getPath
    )
        where T : IEquatable<T> => SortedSearch.IndexOf(sortedItems, normalizedPath, length, getPath, Comparison);

    /// <summary>
    /// The paths of <paramref name="sortedPaths" />, which is ordered by <see cref="Comparer" />, that more than one
    /// of <paramref name="files" /> has, ignoring case, in the same order and spelled as there.  A file the project
    /// lists twice under one spelling counts twice.
    /// </summary>
    /// <remarks>
    /// This runs whenever the project's list of files changes and almost never finds anything, so it is a search for
    /// each file and allocates nothing until it finds a path.
    /// </remarks>
    public static EquatableArray<string> FindRepeated(
        ImmutableArray<SqlFilePath> files,
        EquatableArray<string> sortedPaths
    )
    {
        // Whether each path has been seen, then whether it has been seen twice, by its index.
        var seen = ArrayPool<byte>.Shared.Rent(sortedPaths.Count);
        try
        {
            Array.Clear(seen, 0, sortedPaths.Count);
            ImmutableArray<string>.Builder? repeated = null;
            foreach (var file in files)
            {
                var index = IndexOf(sortedPaths, file.NormalizedPath, static path => path);
                if (index < 0 || seen[index] == Twice)
                {
                    continue;
                }

                if (seen[index] == Once)
                {
                    seen[index] = Twice;
                    (repeated ??= ImmutableArray.CreateBuilder<string>()).Add(sortedPaths[index]);
                }
                else
                {
                    seen[index] = Once;
                }
            }

            if (repeated is null)
            {
                return EquatableArray<string>.Empty;
            }

            // The files are in the project's order, and the paths are wanted in theirs.
            repeated.Sort(Comparer);
            return new EquatableArray<string>(repeated.ToImmutable());
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(seen);
        }
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
    private static int Seek(
        EquatableArray<string> sortedPaths,
        int start,
        string folder,
        int folderLength,
        bool past
    ) => SortedSearch.LowerBound(start, sortedPaths.Count, new FolderProbe(sortedPaths, folder, folderLength, past));

    // A struct, so that SortedSearch does not box it.  A path under the folder, which compares to it as zero, sorts
    // before what is looked for when it is past the folder.
    private readonly struct FolderProbe(EquatableArray<string> sortedPaths, string folder, int folderLength, bool past)
        : SortedSearch.IProbe
    {
        public int CompareAt(int index)
        {
            var order = CompareToFolder(sortedPaths[index], folder, folderLength);
            return order == 0 && past ? -1 : order;
        }
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
