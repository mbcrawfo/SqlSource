using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Settings;

/// <summary>
/// One place that settings are written in, other than a marker: the attribute of a type or a settings file.  It finds
/// a raw value as text, as elements or as members, and says where one that is not valid goes;
/// <see cref="SettingsLevelReader" /> says everything else.
/// </summary>
/// <remarks>
/// An implementation is a struct, so that the reader, which is generic over it, boxes nothing.
/// </remarks>
internal interface ISettingSource
{
    /// <summary>
    /// The place the source is, which the entries of the type override, the column types and the library types it gives
    /// carry.
    /// </summary>
    SettingPlace Place { get; }

    /// <summary>The text as written, or null when the source does not have the setting or does not set it.</summary>
    string? Text(SettingId setting);

    /// <summary>
    /// The elements of a setting written as an array of strings, as a settings file writes the generator parameters,
    /// or null when the source writes it as text or not at all.
    /// </summary>
    IReadOnlyList<string>? Elements(SettingId setting);

    /// <summary>
    /// The key and the value of each member of a setting written as an object of strings, as a settings file writes
    /// the type override, the column types and the library types, as written and in order, or null when the source
    /// writes it as text or not at all.
    /// </summary>
    IReadOnlyList<KeyValuePair<string, string>>? Members(SettingId setting);

    /// <summary>
    /// Where the member at <paramref name="index" /> of a setting written as an object is, from its key to its value,
    /// with the line and column of that span.  A source that never gives members for the setting returns the default.
    /// </summary>
    (TextSpan Span, LinePositionSpan LineSpan) MemberSpans(SettingId setting, int index);

    /// <summary>
    /// The choice, or null when it is not set.  A value that is no member is recorded by the source, which alone
    /// knows how it was written: a number in an attribute, a word in a settings file.
    /// </summary>
    T? Choice<T>(SettingId setting)
        where T : struct, Enum;

    /// <summary>Records a value that is not valid.  The setting is then not set.</summary>
    void Invalid(SettingId setting, string value);

    /// <summary>
    /// Records that the element or member at <paramref name="index" /> is not valid.  The others apply.
    /// </summary>
    void InvalidPart(SettingId setting, int index);

    /// <summary>
    /// Records that the member at <paramref name="index" /> has the key of the member at <paramref name="first" />, by
    /// the identity of the setting's map.  It is left out, and the first applies.
    /// </summary>
    void RepeatedKey(SettingId setting, int index, int first);
}
