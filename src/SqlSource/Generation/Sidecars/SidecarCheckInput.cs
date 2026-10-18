using SqlSource.Generation.Paths;

namespace SqlSource.Generation.Sidecars;

/// <summary>
/// One claimed file with everything its checks read: its sidecar and what its types need.  It compares equal until
/// one of the three changes, so an edit to another file checks this one again only when its own value changed.
/// </summary>
/// <param name="File">The parsed file.</param>
/// <param name="Sidecar">Its sidecar, or null when it has none.</param>
/// <param name="Needs">What its types need, or null when no query of it needs an entry.</param>
internal sealed record SidecarCheckInput(ParsedSqlFile File, SidecarFile? Sidecar, FileNeeds? Needs)
{
    /// <summary>
    /// Finds the sidecar and the needs of <paramref name="file" />.  Both lists are sorted by
    /// <see cref="SqlPath.Comparer" />.
    /// </summary>
    public static SidecarCheckInput Gather(
        ParsedSqlFile file,
        EquatableArray<FileNeeds> needs,
        EquatableArray<SidecarFile> sidecars
    )
    {
        var needsIndex = SqlPath.IndexOf(needs, file.NormalizedPath, static item => item.NormalizedPath);
        var sidecarIndex = SqlPath.IndexOf(sidecars, file.NormalizedPath, static item => item.SqlPath);
        return new SidecarCheckInput(
            file,
            sidecarIndex < 0 ? null : sidecars[sidecarIndex],
            needsIndex < 0 ? null : needs[needsIndex]
        );
    }
}
