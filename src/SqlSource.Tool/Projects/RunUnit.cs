namespace SqlSource.Tool.Projects;

/// <summary>
/// The one solution or project that a run is on.
/// </summary>
/// <param name="Kind">Which of the two.</param>
/// <param name="Path">The full path of the file.</param>
internal sealed record RunUnit(RunUnitKind Kind, string Path);
