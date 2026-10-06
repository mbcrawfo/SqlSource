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

    private static readonly char[] Reserved = ['&', '<', '>'];

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
    /// Escapes plain text for use as XML content.
    /// </summary>
    public static string Escape(string text)
    {
        var next = text.IndexOfAny(Reserved);
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
                        _ => "&gt;",
                    }
                );
            start = next + 1;
            next = text.IndexOfAny(Reserved, start);
        }

        return escaped.Append(text, start, text.Length - start).ToString();
    }

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
