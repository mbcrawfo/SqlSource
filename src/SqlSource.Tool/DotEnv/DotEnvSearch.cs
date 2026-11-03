using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using SqlSource.Diagnostics;
using SqlSource.Tool.Reporting;
using Path = System.IO.Path;

namespace SqlSource.Tool.DotEnv;

/// <summary>
/// Finds the <c>.env</c> file of a run and reads it.
/// </summary>
/// <remarks>
/// The search starts in the folder of the run's unit, the project or the solution, and goes up, one folder at a time,
/// to the first folder that holds a <c>.git</c> folder or file, the root of the repository, or of a worktree.  With
/// no such folder up to the root of the file system only the unit's folder is searched, so that a file in a folder
/// that is not the project's own is never picked up by chance.  The nearest file wins and files are never merged.
/// The file is read through the host, so a test never reads a developer's own.
/// </remarks>
internal static class DotEnvSearch
{
    /// <summary>The name of the file.</summary>
    public const string FileName = ".env";

    private const string RepositoryMarker = ".git";

    /// <summary>
    /// The folders searched, nearest first.
    /// </summary>
    public static ImmutableArray<string> FoldersOf(string unitFolder)
    {
        var folders = ImmutableArray.CreateBuilder<string>();
        for (var folder = Normalize(unitFolder); folder is not null; folder = Path.GetDirectoryName(folder))
        {
            folders.Add(folder);
            if (IsRepositoryRoot(folder))
            {
                return folders.ToImmutable();
            }
        }

        return [Normalize(unitFolder)];
    }

    /// <summary>
    /// Reads the nearest <c>.env</c> of <see cref="FoldersOf" />.  A file that cannot be read, or has a line of a
    /// key of the tool that cannot be, gives errors and no values, so the run goes on as if there were none.
    /// </summary>
    public static DotEnvFile Read(string unitFolder, Func<string, string?> readTextFile)
    {
        foreach (var folder in FoldersOf(unitFolder))
        {
            var path = Path.Combine(folder, FileName);
            string? text;
            try
            {
                text = readTextFile(path);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                return new DotEnvFile(
                    path,
                    [],
                    [ToolDiagnostic.ForFile(ToolDiagnostics.DotEnvCannotBeRead, path, exception.Message)]
                );
            }

            if (text is null)
            {
                continue;
            }

            var contents = DotEnvParser.Parse(text);
            return contents.Errors.IsEmpty
                ? new DotEnvFile(path, contents.Values, [])
                : new DotEnvFile(
                    path,
                    [],
                    [
                        .. contents.Errors.Select(error =>
                            ToolDiagnostic.At(
                                ToolDiagnostics.DotEnvLineCannotBeRead,
                                path,
                                new(error.Line, error.Column),
                                error.Key,
                                error.Reason
                            )
                        ),
                    ]
                );
        }

        return new DotEnvFile(Path: null, [], []);
    }

    /// <summary>
    /// The <c>.env</c> files the run did not read, though a project's own may be meant: each that exists, in a folder
    /// from a project's folder up to, and not including, the unit's folder or one of its ancestors, other than
    /// <paramref name="read" />.  Each path is once, in the order found.  A project in the unit's folder has none.
    /// </summary>
    public static ImmutableArray<string> NotRead(
        string unitFolder,
        string? read,
        IEnumerable<string> projectFolders,
        Func<string, string?> readTextFile
    )
    {
        var comparer = PathComparer;
        var unitAndAbove = new HashSet<string>(comparer);
        for (var folder = Normalize(unitFolder); folder is not null; folder = Path.GetDirectoryName(folder))
        {
            _ = unitAndAbove.Add(folder);
        }

        var found = new List<string>();
        foreach (var projectFolder in projectFolders)
        {
            for (
                var folder = Normalize(projectFolder);
                folder is not null && !unitAndAbove.Contains(folder);
                folder = Path.GetDirectoryName(folder)
            )
            {
                var path = Path.Combine(folder, FileName);
                if (!comparer.Equals(path, read) && !found.Contains(path, comparer) && Exists(path, readTextFile))
                {
                    found.Add(path);
                }
            }
        }

        return [.. found];
    }

    private static StringComparer PathComparer =>
        OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

    // A file exists when the host reads it, or fails to: a file that cannot be read is still one the run did not use.
    private static bool Exists(string path, Func<string, string?> readTextFile)
    {
        try
        {
            return readTextFile(path) is not null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return true;
        }
    }

    // A plain disk check, as the tool's other reads of a project's folders are.
    private static bool IsRepositoryRoot(string folder)
    {
        var marker = Path.Combine(folder, RepositoryMarker);
        return Directory.Exists(marker) || File.Exists(marker);
    }

    private static string Normalize(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
}
