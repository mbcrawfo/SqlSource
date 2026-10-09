using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// What the metadata of one file's <c>AdditionalFiles</c> item says about the settings.  A file's own markers come
/// before it, and so does the attribute of a type that claims the file.
/// </summary>
/// <param name="NormalizedPath">The file's path in the form <see cref="SqlPath.Normalize" /> gives.</param>
/// <param name="Level">The metadata that is set and valid.</param>
/// <param name="Invalid">The values that are not valid.</param>
internal sealed record FileSettings(string NormalizedPath, SettingsLevel Level, EquatableArray<InvalidSetting> Invalid)
{
    /// <summary>Reads a file's metadata.  Null for a file that sets nothing, which is almost every file.</summary>
    public static FileSettings? Read(string normalizedPath, AnalyzerConfigOptions fileOptions)
    {
        List<InvalidSetting>? invalid = null;
        var level = MSBuildSettings.Read(fileOptions, MSBuildSettings.Metadata, ref invalid);
        if (ReferenceEquals(level, SettingsLevel.None) && invalid is null)
        {
            return null;
        }

        return new FileSettings(
            normalizedPath,
            level,
            invalid is null
                ? EquatableArray<InvalidSetting>.Empty
                : new EquatableArray<InvalidSetting>(invalid.ToImmutableArray())
        );
    }
}
