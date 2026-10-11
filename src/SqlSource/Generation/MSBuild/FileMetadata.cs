using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace SqlSource.Generation.MSBuild;

/// <summary>
/// One <c>.sql</c> file with what the metadata of its item says: its dialect, and its settings.
/// </summary>
/// <param name="File">
/// The file.  Compared by reference: the compiler hands out the same object until the file changes.
/// </param>
/// <param name="Dialect">The dialect the metadata names, if any.</param>
/// <param name="Settings">The settings the metadata gives, or null when it gives none.</param>
internal sealed record FileMetadata(AdditionalText File, DialectSetting Dialect, FileSettings? Settings)
{
    /// <summary>Reads the metadata the compiler is shown.</summary>
    public static FileMetadata Read(AdditionalText file, AnalyzerConfigOptions fileOptions) =>
        Read(file, fileOptions, MSBuildSettings.Metadata);

    /// <summary>Reads the metadata, with the settings under <paramref name="keys" />.</summary>
    public static FileMetadata Read(
        AdditionalText file,
        AnalyzerConfigOptions fileOptions,
        MSBuildSettings.SettingKeys keys
    ) => new(file, DialectSetting.ReadMetadata(fileOptions), FileSettings.Read(file.Path, fileOptions, keys));
}
