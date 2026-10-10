using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using SqlSource.Generation;

namespace SqlSource.Tool.Planning;

/// <summary>
/// What the command line restricts a run to.  A filter selects queries and takes nothing out of the plan: a sidecar
/// is written only when every query of its file that needs an entry has one, so the plan keeps them all.
/// </summary>
/// <param name="Files">The <c>.sql</c> files named, each once.  Empty for every file.</param>
/// <param name="Databases">The databases named, each once ignoring case.  Empty for every database.</param>
internal sealed record RunFilters(ImmutableArray<FileFilter> Files, ImmutableArray<string> Databases)
{
    /// <summary>A run with no filter.</summary>
    public static RunFilters None { get; } = new([], []);

    public bool IsEmpty => Files.IsEmpty && Databases.IsEmpty;

    /// <summary>
    /// The filters of a command line.  A path is resolved against the working directory and compared as the
    /// generator compares paths, so another case, <c>./</c>, <c>..</c> and either separator name the same file.
    /// </summary>
    public static RunFilters Create(
        IEnumerable<string> sqlPaths,
        IEnumerable<string> databases,
        string workingDirectory
    )
    {
        var files = ImmutableArray.CreateBuilder<FileFilter>();
        var seen = new HashSet<string>(SqlPath.Comparer);
        foreach (var given in sqlPaths)
        {
            var path = Path.GetFullPath(given, workingDirectory);
            if (SqlPath.Normalize(path) is { } normalized && seen.Add(normalized))
            {
                files.Add(new FileFilter(path, normalized));
            }
        }

        return new RunFilters(files.ToImmutable(), [.. databases.Distinct(StringComparer.OrdinalIgnoreCase)]);
    }
}
