using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation;

/// <summary>
/// Reads what MSBuild says about the settings: the project's properties, and the metadata of one file's
/// <c>AdditionalFiles</c> item.  Both reach the generator only because <c>build/SqlSource.props</c> lists them.
/// </summary>
/// <remarks>
/// The dialect is not read here.  It is an input of the parse and is resolved before it, by
/// <see cref="DialectSetting" />; everything here joins after a type's queries are selected.
/// </remarks>
internal static class MSBuildSettings
{
    public const string GeneratorParametersName = "SqlSourceGeneratorParameters";

    /// <summary>Where the compiler puts the properties of the project.</summary>
    public static SettingKeys Property { get; } = new("build_property.");

    /// <summary>
    /// Where the compiler puts the metadata of an item of <c>SqlSourceSettingsFile</c>, the item type that
    /// <c>build/SqlSource.targets</c> fills with the <c>AdditionalFiles</c> items that have any.
    /// </summary>
    public static SettingKeys Metadata { get; } = new("build_metadata.SqlSourceSettingsFile.");

    /// <summary>
    /// Reads one level.  A value that is not valid is added to <paramref name="invalid" /> and is not set.  The
    /// shared empty level is returned when nothing is set.
    /// </summary>
    public static SettingsLevel Read(AnalyzerConfigOptions options, SettingKeys keys, ref List<InvalidSetting>? invalid)
    {
        var level = SettingsLevel.None;
        if (options.TryGetValue(keys.GeneratorParameters, out var list) && !string.IsNullOrWhiteSpace(list))
        {
            var words = new List<string>();
            if (GeneratorParameterList.Parse(list, words) is { } parameters)
            {
                level = level with { Parameters = parameters };
            }

            foreach (var word in words)
            {
                (invalid ??= []).Add(new InvalidSetting(GeneratorParametersName, word));
            }
        }

        return level;
    }

    /// <summary>The keys of the settings under one prefix, built once.</summary>
    internal sealed class SettingKeys(string prefix)
    {
        public string GeneratorParameters { get; } = prefix + GeneratorParametersName;
    }
}
