namespace SqlSource.Settings;

/// <summary>What the settings that merge per key, the type override and the column types, say of a place.</summary>
internal static class SettingPlaces
{
    /// <summary>
    /// How specific a place is, the most specific highest: the query, the file's header, the settings files, the
    /// metadata, then the property.  A settings file ranks above the metadata, and the nearer its folder to the
    /// file's the higher.  Neither setting has an attribute level, which ranks with the property.
    /// </summary>
    public static int Specificity(SettingPlace place) =>
        place.Kind switch
        {
            SettingPlaceKind.Query => int.MaxValue,
            SettingPlaceKind.FileHeader => int.MaxValue - 1,
            SettingPlaceKind.SettingsFile => 2 + place.Depth,
            SettingPlaceKind.Metadata => 1,
            SettingPlaceKind.Property or SettingPlaceKind.Attribute or _ => 0,
        };
}
