using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation.MSBuild;

/// <summary>
/// What the project's MSBuild properties say about the settings.
/// </summary>
/// <param name="Level">The properties that are set and valid.</param>
/// <param name="Invalid">The values that are not valid.</param>
internal sealed record ProjectSettings(SettingsLevel Level, EquatableArray<InvalidSetting> Invalid)
{
    /// <summary>Reads the properties the compiler is shown.</summary>
    public static ProjectSettings Read(AnalyzerConfigOptions globalOptions) =>
        Read(globalOptions, MSBuildSettings.Property);

    /// <summary>Reads the properties under <paramref name="keys" />.</summary>
    public static ProjectSettings Read(AnalyzerConfigOptions globalOptions, MSBuildSettings.SettingKeys keys)
    {
        List<InvalidSetting>? invalid = null;
        var level = MSBuildSettings.Read(globalOptions, keys, ref invalid);
        return new ProjectSettings(
            level,
            invalid is null
                ? EquatableArray<InvalidSetting>.Empty
                : new EquatableArray<InvalidSetting>(invalid.ToImmutableArray())
        );
    }
}
