using System;
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

    public const string OutputName = "SqlSourceOutput";

    public const string InputModelSuffixName = "SqlSourceInputModelSuffix";

    public const string OutputModelSuffixName = "SqlSourceOutputModelSuffix";

    public const string ModelNamespaceName = "SqlSourceModelNamespace";

    public const string InputModelTypeName = "SqlSourceInputModelType";

    public const string OutputModelTypeName = "SqlSourceOutputModelType";

    public const string CollectionTypeName = "SqlSourceCollectionType";

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
        var level = new SettingsLevel
        {
            Parameters = ReadParameters(options, keys.GeneratorParameters, ref invalid),
            Output = ReadChoice<OutputKind>(options, keys.Output, OutputName, ref invalid),
            InputModelSuffix = ReadText(
                options,
                keys.InputModelSuffix,
                InputModelSuffixName,
                SettingValue.IsSuffix,
                ref invalid
            ),
            OutputModelSuffix = ReadText(
                options,
                keys.OutputModelSuffix,
                OutputModelSuffixName,
                SettingValue.IsSuffix,
                ref invalid
            ),
            ModelNamespace = ReadText(
                options,
                keys.ModelNamespace,
                ModelNamespaceName,
                SettingValue.IsNamespace,
                ref invalid
            ),
            InputModelType = ReadChoice<ModelKind>(options, keys.InputModelType, InputModelTypeName, ref invalid),
            OutputModelType = ReadChoice<ModelKind>(options, keys.OutputModelType, OutputModelTypeName, ref invalid),
            CollectionType = ReadChoice<CollectionKind>(options, keys.CollectionType, CollectionTypeName, ref invalid),
        };

        return level.Equals(SettingsLevel.None) ? SettingsLevel.None : level;
    }

    private static GeneratorParameters? ReadParameters(
        AnalyzerConfigOptions options,
        string key,
        ref List<InvalidSetting>? invalid
    )
    {
        if (!options.TryGetValue(key, out var list) || string.IsNullOrWhiteSpace(list))
        {
            return null;
        }

        var words = new List<string>();
        var parameters = GeneratorParameterList.Parse(list, words);
        foreach (var word in words)
        {
            (invalid ??= []).Add(new InvalidSetting(GeneratorParametersName, word));
        }

        return parameters;
    }

    private delegate bool Validator(ReadOnlySpan<char> value);

    private static string? ReadText(
        AnalyzerConfigOptions options,
        string key,
        string name,
        Validator isValid,
        ref List<InvalidSetting>? invalid
    )
    {
        if (!options.TryGetValue(key, out var written) || string.IsNullOrWhiteSpace(written))
        {
            return null;
        }

        var value = written.Trim();
        if (isValid(value.AsSpan()))
        {
            return value;
        }

        (invalid ??= []).Add(new InvalidSetting(name, value));
        return null;
    }

    private static T? ReadChoice<T>(
        AnalyzerConfigOptions options,
        string key,
        string name,
        ref List<InvalidSetting>? invalid
    )
        where T : struct, Enum
    {
        if (!options.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (SettingValue.TryReadChoice<T>(value.AsSpan().Trim(), out var choice))
        {
            return choice;
        }

        (invalid ??= []).Add(new InvalidSetting(name, value.Trim()));
        return null;
    }

    /// <summary>The keys of the settings under one prefix, built once.</summary>
    internal sealed class SettingKeys(string prefix)
    {
        public string GeneratorParameters { get; } = prefix + GeneratorParametersName;

        public string Output { get; } = prefix + OutputName;

        public string InputModelSuffix { get; } = prefix + InputModelSuffixName;

        public string OutputModelSuffix { get; } = prefix + OutputModelSuffixName;

        public string ModelNamespace { get; } = prefix + ModelNamespaceName;

        public string InputModelType { get; } = prefix + InputModelTypeName;

        public string OutputModelType { get; } = prefix + OutputModelTypeName;

        public string CollectionType { get; } = prefix + CollectionTypeName;
    }
}
