using System.Text;

namespace SqlSource.Generation;

/// <summary>
/// Writes the documentation comment of a generated member.
/// </summary>
internal static class XmlDocWriter
{
    private const string Prefix = "///";

    // Every character C# treats as the end of a line.  A quoted region of SQL can hold any of them, and one that was
    // copied into a comment as it is would end the comment.
    private static readonly char[] LineTerminators = ['\n', '\r', '\u0085', '\u2028', '\u2029'];

    // Stands in for a character that an XML document cannot hold.  The constant keeps the real one.
    private const string Replacement = "\uFFFD";

    /// <summary>
    /// Appends a <c>summary</c> and a <c>remarks</c> that holds the SQL.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts each line.</param>
    /// <param name="summaryXml">The summary as XML: already escaped, and free to hold tags.</param>
    /// <param name="sql">The SQL as plain text.</param>
    public static void AppendMember(StringBuilder builder, string indent, string summaryXml, string sql)
    {
        AppendLine(builder, indent, "<summary>");
        AppendLines(builder, indent, summaryXml);
        AppendLine(builder, indent, "</summary>");
        AppendLine(builder, indent, "<remarks>");
        AppendLine(builder, indent, "<code>");
        AppendLines(builder, indent, Escape(sql));
        AppendLine(builder, indent, "</code>");
        AppendLine(builder, indent, "</remarks>");
    }

    /// <summary>
    /// Appends the <c>param</c> of a parameter that a token became.
    /// </summary>
    /// <param name="builder">The source being built.</param>
    /// <param name="indent">The whitespace that starts the line.</param>
    /// <param name="name">The token's name, which is the parameter's.  An identifier, so it needs no escaping.</param>
    public static void AppendParam(StringBuilder builder, string indent, string name) =>
        _ = builder
            .Append(indent)
            .Append(Prefix)
            .Append(" <param name=\"")
            .Append(name)
            .Append("\">The text that replaces <c>{{")
            .Append(name)
            .Append("}}</c>.</param>\n");

    /// <summary>
    /// Escapes plain text for use as XML content.  A character that XML 1.0 does not allow, such as a form feed or
    /// the Ctrl-Z at the end of an old file, becomes U+FFFD: left as it is, it would make the comment malformed
    /// (CS1570) in code the consumer cannot change.
    /// </summary>
    public static string Escape(string text)
    {
        var next = IndexOfSpecial(text, 0);
        if (next < 0)
        {
            return text;
        }

        var escaped = new StringBuilder(text.Length + 16);
        var start = 0;
        while (next >= 0)
        {
            _ = escaped
                .Append(text, start, next - start)
                .Append(
                    text[next] switch
                    {
                        '&' => "&amp;",
                        '<' => "&lt;",
                        '>' => "&gt;",
                        _ => Replacement,
                    }
                );
            start = next + 1;
            next = IndexOfSpecial(text, start);
        }

        return escaped.Append(text, start, text.Length - start).ToString();
    }

    // The next character that cannot be copied as it is: one XML reserves, or one XML does not allow.
    private static int IndexOfSpecial(string text, int start)
    {
        var index = start;
        while (index < text.Length)
        {
            var character = text[index];
            if (character is '&' or '<' or '>')
            {
                return index;
            }

            // A surrogate pair is one character, and XML allows it.  A surrogate on its own is not allowed.
            if (char.IsHighSurrogate(character) && index + 1 < text.Length && char.IsLowSurrogate(text[index + 1]))
            {
                index += 2;
                continue;
            }

            if (!IsAllowedInXml(character))
            {
                return index;
            }

            index++;
        }

        return -1;
    }

    // XML 1.0: tab, line feed, carriage return, and everything from a space up, except lone surrogates and the two
    // code points at the end of the plane.
    private static bool IsAllowedInXml(char character) =>
        character is '\t' or '\n' or '\r' or (>= ' ' and < '\uD800') or (>= '\uE000' and <= '\uFFFD');

    // One comment line for each line of the text.
    private static void AppendLines(StringBuilder builder, string indent, string text)
    {
        var start = 0;
        while (true)
        {
            var end = text.IndexOfAny(LineTerminators, start);
            if (end < 0)
            {
                AppendLine(builder, indent, text, start, text.Length - start);
                return;
            }

            AppendLine(builder, indent, text, start, end - start);
            var isPair = text[end] == '\r' && end + 1 < text.Length && text[end + 1] == '\n';
            start = end + (isPair ? 2 : 1);
        }
    }

    private static void AppendLine(StringBuilder builder, string indent, string text) =>
        AppendLine(builder, indent, text, 0, text.Length);

    private static void AppendLine(StringBuilder builder, string indent, string text, int start, int length)
    {
        _ = builder.Append(indent).Append(Prefix);
        if (length > 0)
        {
            _ = builder.Append(' ').Append(text, start, length);
        }

        _ = builder.Append('\n');
    }
}
