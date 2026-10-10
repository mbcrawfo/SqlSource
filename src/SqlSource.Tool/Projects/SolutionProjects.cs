using System.Collections.Immutable;

namespace SqlSource.Tool.Projects;

/// <summary>
/// The C# projects of a solution, or why the solution could not be read.
/// </summary>
/// <param name="Projects">The full paths of the <c>.csproj</c> files, in ordinal order.</param>
/// <param name="Failure">What the reader said when it could not read the file; null when it could.</param>
internal sealed record SolutionProjects(ImmutableArray<string> Projects, string? Failure);
