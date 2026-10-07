using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using SqlSource.Diagnostics;

namespace SqlSource.Generation;

/// <summary>
/// A <c>.sql</c> file whose path differs only by case from that of a file the project lists before it.  The generator
/// compares paths ignoring case, so it reads the earlier file for both and would drop this one without a word.
/// </summary>
/// <remarks>
/// A generator may not ask the file system, so two files of a case-sensitive file system look the same as one file
/// that the project lists under two spellings.  Both are a collision.
/// </remarks>
/// <param name="NormalizedPath">The path of the file in the form <see cref="SqlPath.Normalize" /> gives.</param>
/// <param name="Path">The path of the file as the project lists it.</param>
/// <param name="OtherPath">The path of the earlier file as the project lists it.</param>
internal sealed record PathCollision(string NormalizedPath, string Path, string OtherPath)
{
    /// <summary>
    /// Finds the collisions among <paramref name="files" />, in the order the project lists them.
    /// </summary>
    /// <param name="files">The project's <c>.sql</c> files, in the order the project lists them.</param>
    /// <param name="sortedPaths">
    /// The normalised paths of <paramref name="files" />, distinct and in the order of <see cref="SqlPath.Comparer" />,
    /// with the spelling of the first file the project lists for each.
    /// </param>
    /// <remarks>
    /// This runs whenever the project's list of files changes and almost never finds anything, so it is a search for
    /// each file and allocates nothing until it finds a collision.
    /// </remarks>
    public static EquatableArray<PathCollision> Find(
        ImmutableArray<SqlFilePath> files,
        EquatableArray<string> sortedPaths
    )
    {
        ImmutableArray<PathCollision>.Builder? collisions = null;
        foreach (var file in files)
        {
            var index = SqlPath.IndexOf(sortedPaths, file.NormalizedPath, static path => path);
            if (index < 0 || string.Equals(sortedPaths[index], file.NormalizedPath, StringComparison.Ordinal))
            {
                continue;
            }

            // A file that the project lists twice under one spelling is one collision.
            collisions ??= ImmutableArray.CreateBuilder<PathCollision>();
            if (!Contains(collisions, file.NormalizedPath))
            {
                collisions.Add(new PathCollision(file.NormalizedPath, file.Path, FindPath(files, sortedPaths[index])));
            }
        }

        return collisions is null ? EquatableArray<PathCollision>.Empty : new(collisions.ToImmutable());
    }

    /// <summary>
    /// The error for this collision, at the start of the file.
    /// </summary>
    public Diagnostic ToDiagnostic() =>
        DiagnosticInfo
            .Create(SqlDiagnostics.PathDiffersOnlyByCase, new LocationInfo(Path, default, default), OtherPath)
            .ToDiagnostic();

    private static bool Contains(ImmutableArray<PathCollision>.Builder collisions, string normalizedPath) =>
        collisions.Any(collision => string.Equals(collision.NormalizedPath, normalizedPath, StringComparison.Ordinal));

    // The path as the project lists it of the first file with this normalised path.
    private static string FindPath(ImmutableArray<SqlFilePath> files, string normalizedPath) =>
        files.First(file => string.Equals(file.NormalizedPath, normalizedPath, StringComparison.Ordinal)).Path;
}
