namespace SqlSource.Settings;

/// <summary>
/// The settings of a <see cref="SettingsLevel" /> that a source other than a marker can give, in the order
/// <see cref="SettingsLevelReader" /> reads them.  A source maps each to its own name for it.
/// </summary>
internal enum SettingId
{
    Parameters,
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
    NestedClass,
}
