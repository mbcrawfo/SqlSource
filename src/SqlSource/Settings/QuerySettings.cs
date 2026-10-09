namespace SqlSource.Settings;

/// <summary>
/// The settings in effect for one query, for one type that claims its file.
/// </summary>
/// <param name="Parameters">The list of generator parameters.</param>
internal readonly record struct QuerySettings(GeneratorParameters Parameters)
{
    /// <summary>Whether comments and blank lines stay in the query's SQL.</summary>
    public bool KeepComments => (Parameters & GeneratorParameters.KeepComments) != 0;

    /// <summary>Whether the query's method checks its arguments.</summary>
    public bool ValidateTokens => (Parameters & GeneratorParameters.NoTokenValidation) == 0;

    /// <summary>
    /// Resolves each setting from the most specific level that has it: the markers of the query and its file, then
    /// the attribute, then the metadata of the file's item, then the project's property, then the default.
    /// </summary>
    public static QuerySettings Resolve(
        SettingsLevel markers,
        SettingsLevel attribute,
        SettingsLevel metadata,
        SettingsLevel property
    ) =>
        new(
            markers.Parameters
                ?? attribute.Parameters
                ?? metadata.Parameters
                ?? property.Parameters
                ?? GeneratorParameters.None
        );
}
