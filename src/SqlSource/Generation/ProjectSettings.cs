using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// What the project's MSBuild properties say about the settings.
/// </summary>
/// <param name="Level">The properties that are set and valid.</param>
/// <param name="Invalid">The values that are not valid.</param>
internal sealed record ProjectSettings(SettingsLevel Level, EquatableArray<InvalidSetting> Invalid)
{
    public static ProjectSettings Read(AnalyzerConfigOptions globalOptions)
    {
        List<InvalidSetting>? invalid = null;
        var level = MSBuildSettings.Read(globalOptions, MSBuildSettings.Property, ref invalid);
        return new ProjectSettings(
            level,
            invalid is null
                ? EquatableArray<InvalidSetting>.Empty
                : new EquatableArray<InvalidSetting>(invalid.ToImmutableArray())
        );
    }
}
