using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using SqlSource.Generation;
using SqlSource.Tool.Projects;

namespace SqlSource.Tool.Planning;

/// <summary>
/// The <c>.sql</c> files of one project as the generator lists them: normalised, each once ignoring case, the first
/// spelling kept, in the order of <c>SqlPath.Comparer</c>.  A path that the project lists twice has the metadata a
/// build reads for it: that of its last item that has any.
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
            if (!SqlPath.IsSqlFile(file.Path) || SqlPath.Normalize(file.Path) is not { } path)
            {
                continue;
            }

            if (!byPath.TryGetValue(path, out var kept))
            {
                byPath.Add(path, file);
            }
            else if (file.Metadata.Count > 0 && string.Equals(kept.Path, file.Path, StringComparison.Ordinal))
            {
                // One path listed twice, as "Include" does where "Update" was meant.  The compiler gives a file
                // its metadata by its path, and of the items of one path the last that has any of the package's
                // is the one a build reads.  Another spelling of the path is another file to the compiler: the
                // first spelling is kept, and the build reports SQLSRC013.
                byPath[path] = file;
            }
        }

        return new ListedFiles(byPath);
    }

    /// <summary>The files that a claim's <c>Path</c> resolves to, by the generator's rule, in member order.</summary>
    public ImmutableArray<string> ClaimedBy(TypeClaim claim) =>
        PathResolver.FindFiles(claim.SourcePath, claim.Path, Paths);
}
