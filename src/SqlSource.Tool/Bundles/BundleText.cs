using System.Globalization;
using System.Text;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// A text that <c>replay</c> prints from a bundle, made safe to print.  A bundle may come from anyone, as one attached
/// to a public issue does, and a control character in it, such as the escape that starts a terminal's sequence, would
/// let the bundle move the cursor, rewrite what was printed or reach the clipboard.
/// </summary>
/// <remarks>
/// Every control character but the tab, those of C0, DEL and those of C1, is written as <c>\uXXXX</c> in upper case,
/// as the JSON writer writes one, so a line of an event and a line of an error spell it alike.  A line break is one of
/// them, so the text stays on its line.  Nothing else is changed: a text that a JSON writer wrote holds no control
/// character, and comes out as it went in.
/// </remarks>
internal static class BundleText
{
    /// <summary>The text with each control character but the tab written as <c>\uXXXX</c>.</summary>
    public static string Of(string text)
    {
        var first = FirstEscaped(text);
        if (first < 0)
        {
            return text;
        }

        var escaped = new StringBuilder(text.Length + 10).Append(text, 0, first);
        for (var index = first; index < text.Length; index++)
        {
            var character = text[index];
            _ = IsEscaped(character)
                ? escaped.Append("\\u").Append(((int)character).ToString("X4", CultureInfo.InvariantCulture))
                : escaped.Append(character);
        }

        return escaped.ToString();
    }

    private static int FirstEscaped(string text)
    {
        for (var index = 0; index < text.Length; index++)
        {
            if (IsEscaped(text[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static bool IsEscaped(char character) => character != '\t' && char.IsControl(character);
}
