using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Settings;
using SqlSource.Settings.Libraries;

namespace SqlSource.SettingsFiles;

/// <summary>
/// The settings of one settings file, as <see cref="SettingsLevelReader" /> asks for them.  It turns what the level
/// reader finds not valid into errors at the positions of the file.  Used only by <see cref="SettingsFileReader" />.
/// </summary>
/// <param name="values">The raw value of each setting the file gives, by <see cref="SettingId" />.</param>
/// <param name="text">The text of the file, which the line and column of a member are found in.</param>
/// <param name="place">The place the entries of the type override, column types and library types carry.</param>
/// <param name="errors">The reader's list, which the errors are added to.</param>
/// <param name="libraryMembers">The reader's list, which the position of each entry's member is added to.</param>
internal readonly struct SettingsFileSource(
    SettingsFileValue?[] values,
    string text,
    SettingPlace place,
    List<SettingsFileError> errors,
    List<LibraryMember> libraryMembers
) : ISettingSource
{
    public SettingPlace Place => place;

    public string? Text(SettingId setting) => values[(int)setting]?.Text;

    public IReadOnlyList<string>? Elements(SettingId setting) => values[(int)setting]?.Elements;

    public IReadOnlyList<KeyValuePair<string, string>>? Members(SettingId setting) => values[(int)setting]?.Members;

    public bool IsJsonMember(SettingId setting, int index) => values[(int)setting]?.Json is { } json && json[index];

    // The member from its key to its value, as InvalidPart reports one, kept beside the level and not in it.
    public void LibraryEntry(SqlLibrary library, int index)
    {
        var value = ValueOf(SettingId.LibraryTypes);
        var span = TextSpan.FromBounds(value.KeySpans![index].Start, value.Spans[index].End);
        libraryMembers.Add(new LibraryMember(library, span, TextLines.LineSpanOf(text, span)));
    }

    public T? Choice<T>(SettingId setting)
        where T : struct, Enum
    {
        if (Text(setting) is not { } written || string.IsNullOrWhiteSpace(written))
        {
            return null;
        }

        var value = written.Trim();
        if (SettingValue.TryReadChoice<T>(value.AsSpan(), out var choice))
        {
            return choice;
        }

        Invalid(setting, value);
        return null;
    }

    public void Invalid(SettingId setting, string value) =>
        Add(SettingsFileErrorKind.InvalidValue, ValueOf(setting).Spans[0], setting, value, null);

    // An element is reported at its string; a member from its key to its value, with both.
    public void InvalidPart(SettingId setting, int index)
    {
        var value = ValueOf(setting);
        if (value.Members is { } members)
        {
            Add(
                SettingsFileErrorKind.InvalidValue,
                TextSpan.FromBounds(value.KeySpans![index].Start, value.Spans[index].End),
                setting,
                members[index].Key.Trim(),
                members[index].Value.Trim()
            );
            return;
        }

        Add(SettingsFileErrorKind.InvalidValue, value.Spans[index], setting, value.Elements![index].Trim(), null);
    }

    public void RepeatedKey(SettingId setting, int index, int first)
    {
        var value = ValueOf(setting);
        var members = value.Members!;
        Add(
            SettingsFileErrorKind.RepeatedKey,
            value.KeySpans![index],
            setting,
            members[index].Key.Trim(),
            members[first].Key.Trim()
        );
    }

    // The level reader reports only a setting the source gave it.
    private SettingsFileValue ValueOf(SettingId setting) => values[(int)setting]!;

    private void Add(SettingsFileErrorKind kind, TextSpan span, SettingId setting, string argument, string? other) =>
        errors.Add(new SettingsFileError(kind, span, SettingsFileKeys.KeyOf(setting), argument, other));
}
