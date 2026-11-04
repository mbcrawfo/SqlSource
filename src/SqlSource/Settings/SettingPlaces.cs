namespace SqlSource.Settings;

/// <summary>What the settings that merge per key, the type override and the column types, say of a place.</summary>
internal static class SettingPlaces
{
    /// <summary>
    /// How specific a place is, the most specific highest: the query, the file's header, then the settings files.  The
    /// nearer a settings file's folder to the file's the higher it ranks.
    /// </summary>
    public static int Specificity(SettingPlace place) =>
        place.Kind switch
        {
            SettingPlaceKind.Query => int.MaxValue,
            SettingPlaceKind.FileHeader => int.MaxValue - 1,
            SettingPlaceKind.SettingsFile => 1 + place.Depth,
            _ => throw UnexpectedEnumValueException.Of(place.Kind),
        };
}
