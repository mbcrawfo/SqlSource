using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Generation;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Planning;

/// <summary>
/// The <c>.sql</c> files of one project as the generator lists them: normalised, each once ignoring case, the first
/// spelling kept, in the order of <c>SqlPath.Comparer</c>.
/// </summary>
internal sealed class ListedFiles
{
    private readonly Dictionary<string, ManifestFile> _byPath;

    private ListedFiles(Dictionary<string, ManifestFile> byPath)
    {
        _byPath = byPath;
        Paths = SqlPath.ToSortedSet(byPath.Keys);
    }

    /// <summary>The normalised paths, as <c>PathResolver.FindFiles</c> takes them.</summary>
    public EquatableArray<string> Paths { get; }

    /// <summary>The file of a path of <see cref="Paths" />.</summary>
    public ManifestFile this[string normalizedPath] => _byPath[normalizedPath];

    public static ListedFiles Read(ProjectManifest manifest)
    {
        var byPath = new Dictionary<string, ManifestFile>(SqlPath.Comparer);
        foreach (var file in manifest.Files)
        {
            if (SqlPath.IsSqlFile(file.Path) && SqlPath.Normalize(file.Path) is { } path)
            {
                _ = byPath.TryAdd(path, file);
            }
        }

        return new ListedFiles(byPath);
    }

    /// <summary>The files that a claim's <c>Path</c> resolves to, by the generator's rule, in member order.</summary>
    public ImmutableArray<string> ClaimedBy(TypeClaim claim) =>
        PathResolver.FindFiles(claim.SourcePath, claim.Path, Paths);
}
