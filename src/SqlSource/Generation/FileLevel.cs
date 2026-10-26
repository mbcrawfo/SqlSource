using SqlSource.Generation.Chains;
using SqlSource.Generation.MSBuild;
using SqlSource.Generation.Paths;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// What the places outside a <c>.sql</c> file and its types say about its settings: the chain of its folder over the
/// metadata of its item.  This is stage two of the settings, which joins after a type's queries are selected and never
/// reaches the parse.
/// </summary>
/// <param name="NormalizedPath">The file's path in the form <see cref="SqlPath.Normalize" /> gives.</param>
/// <param name="Level">The chain's level over the metadata's.</param>
internal sealed record FileLevel(string NormalizedPath, SettingsLevel Level)
{
    /// <summary>
    /// The level of <paramref name="metadata" />'s file under <paramref name="chain" />, the chain of its folder.  Null
    /// when neither says anything, and for a file whose path does not normalise.
    /// </summary>
    public static FileLevel? Resolve(FileMetadata metadata, SettingsChain chain) =>
        Resolve(metadata, SqlPath.Normalize(metadata.File.Path), chain);

    /// <summary>
    /// The same, for a caller that has normalised the file's path already, to find its chain:
    /// <paramref name="normalizedPath" /> is what <see cref="SqlPath.Normalize" /> gives for it.
    /// </summary>
    /// <remarks>
    /// A file without metadata shares its chain's level, and one under no chain its metadata's, so that neither
    /// allocates a level.
    /// </remarks>
    public static FileLevel? Resolve(FileMetadata metadata, string? normalizedPath, SettingsChain chain)
    {
        var level = chain.Level.Over(metadata.Settings?.Level ?? SettingsLevel.None);
        return ReferenceEquals(level, SettingsLevel.None) || normalizedPath is null
            ? null
            : new FileLevel(normalizedPath, level);
    }
}
