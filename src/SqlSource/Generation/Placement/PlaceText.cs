using SqlSource.Settings;

namespace SqlSource.Generation.Placement;

/// <summary>
/// The place a setting of placement was written in, as the diagnostics of placement name it.
/// </summary>
internal static class PlaceText
{
    /// <summary>
    /// The place: "its own marker" for a query's, "the file header" for its file's, and
    /// "the settings file '&lt;path&gt;'"
    /// for a settings file.
    /// </summary>
    public static string Of(SettingPlace place) =>
        place.Kind switch
        {
            SettingPlaceKind.Query => "its own marker",
            SettingPlaceKind.FileHeader => "the file header",
            SettingPlaceKind.SettingsFile => "the settings file '" + place.Path + "'",
        };
}
