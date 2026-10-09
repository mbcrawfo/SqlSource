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
    /// <summary>
    /// Reads a file's metadata.  Null for a file that sets nothing, which is almost every file, and for one whose path
    /// cannot be normalized.  The path is normalized only for a file that has settings.
    /// </summary>
    /// <param name="path">The file's path as the compiler gives it.</param>
    /// <param name="fileOptions">The options of the file's item.</param>
    public static FileSettings? Read(string path, AnalyzerConfigOptions fileOptions)
    {
        List<InvalidSetting>? invalid = null;
        var level = MSBuildSettings.Read(fileOptions, MSBuildSettings.Metadata, ref invalid);
        if (ReferenceEquals(level, SettingsLevel.None) && invalid is null)
        {
            return null;
        }

        if (SqlPath.Normalize(path) is not { } normalizedPath)
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
