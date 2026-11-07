using SqlSource.Settings;

namespace SqlSource.Tests.Settings;

// The project's settings file of the tests, and the root's above it.
internal static class TestPlaces
{
    public static readonly SettingPlace SettingsFile = SettingPlace.OfSettingsFile("/app/sqlsource.json", 1);

    public static readonly SettingPlace RootSettingsFile = SettingPlace.OfSettingsFile("/sqlsource.json", 0);
}
