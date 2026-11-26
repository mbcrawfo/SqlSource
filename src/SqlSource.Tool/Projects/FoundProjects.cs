using System.Collections.Immutable;

namespace SqlSource.Tool.Projects;

/// <summary>
/// What <see cref="RunProjects.FindAsync" /> found: the projects of the run, and the manifests of those that use
/// SqlSource.
/// </summary>
/// <param name="Projects">
/// The full path of each project of the unit that the run asked MSBuild about, which <c>--project</c> narrows, in the
/// unit's order: the ones with an error and the ones that do not use SqlSource too.
/// </param>
/// <param name="Manifests">
/// The manifests of the projects, in the order of the projects, a project's target frameworks one after another.
/// </param>
/// <param name="AssetsFiles">
/// The full path of each project that MSBuild named the <c>project.assets.json</c> of, with that file's full path,
/// whether it exists or not, in the order of the projects.
/// </param>
internal sealed record FoundProjects(
    ImmutableArray<string> Projects,
    ImmutableArray<ProjectManifest> Manifests,
    ImmutableArray<(string Project, string AssetsFile)> AssetsFiles
)
{
    /// <summary>
    /// The manifests of the projects of the solution that <c>--project</c> left out and that use SqlSource, when the
    /// run asked for them, in the order of the projects.  They are no projects of the run: the plan takes from them
    /// only what they need of the files that a project of the run claims too.
    /// </summary>
    public ImmutableArray<ProjectManifest> Others { get; init; } = [];

    /// <summary>No project, as a run finds when its solution cannot be read.</summary>
    public static FoundProjects None { get; } = new([], [], []);
}
