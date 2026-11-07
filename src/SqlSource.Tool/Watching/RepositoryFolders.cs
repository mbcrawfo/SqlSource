using System.Collections.Immutable;
using System.IO;
using Path = System.IO.Path;

namespace SqlSource.Tool.Watching;

/// <summary>
/// The folders of a repository that a folder is in, which bound how far up a watch looks for a settings file.
/// </summary>
/// <remarks>
/// The root of a repository, or of a worktree, is the first folder at or above a folder that holds a <c>.git</c>
/// folder or file.  With no such folder up to the root of the file system the folder is in no repository, and only
/// the folder itself counts.
/// </remarks>
internal static class RepositoryFolders
{
    private const string RepositoryMarker = ".git";

    /// <summary>
    /// The folder and each above it, nearest first, up to and including the root of its repository; the folder alone
    /// when it is in none.
    /// </summary>
    public static ImmutableArray<string> FoldersOf(string folder)
    {
        var folders = ImmutableArray.CreateBuilder<string>();
        for (var current = Normalize(folder); current is not null; current = Path.GetDirectoryName(current))
        {
            folders.Add(current);
            if (IsRepositoryRoot(current))
            {
                return folders.ToImmutable();
            }
        }

        return [Normalize(folder)];
    }

    /// <summary>
    /// True when the folder is in a repository, or a worktree: the last folder of <see cref="FoldersOf" /> holds a
    /// <c>.git</c> folder or file.
    /// </summary>
    public static bool IsInRepository(string folder) => IsRepositoryRoot(FoldersOf(folder)[^1]);

    // A plain disk check, as the tool's other reads of a project's folders are.
    private static bool IsRepositoryRoot(string folder)
    {
        var marker = Path.Combine(folder, RepositoryMarker);
        return Directory.Exists(marker) || File.Exists(marker);
    }

    private static string Normalize(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
}
