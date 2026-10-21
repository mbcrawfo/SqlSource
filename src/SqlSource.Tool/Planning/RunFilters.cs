using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using SqlSource.Generation.Paths;

namespace SqlSource.Tool.Planning;

/// <summary>
/// What the command line restricts a run to.  A filter selects queries and takes nothing out of the plan: a sidecar
/// is written only when every query of its file that needs an entry has one, so the plan keeps them all.
/// </summary>
/// <param name="Files">The <c>.sql</c> files named, each once.  Empty for every file.</param>
/// <param name="Databases">
/// The values of <c>--database</c>, each name once ignoring case, in the order given; the first of a name is kept.
/// Empty for every database.
/// </param>
internal sealed record RunFilters(ImmutableArray<FileFilter> Files, ImmutableArray<UsageValue> Databases)
{
    /// <summary>A run with no filter.</summary>
    public static RunFilters None { get; } = new([], []);

    public bool IsEmpty => Files.IsEmpty && Databases.IsEmpty;

    /// <summary>
    /// Whether the command line named a <c>.sql</c> file, placed in <see cref="Files" /> or not.  A path whose
    /// <c>..</c> climbs above the root names no file and selects nothing, but the run is still one on a part, which
    /// deletes no sidecar.
    /// </summary>
    public bool NamesFiles { get; init; }

    /// <summary>
    /// The filters of a command line.  A path is resolved against the working directory and compared as the
    /// generator compares paths, so another case, <c>./</c>, <c>..</c> and either separator name the same file.  A
    /// file that several paths name is named by the first, and a path that names no file is left out of
    /// <see cref="Files" /> but sets <see cref="NamesFiles" />.  Such a path is reported nowhere (TD-0068).
    /// </summary>
    public static RunFilters Create(
        IEnumerable<UsageValue> sqlPaths,
        IEnumerable<UsageValue> databases,
        string workingDirectory
    )
    {
        var files = ImmutableArray.CreateBuilder<FileFilter>();
        var seen = new HashSet<string>(SqlPath.Comparer);
        var namesFiles = false;
        foreach (var given in sqlPaths)
        {
            namesFiles = true;
            var path = Path.GetFullPath(given.Text, workingDirectory);
            if (SqlPath.Normalize(path) is { } normalized && seen.Add(normalized))
            {
                files.Add(new FileFilter(normalized, given.NamedAsPath(path)));
            }
        }

        return new RunFilters(
            files.ToImmutable(),
            [.. databases.DistinctBy(static value => value.Text, DatabaseName.Comparer)]
        )
        {
            NamesFiles = namesFiles,
        };
    }

    /// <summary>Whether <c>--database</c> names this database, ignoring case.</summary>
    public bool NamesDatabase(string name) => Databases.Any(value => DatabaseName.Comparer.Equals(value.Text, name));
}
