using System.Collections.Generic;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.SettingsFiles;

/// <summary>
/// The raw value of a setting in a settings file, with where it is: a string, an array of strings or an object of
/// strings.  An element or a member whose value is not a string is not held: the reader reports it and leaves it out,
/// but for a member of the column types written as an object, which is held as the text of its type, beside whether it
/// says <c>"json": true</c>.
/// </summary>
internal sealed class SettingsFileValue
{
    private SettingsFileValue(
        string? text,
        List<string>? elements,
        List<KeyValuePair<string, string>>? members,
        List<TextSpan> spans,
        List<TextSpan>? keySpans,
        List<bool>? json
    )
    {
        Text = text;
        Elements = elements;
        Members = members;
        Spans = spans;
        KeySpans = keySpans;
        Json = json;
    }

    /// <summary>The value of a string, or null for an array or an object.</summary>
    public string? Text { get; }

    /// <summary>The elements of an array, or null for a string or an object.</summary>
    public List<string>? Elements { get; }

    /// <summary>
    /// The key and the value of each member of an object, in order, or null for a string or an array.
    /// </summary>
    public List<KeyValuePair<string, string>>? Members { get; }

    /// <summary>
    /// Where each string is, quotes included: the one of a string, each element of an array, or the value of each
    /// member of an object, the whole of a value written as an object.
    /// </summary>
    public List<TextSpan> Spans { get; }

    /// <summary>
    /// Where the key of each member of an object is, quotes included, or null for a string or an array.
    /// </summary>
    public List<TextSpan>? KeySpans { get; }

    /// <summary>
    /// Whether each member of an object says that its column holds JSON, or null for a string, an array, or an object
    /// of a setting other than the column types.
    /// </summary>
    public List<bool>? Json { get; }

    public static SettingsFileValue String(string text, TextSpan span) => new(text, null, null, [span], null, null);

    public static SettingsFileValue Array(List<string> elements, List<TextSpan> spans) =>
        new(null, elements, null, spans, null, null);

    public static SettingsFileValue Object(
        List<KeyValuePair<string, string>> members,
        List<TextSpan> keySpans,
        List<TextSpan> valueSpans,
        List<bool>? json
    ) => new(null, null, members, valueSpans, keySpans, json);
}
