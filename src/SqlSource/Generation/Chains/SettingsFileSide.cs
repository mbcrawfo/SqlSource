using SqlSource.SettingsFiles;

namespace SqlSource.Generation.Chains;

/// <summary>Who reads a settings file, which decides the problems of it that are reported.</summary>
internal enum SettingsFileSide
{
    /// <summary>The generator, in a build.</summary>
    Build,

    /// <summary>The <c>sqlsource</c> tool.</summary>
    Tool,
}

/// <summary>What each <see cref="SettingsFileSide" /> reports.</summary>
internal static class SettingsFileSides
{
    /// <summary>
    /// Whether <paramref name="side" /> reports <paramref name="error" />.  Both report a problem of the file as a
    /// file.  A value of a setting is reported by the side that reads it: the build reads every setting but
    /// <c>database</c>, and the tool reads <c>dialect</c>, <c>output</c> and <c>database</c>.
    /// </summary>
    public static bool Reports(SettingsFileSide side, SettingsFileError error) =>
        error.IsAboutTheFile
        || side switch
        {
            SettingsFileSide.Build => error.Setting != SettingsFileKeys.Database,
            SettingsFileSide.Tool => error.Setting
                is SettingsFileKeys.Dialect
                    or SettingsFileKeys.Output
                    or SettingsFileKeys.Database,
            _ => false,
        };
}
