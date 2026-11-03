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
/// <param name="Manifests">The manifests of the projects, in the order of the projects.</param>
internal sealed record FoundProjects(ImmutableArray<string> Projects, ImmutableArray<ProjectManifest> Manifests);
