using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Diagnostics;
using SqlSource.Settings;

namespace SqlSource.Generation.MSBuild;

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
    /// <summary>Where the compiler puts the properties of the project.</summary>
    public const string PropertyPrefix = "build_property.";

    /// <summary>
    /// Where the compiler puts the metadata of an item of <c>SqlSourceSettingsFile</c>, the item type that
    /// <c>build/SqlSource.targets</c> fills with the <c>AdditionalFiles</c> items that have any.
    /// </summary>
    public const string MetadataPrefix = "build_metadata.SqlSourceSettingsFile.";

    public const string GeneratorParametersName = "SqlSourceGeneratorParameters";

    public const string OutputName = "SqlSourceOutput";

    /// <summary>
    /// The setting that the tool reads and the generator does not: <c>build/SqlSource.props</c> does not show it to
    /// the compiler, and the keys of the compiler, <see cref="Property" /> and <see cref="Metadata" />, have none
    /// for it.
    /// </summary>
    public const string DatabaseName = "SqlSourceDatabase";

    public const string InputModelSuffixName = "SqlSourceInputModelSuffix";

    public const string OutputModelSuffixName = "SqlSourceOutputModelSuffix";

    public const string ModelNamespaceName = "SqlSourceModelNamespace";

    public const string InputModelTypeName = "SqlSourceInputModelType";

    public const string OutputModelTypeName = "SqlSourceOutputModelType";

    public const string CollectionTypeName = "SqlSourceCollectionType";

    /// <summary>
    /// The setting that the generator reads and the tool does not, since it changes nothing that is described: the
    /// keys of a manifest, <see cref="ManifestProperty" /> and <see cref="ManifestMetadata" />, have none for it.
    /// </summary>
    public const string TypeOverridesName = "SqlSourceTypeOverrides";

    // The names, in the order of SettingId.  Above Property and Metadata, which read it when they are made.
    private static readonly string[] Names =
    [
        GeneratorParametersName,
        OutputName,
        DatabaseName,
        InputModelSuffixName,
        OutputModelSuffixName,
        ModelNamespaceName,
        InputModelTypeName,
        OutputModelTypeName,
        CollectionTypeName,
        TypeOverridesName,
    ];

    /// <summary>
    /// The keys of the project's properties that the compiler is shown.  The database is not among them, so the
    /// generator does not read it, and does not judge it for a project that shows it to the compiler itself.
    /// </summary>
    public static SettingKeys Property { get; } = new(PropertyPrefix, SettingPlace.Property, isManifest: false);

    /// <summary>The keys of the metadata of a file's item that the compiler is shown, without the database.</summary>
    public static SettingKeys Metadata { get; } = new(MetadataPrefix, SettingPlace.Metadata, isManifest: false);

    /// <summary>
    /// The keys of the properties in a project manifest, which the tool reads: with the database, and without the
    /// type override.
    /// </summary>
    public static SettingKeys ManifestProperty { get; } = new(PropertyPrefix, SettingPlace.Property, isManifest: true);

    /// <summary>
    /// The keys of a file's metadata in a project manifest: with the database, and without the type override.
    /// </summary>
    public static SettingKeys ManifestMetadata { get; } = new(MetadataPrefix, SettingPlace.Metadata, isManifest: true);

    /// <summary>The name MSBuild knows a setting by, without a prefix.</summary>
    public static string NameOf(SettingId setting) => Names[(int)setting];

    /// <summary>
    /// Reads one level.  A value that is not valid is added to <paramref name="invalid" /> and is not set.  The
    /// shared empty level is returned when nothing is set.
    /// </summary>
    public static SettingsLevel Read(AnalyzerConfigOptions options, SettingKeys keys, ref List<InvalidSetting>? invalid)
    {
        var source = new Source(options, keys, invalid);
        var level = SettingsLevelReader.Read(ref source);
        invalid = source.InvalidSettings;
        return level;
    }

    // The options the compiler gives for the project or for one file, under the keys of one prefix.
    private struct Source(AnalyzerConfigOptions options, SettingKeys keys, List<InvalidSetting>? invalid)
        : ISettingSource
    {
        public List<InvalidSetting>? InvalidSettings { get; private set; } = invalid;

        public readonly SettingPlace Place => keys.Place;

        public readonly string? Text(SettingId setting) =>
            keys[setting] is { } key && options.TryGetValue(key, out var value) ? value : null;

        public T? Choice<T>(SettingId setting)
            where T : struct, Enum
        {
            if (Text(setting) is not { } written || string.IsNullOrWhiteSpace(written))
            {
                return null;
            }

            if (SettingValue.TryReadChoice<T>(written.AsSpan().Trim(), out var choice))
            {
                return choice;
            }

            Invalid(setting, written.Trim());
            return null;
        }

        public void Invalid(SettingId setting, string value) =>
            (InvalidSettings ??= []).Add(new InvalidSetting(NameOf(setting), value));
    }

    /// <summary>
    /// The keys of the settings under one prefix, built once.  The keys of a manifest are the tool's, and have the
    /// database and not the type override; the compiler's have the type override and not the database.
    /// </summary>
    internal sealed class SettingKeys(string prefix, SettingPlace place, bool isManifest)
    {
        private readonly string[] _keys = Array.ConvertAll(Names, name => prefix + name);

        /// <summary>The place the settings under these keys are written in.</summary>
        public SettingPlace Place => place;

        /// <summary>The key of a setting, or null for one these keys have none for.</summary>
        public string? this[SettingId setting] =>
            (setting == SettingId.Database && !isManifest) || (setting == SettingId.TypeOverrides && isManifest)
                ? null
                : _keys[(int)setting];
    }
}
