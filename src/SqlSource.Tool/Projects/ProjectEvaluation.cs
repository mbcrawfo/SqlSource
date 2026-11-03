using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Projects;

/// <summary>
/// One project of a run, after MSBuild was asked about it.
/// </summary>
/// <param name="ProjectPath">The full path of the project file.</param>
/// <param name="State">What was found.</param>
/// <param name="Manifest">The manifest, when the project uses SqlSource.</param>
/// <param name="Failure">The error to report, when the state is <see cref="ProjectState.Failed" />.</param>
/// <param name="AssetsFile">
/// The full path of the <c>project.assets.json</c> that a restore of the project writes, whether it exists or not, when
/// MSBuild was asked about the project and named one.
/// </param>
internal sealed record ProjectEvaluation(
    string ProjectPath,
    ProjectState State,
    ProjectManifest? Manifest = null,
    ToolDiagnostic? Failure = null,
    string? AssetsFile = null
);
