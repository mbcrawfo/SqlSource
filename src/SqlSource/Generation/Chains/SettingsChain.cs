using SqlSource.Parsing;
using SqlSource.Settings;

namespace SqlSource.Generation.Chains;

/// <summary>
/// What a chain of settings files says: the files that apply to a folder, from the nearest to the first whose
/// <c>root</c> is true, or to the root folder.
/// </summary>
/// <param name="Level">The settings of the chain's files, a nearer file's over a farther one's.</param>
/// <param name="Dialect">The dialect of the nearest file that sets one, or null when none does.</param>
internal sealed record SettingsChain(SettingsLevel Level, SqlDialectChoice? Dialect)
{
    /// <summary>
    /// The chain of a folder that no settings file applies to, or whose files say nothing.  Shared, so that such a
    /// folder allocates none.
    /// </summary>
    public static SettingsChain None { get; } = new(SettingsLevel.None, null);
}
