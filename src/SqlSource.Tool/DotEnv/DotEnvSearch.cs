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
/// Says which <c>.env</c> file a run reads, and reads it.
/// </summary>
/// <remarks>
/// A run reads one file, or none: the <c>.env</c> of one folder, its unit's, or a file named by its path.  No folder
/// above is looked in, so that a file that is not the project's own is never picked up by chance.  The file is read
/// through the host, so a test never reads a developer's own.
/// </remarks>
internal static class DotEnvSearch
{
    /// <summary>The name of the file.</summary>
    public const string FileName = ".env";

    /// <summary>The value of the option that reads no file, whatever its case.</summary>
    public const string NoneValue = "none";

    /// <summary>
    /// The full path of the file a run reads, or null when it reads none.
    /// </summary>
    /// <param name="option">
    /// The value of the option that names the file: null for the <c>.env</c> of <paramref name="defaultFolder" />;
    /// <see cref="NoneValue" />, ignoring case and the white space around it, for none; else a path, which is from
    /// <paramref name="workingDirectory" /> when it is relative, so that <c>./none</c> names a file.
    /// </param>
    /// <param name="defaultFolder">The folder whose <c>.env</c> a run reads when the option names none.</param>
    /// <param name="workingDirectory">The folder that a relative path is from.</param>
    public static string? FileOf(string? option, string defaultFolder, string workingDirectory)
    {
        if (option is null)
        {
            return Path.GetFullPath(Path.Combine(defaultFolder, FileName));
        }

        return string.Equals(option.Trim(), NoneValue, StringComparison.OrdinalIgnoreCase)
            ? null
            : Path.GetFullPath(option, workingDirectory);
    }

    /// <summary>
    /// Reads the file at <paramref name="path" />.  No path, or a path where the host finds no file, gives no file.  A
    /// file that cannot be read, or has a line of a key of the tool that cannot be, gives errors and no values, so the
    /// run goes on as if there were none.
    /// </summary>
    public static DotEnvFile Read(string? path, Func<string, string?> readTextFile)
    {
        if (path is null)
        {
            return new DotEnvFile(Path: null, [], []);
        }

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
            return new DotEnvFile(Path: null, [], []);
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

    private static string Normalize(string folder) => Path.TrimEndingDirectorySeparator(Path.GetFullPath(folder));
}
