using System;

namespace SqlSource.Parsing;

/// <summary>
/// The <c>-- dialect:</c> marker: what a valid one is, and putting a file's into effect before the text after it is
/// lexed.
/// </summary>
/// <remarks>
/// A file's dialect is set in its header: the comments that come before its first SQL and before its first
/// <c>-- name:</c> marker.  The header is read under the dialect the lexer starts with, and the marker applies from
/// the line after it.  Reporting a marker that is wrong or in the wrong place is left to
/// <see cref="SqlFileParser" />.
/// </remarks>
internal static class SqlDialectMarker
{
    /// <summary>
    /// Reads the comments at the start of <paramref name="text" /> from <paramref name="lexer" /> and switches the
    /// lexer to the dialect that the first valid <c>-- dialect:</c> marker among them names.  Returns the offset
    /// where the header ends: a <c>-- dialect:</c> marker that starts at or after it is misplaced.
    /// </summary>
    public static int Apply(SqlLexer lexer, string text)
    {
        var switched = false;
        while (lexer.TryReadLeadingComment(out var comment))
        {
            if (SqlMarkerReader.Read(text, comment) is not { } marker)
            {
                continue;
            }

            if (marker.Kind == SqlMarkerKind.Name)
            {
                return marker.Span.Start;
            }

            if (!switched && marker.Kind == SqlMarkerKind.Dialect && TryRead(text, marker, out var dialect))
            {
                lexer.Rules = SqlDialectRules.For(dialect);
                switched = true;
            }
        }

        return lexer.Position;
    }

    /// <summary>
    /// Reads the value of a <c>-- dialect:</c> marker: the rest of its line, as <see cref="SqlDialectName" /> reads
    /// the MSBuild property.  False for a value that is empty or is not a dialect with its options.  Nothing is
    /// allocated.
    /// </summary>
    public static bool TryRead(string text, SqlMarker marker, out SqlDialectChoice dialect) =>
        SqlDialectName.TryParse(text.AsSpan(marker.ValueSpan.Start, marker.ValueSpan.Length), out dialect);

    /// <summary>
    /// The marker as a message names it: <c>dialect: mysql</c>, or <c>dialect:</c> for one without a value.
    /// </summary>
    public static string Describe(string text, SqlMarker marker) => SqlMarkerReader.Describe(text, marker);
}
