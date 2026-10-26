using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using SqlSource.Generation.Paths;
using SqlSource.Settings;

namespace SqlSource.Generation.Chains;

/// <summary>
/// The settings files of a project, each folder's once, with the chain that starts at each, so that the chain of any
/// folder is a search for its nearest file.  The generator and the <c>sqlsource</c> tool both build it with
/// <see cref="Of" />.
/// </summary>
/// <param name="Files">The files, in the order of <see cref="SqlPath.Comparer" /> on their folders.</param>
/// <param name="Parents">
/// For each file, the index of the nearest file in a folder above its own, or -1 when there is none.
/// </param>
/// <param name="Chains">For each file, the chain that starts at it.</param>
internal sealed record SettingsFileSet(
    EquatableArray<ListedSettingsFile> Files,
    EquatableArray<int> Parents,
    EquatableArray<SettingsChain> Chains
)
{
    private const char Separator = '/';

    /// <summary>A project without settings files.</summary>
    public static SettingsFileSet Empty { get; } =
        new(EquatableArray<ListedSettingsFile>.Empty, EquatableArray<int>.Empty, EquatableArray<SettingsChain>.Empty);

    /// <summary>
    /// The set of <paramref name="files" />.  Of two files of one folder, ignoring case, the first is kept.  A chain
    /// ends after the first file whose <c>root</c> is true, and goes on past one whose <c>root</c> is false or unknown.
    /// </summary>
    public static SettingsFileSet Of(IEnumerable<ListedSettingsFile> files)
    {
        // The sort is stable, so the first file of a folder comes first among that folder's.
        var kept = ImmutableArray.CreateBuilder<ListedSettingsFile>();
        foreach (var file in files.OrderBy(static file => file.NormalizedFolder, SqlPath.Comparer))
        {
            var previous = kept.Count == 0 ? null : kept[kept.Count - 1].NormalizedFolder;
            if (previous is null || !SqlPath.Comparer.Equals(previous, file.NormalizedFolder))
            {
                kept.Add(file);
            }
        }

        if (kept.Count == 0)
        {
            return Empty;
        }

        // A folder above another is a prefix of it and sorts before it, so each file's parent and the parent's chain
        // are known when it is reached.  The parent is searched for, not kept on a stack: a folder such as app-tests
        // sorts between app and app/Queries, since '-' sorts before the separator.
        var sorted = new EquatableArray<ListedSettingsFile>(kept.ToImmutable());
        var parents = ImmutableArray.CreateBuilder<int>(sorted.Count);
        var chains = ImmutableArray.CreateBuilder<SettingsChain>(sorted.Count);
        foreach (var file in sorted)
        {
            var parent = file.NormalizedFolder.Length == 0 ? -1 : NearestIndex(sorted, file.NormalizedFolder);
            parents.Add(parent);
            chains.Add(ChainStartingAt(file, parent < 0 ? SettingsChain.None : chains[parent]));
        }

        return new SettingsFileSet(
            sorted,
            new EquatableArray<int>(parents.MoveToImmutable()),
            new EquatableArray<SettingsChain>(chains.MoveToImmutable())
        );
    }

    /// <summary>
    /// The chain of the folder of <paramref name="normalizedFilePath" />: the one that starts at the nearest file in
    /// that folder or a folder above it, or <see cref="SettingsChain.None" /> when there is none.  The chain is
    /// shared by every path whose nearest file is the same.
    /// </summary>
    /// <remarks>A search for each folder above the path's, which compares in place and allocates nothing.</remarks>
    public SettingsChain ChainOf(string normalizedFilePath) =>
        NearestIndex(Files, normalizedFilePath) is var index and >= 0 ? Chains[index] : SettingsChain.None;

    /// <summary>
    /// The folders of the files that the chain of the folder of some path of <paramref name="normalizedFilePaths" />
    /// reaches, in the order of <see cref="Files" />.  It stops reading the paths once every file is reached.
    /// </summary>
    public EquatableArray<string> Reached(IEnumerable<string> normalizedFilePaths)
    {
        if (Files.Count == 0)
        {
            return EquatableArray<string>.Empty;
        }

        var reached = ArrayPool<bool>.Shared.Rent(Files.Count);
        try
        {
            Array.Clear(reached, 0, Files.Count);
            var count = 0;
            foreach (var path in normalizedFilePaths)
            {
                // A file reached before has had the rest of its chain reached with it.
                var index = NearestIndex(Files, path);
                while (index >= 0 && !reached[index])
                {
                    reached[index] = true;
                    count++;
                    index = Files[index].File.Root == true ? -1 : Parents[index];
                }

                if (count == Files.Count)
                {
                    break;
                }
            }

            var folders = ImmutableArray.CreateBuilder<string>(count);
            for (var i = 0; i < Files.Count; i++)
            {
                if (reached[i])
                {
                    folders.Add(Files[i].NormalizedFolder);
                }
            }

            return new EquatableArray<string>(folders.MoveToImmutable());
        }
        finally
        {
            ArrayPool<bool>.Shared.Return(reached);
        }
    }

    // The chain that starts at the file, whose parent's chain is given.  A file that says nothing and does not end the
    // chain shares its parent's.
    private static SettingsChain ChainStartingAt(ListedSettingsFile file, SettingsChain parent)
    {
        var read = file.File;
        var saysNothing = ReferenceEquals(read.Level, SettingsLevel.None) && read.Dialect is null;
        if (read.Root == true)
        {
            return saysNothing ? SettingsChain.None : new SettingsChain(read.Level, read.Dialect);
        }

        return saysNothing ? parent : new SettingsChain(read.Level.Over(parent.Level), read.Dialect ?? parent.Dialect);
    }

    // The index of the nearest file in the folder of the path or a folder above it, or -1.  Each folder is the path's
    // text up to a separator, searched for as a span.
    private static int NearestIndex(EquatableArray<ListedSettingsFile> files, string normalizedPath)
    {
        var end = normalizedPath.LastIndexOf(Separator);
        while (true)
        {
            var length = Math.Max(end, 0);
            var index = IndexOfFolder(files, normalizedPath.AsSpan(0, length));
            if (index >= 0 || length == 0)
            {
                return index;
            }

            end = normalizedPath.LastIndexOf(Separator, length - 1);
        }
    }

    // The index of the file of the folder, or -1.  Compares as SqlPath.Comparer orders the files.
    private static int IndexOfFolder(EquatableArray<ListedSettingsFile> files, ReadOnlySpan<char> folder)
    {
        var low = 0;
        var high = files.Count - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var order = files[middle].NormalizedFolder.AsSpan().CompareTo(folder, StringComparison.OrdinalIgnoreCase);
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
