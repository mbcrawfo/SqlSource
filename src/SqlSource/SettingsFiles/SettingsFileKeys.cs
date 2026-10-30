using System;
using System.Collections.Immutable;
using SqlSource.Settings;

namespace SqlSource.SettingsFiles;

/// <summary>
/// The keys of a settings file, by the table of the settings files design.  Keys are matched ordinally.
/// </summary>
internal static class SettingsFileKeys
{
    /// <summary>The schema, for editors.  The reader checks that it is a string and reads nothing of it.</summary>
    public const string Schema = "$schema";

    /// <summary>Whether the search for settings files ends at this file.</summary>
    public const string Root = "root";

    /// <summary>The dialect and its options, read by <see cref="Parsing.SqlDialectName" />.</summary>
    public const string Dialect = "dialect";

    public const string Database = "database";

    public const string Output = "output";

    public const string GeneratorParameters = "generatorParameters";

    public const string InputModelSuffix = "inputModelSuffix";

    public const string OutputModelSuffix = "outputModelSuffix";

    public const string ModelNamespace = "modelNamespace";

    public const string InputModelType = "inputModelType";

    public const string OutputModelType = "outputModelType";

    public const string CollectionType = "collectionType";

    public const string TypeOverrides = "typeOverrides";

    public const string ColumnTypes = "columnTypes";

    public const string LibraryTypes = "libraryTypes";

    public const string NestedClass = "nestedClass";

    /// <summary>
    /// The member of a column type written as an object that gives its C# type.  Not a key of the top level.
    /// </summary>
    public const string TypeMember = "type";

    /// <summary>
    /// The member of a column type written as an object that says whether its column holds JSON, the JSON opt-in.  Not
    /// a key of the top level.
    /// </summary>
    public const string JsonMember = "json";

    // The keys of the settings of a level, in the order of SettingId, which KeyOf indexes.
    private static readonly string[] Settings =
    [
        GeneratorParameters,
        Output,
        Database,
        InputModelSuffix,
        OutputModelSuffix,
        ModelNamespace,
        InputModelType,
        OutputModelType,
        CollectionType,
        TypeOverrides,
        ColumnTypes,
        LibraryTypes,
        NestedClass,
    ];

    /// <summary>Every key, in the order of the design's table.</summary>
    public static ImmutableArray<string> All { get; } =
        ImmutableArray.Create(
            Schema,
            Root,
            Dialect,
            Database,
            Output,
            GeneratorParameters,
            InputModelSuffix,
            OutputModelSuffix,
            ModelNamespace,
            InputModelType,
            OutputModelType,
            CollectionType,
            TypeOverrides,
            ColumnTypes,
            LibraryTypes,
            NestedClass
        );

    /// <summary>The JSON type the value of a key must have.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="key" /> is not one of <see cref="All" />.
    /// </exception>
    public static SettingsFileValueType TypeOf(string key) =>
        key switch
        {
            Root => SettingsFileValueType.Boolean,
            GeneratorParameters => SettingsFileValueType.ArrayOfStrings,
            TypeOverrides or LibraryTypes => SettingsFileValueType.ObjectOfStrings,
            ColumnTypes => SettingsFileValueType.ObjectOfColumnTypes,
            _ when All.Contains(key) => SettingsFileValueType.String,
            _ => throw new ArgumentOutOfRangeException(nameof(key)),
        };

    /// <summary>
    /// The setting of a level that a key gives.  False for a key that gives none, <see cref="Schema" />,
    /// <see cref="Root" /> and <see cref="Dialect" />, and for one that is not a key.
    /// </summary>
    public static bool TryGetSetting(string key, out SettingId setting)
    {
        var index = Array.IndexOf(Settings, key);
        setting = (SettingId)Math.Max(index, 0);
        return index >= 0;
    }

    /// <summary>The key that gives a setting of a level.</summary>
    public static string KeyOf(SettingId setting) => Settings[(int)setting];
}
