namespace SqlSource.Tool.Planning;

/// <summary>
/// One <c>.sql</c> path of the command line, which restricts a run to that file.
/// </summary>
/// <param name="Path">The full path.</param>
/// <param name="NormalizedPath">The path in the form <c>SqlPath.Normalize</c> gives, which is what is compared.</param>
/// <param name="Named">
/// How an error names the file: the full path in quotes, or the position of the token that gave it when that token
/// holds <c>=</c> or <c>;</c>.
/// </param>
internal sealed record FileFilter(string Path, string NormalizedPath, string Named);
