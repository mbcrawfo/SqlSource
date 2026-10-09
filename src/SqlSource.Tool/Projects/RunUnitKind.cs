namespace SqlSource.Tool.Projects;

/// <summary>
/// What a run is on.
/// </summary>
internal enum RunUnitKind
{
    /// <summary>A <c>.sln</c> or <c>.slnx</c> file.</summary>
    Solution,

    /// <summary>A <c>.csproj</c> file.</summary>
    Project,
}
