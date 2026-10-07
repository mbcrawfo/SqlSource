namespace SqlSource.Generation;

/// <summary>
/// The path of one <c>.sql</c> file of the project, as the project lists it and as the generator compares it.
/// </summary>
/// <param name="Path">The path as the project lists it, which is the one a diagnostic shows.</param>
/// <param name="NormalizedPath">The path in the form <see cref="SqlPath.Normalize" /> gives.</param>
internal sealed record SqlFilePath(string Path, string NormalizedPath)
{
    /// <summary>
    /// Returns null when <see cref="SqlPath.Normalize" /> does.
    /// </summary>
    public static SqlFilePath? Create(string path) =>
        SqlPath.Normalize(path) is { } normalized ? new SqlFilePath(path, normalized) : null;
}
