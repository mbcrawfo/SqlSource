namespace SqlSource.Tool.Planning;

/// <summary>
/// One <c>.sql</c> path of the command line, which restricts a run to that file.
/// </summary>
/// <param name="Path">The full path, which is what an error shows.</param>
/// <param name="NormalizedPath">The path in the form <c>SqlPath.Normalize</c> gives, which is what is compared.</param>
internal sealed record FileFilter(string Path, string NormalizedPath);
