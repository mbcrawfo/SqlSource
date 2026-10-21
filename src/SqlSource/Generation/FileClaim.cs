using SqlSource.Generation.Paths;

namespace SqlSource.Generation;

/// <summary>
/// Whether a type claims a <c>.sql</c> file, with the file's input.  The answer is compared by value, so a change to
/// the claimed paths reads only a file whose answer or input changed.
/// </summary>
/// <param name="Input">The file with everything its parse depends on.</param>
/// <param name="IsClaimed">Whether the file's path is among the claimed paths: only a claimed file is read.</param>
internal sealed record FileClaim(FileParseInput Input, bool IsClaimed)
{
    /// <summary>
    /// A file is claimed when its path is known and is among <paramref name="claimedPaths" />.
    /// </summary>
    public static FileClaim Of(FileParseInput input, EquatableArray<string> claimedPaths) =>
        new(input, input.NormalizedPath is { } path && SqlPath.Contains(claimedPaths, path));
}
