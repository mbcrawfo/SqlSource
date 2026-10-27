using SqlSource.Generation.Chains;
using SqlSource.Generation.Paths;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// What the places outside a <c>.sql</c> file and its types say about its settings: the chain of its folder.  This is
/// stage two of the settings, which joins after a type's queries are selected and never reaches the parse.
/// </summary>
/// <param name="NormalizedPath">The file's path in the form <see cref="SqlPath.Normalize" /> gives.</param>
/// <param name="Level">The chain's level.</param>
internal sealed record FileLevel(string NormalizedPath, SettingsLevel Level)
{
    /// <summary>
    /// The level of the file at <paramref name="normalizedPath" />, what <see cref="SqlPath.Normalize" /> gives for its
    /// path, under <paramref name="chain" />, the chain of its folder.  Null when the chain says nothing, and for a
    /// file whose path does not normalise.
    /// </summary>
    /// <remarks>The files of a folder share their chain's level, so that none allocates a level.</remarks>
    public static FileLevel? Resolve(string? normalizedPath, SettingsChain chain) =>
        ReferenceEquals(chain.Level, SettingsLevel.None) || normalizedPath is null
            ? null
            : new FileLevel(normalizedPath, chain.Level);
}
