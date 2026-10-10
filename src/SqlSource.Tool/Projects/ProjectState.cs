namespace SqlSource.Tool.Projects;

/// <summary>
/// What the tool found out about a project.
/// </summary>
internal enum ProjectState
{
    /// <summary>The package's props reached the project, and its manifest was read.</summary>
    UsesSqlSource,

    /// <summary>The project was restored and has nothing of the package.</summary>
    DoesNotUseSqlSource,

    /// <summary>
    /// The project was never restored, or not since SqlSource was added to it, so nothing says whether it uses it.
    /// </summary>
    NotRestored,

    /// <summary>MSBuild failed, or wrote a manifest that cannot be read.</summary>
    Failed,
}
