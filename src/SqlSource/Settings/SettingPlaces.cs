namespace SqlSource.Settings;

/// <summary>What the settings that merge per key, the type override and the column types, say of a place.</summary>
internal static class SettingPlaces
{
    /// <summary>
    /// How specific a place is, the most specific highest: the query, the file's header, the metadata, then the
    /// property.  Neither setting has an attribute level, which ranks with the property.
    /// </summary>
    public static int Specificity(SettingPlace place) =>
        place switch
        {
            SettingPlace.Query => 3,
            SettingPlace.FileHeader => 2,
            SettingPlace.Metadata => 1,
            SettingPlace.Property or SettingPlace.Attribute or _ => 0,
        };
}
