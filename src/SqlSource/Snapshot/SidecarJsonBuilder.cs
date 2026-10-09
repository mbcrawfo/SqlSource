using System.Globalization;
using System.Text;

namespace SqlSource.Snapshot;

/// <summary>
/// Builds JSON in the layout of the sidecar format design, section 1: two-space indent, line feeds, one key or
/// array element on a line, an empty array or object on the line of its key, and a trailing newline.
/// </summary>
/// <remarks>
/// A null key is an element of an array, or the first object.  A null value is written as <c>null</c>.  A string is
/// written with <c>\"</c> and <c>\\</c>, with <c>\b</c>, <c>\f</c>, <c>\n</c>, <c>\r</c> and <c>\t</c> for those
/// characters, and with <c>\u00XX</c> in upper case for every other character below U+0020; every other character
/// is written as it is.
/// </remarks>
internal sealed class SidecarJsonBuilder
{
    private const string Hex = "0123456789ABCDEF";

    private const string Null = "null";

    private readonly StringBuilder _text = new();

    private int _depth;

    // Whether the container being written has an item already, so that the next one needs a comma before it.
    private bool _hasItem;

    /// <summary>A string as the writer writes one, quotes included.</summary>
    public static string Quote(string value)
    {
        var text = new StringBuilder(value.Length + 2);
        AppendQuoted(text, value);
        return text.ToString();
    }

    public void BeginObject(string? key) => Begin(key, '{');

    public void EndObject() => End('}');

    public void BeginArray(string? key) => Begin(key, '[');

    public void EndArray() => End(']');

    public void WriteString(string? key, string? value)
    {
        Item(key);
        if (value is null)
        {
            _ = _text.Append(Null);
        }
        else
        {
            AppendQuoted(_text, value);
        }
    }

    public void WriteNumber(string? key, int? value)
    {
        Item(key);
        _ = _text.Append(value is { } number ? number.ToString(CultureInfo.InvariantCulture) : Null);
    }

    public void WriteBoolean(string? key, bool? value)
    {
        Item(key);
        _ = _text.Append(
            value switch
            {
                true => "true",
                false => "false",
                null => Null,
            }
        );
    }

    public void WriteNull(string? key)
    {
        Item(key);
        _ = _text.Append(Null);
    }

    /// <summary>The text, with its trailing newline.  Call it once, after the first object is ended.</summary>
    public string ToText() => _text.Append('\n').ToString();

    private static void AppendQuoted(StringBuilder text, string value)
    {
        _ = text.Append('"');
        var run = 0;
        for (var index = 0; index < value.Length; index++)
        {
            var character = value[index];
            if (character is >= ' ' and not '"' and not '\\')
            {
                continue;
            }

            _ = text.Append(value, run, index - run);
            AppendEscape(text, character);
            run = index + 1;
        }

        _ = text.Append(value, run, value.Length - run).Append('"');
    }

    private static void AppendEscape(StringBuilder text, char character) =>
        _ = character switch
        {
            '"' => text.Append("\\\""),
            '\\' => text.Append("\\\\"),
            '\b' => text.Append("\\b"),
            '\f' => text.Append("\\f"),
            '\n' => text.Append("\\n"),
            '\r' => text.Append("\\r"),
            '\t' => text.Append("\\t"),
            _ => text.Append("\\u00").Append(Hex[character >> 4]).Append(Hex[character & 0xF]),
        };

    private void Begin(string? key, char open)
    {
        Item(key);
        _ = _text.Append(open);
        _depth++;
        _hasItem = false;
    }

    private void End(char close)
    {
        _depth--;
        if (_hasItem)
        {
            NewLine();
        }

        _ = _text.Append(close);
        _hasItem = true;
    }

    // Starts an item of the container being written: a comma after the item before it, a line of its own, its key.
    private void Item(string? key)
    {
        if (_hasItem)
        {
            _ = _text.Append(',');
        }

        if (_depth > 0)
        {
            NewLine();
        }

        _hasItem = true;
        if (key is not null)
        {
            AppendQuoted(_text, key);
            _ = _text.Append(": ");
        }
    }

    private void NewLine() => _ = _text.Append('\n').Append(' ', _depth * 2);
}
