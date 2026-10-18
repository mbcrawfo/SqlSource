using System;

namespace SqlSource.Settings;

/// <summary>
/// One place that settings are written in, other than a marker: the attribute of a type, the properties of a
/// project, the metadata of a file's item.  It says how a raw value is found and where one that is not valid goes;
/// <see cref="SettingsLevelReader" /> says everything else.
/// </summary>
/// <remarks>
/// An implementation is a struct, so that the reader, which is generic over it, boxes nothing.
/// </remarks>
internal interface ISettingSource
{
    /// <summary>The place the source is, which the entries of a type override it gives carry.</summary>
    SettingPlace Place { get; }

    /// <summary>The text as written, or null when the source does not have the setting or does not set it.</summary>
    string? Text(SettingId setting);

    /// <summary>
    /// The choice, or null when it is not set.  A value that is no member is recorded by the source, which alone
    /// knows how it was written: a number in an attribute, a word in a property.
    /// </summary>
    T? Choice<T>(SettingId setting)
        where T : struct, Enum;

    /// <summary>Records a value that is not valid.  The setting is then not set.</summary>
    void Invalid(SettingId setting, string value);
}
