using SqlSource.Parsing;
using SqlSource.Settings;

namespace SqlSource.SettingsFiles;

/// <summary>
/// What one settings file says.  Compared by value, so that the generator can cache it.
/// </summary>
/// <param name="Root">
/// Whether the file ends the search for settings files: true only for <c>"root": true</c>.  Null when the text is not
/// one JSON object, since its <c>root</c> is then unknown and the search goes on past it.
/// </param>
/// <param name="Dialect">
/// The dialect, or null when the file does not set it, sets it blank or sets one not valid.
/// </param>
/// <param name="Level">The settings, <see cref="SettingsLevel.None" /> when the file sets none.</param>
/// <param name="Errors">The problems, in the order of their positions.</param>
internal sealed record SettingsFile(
    bool? Root,
    SqlDialectChoice? Dialect,
    SettingsLevel Level,
    EquatableArray<SettingsFileError> Errors
)
{
    /// <summary>The name of a settings file, matched exactly.</summary>
    public const string FileName = "sqlsource.json";
}
