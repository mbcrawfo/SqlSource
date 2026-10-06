namespace SqlSource.Parsing;

/// <summary>
/// Finds the <c>dialect=</c> directive of a file and puts it into effect, before the text after it is lexed.
/// </summary>
/// <remarks>
/// A file's dialect is set in its header: the comments that come before its first SQL and before its first
/// <c>-- name:</c> marker.  The header is read under the dialect the lexer starts with, and the directive applies
/// from the line after it.  Reporting a directive that is wrong or in the wrong place is left to
/// <see cref="SqlDirectiveScope" />.
/// </remarks>
internal static class SqlPreambleDialect
{
    /// <summary>
    /// Reads the comments at the start of <paramref name="text" /> from <paramref name="lexer" /> and switches the
    /// lexer to the dialect that the first valid <c>dialect=</c> directive among them names.  Returns the offset
    /// where the header ends: a <c>dialect=</c> directive that starts at or after it is misplaced.
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

            if (
                !switched
                && marker.Kind == SqlMarkerKind.Directives
                && SqlDirectiveScope.TryFindDialect(text, marker, out var dialect)
            )
            {
                lexer.Rules = SqlDialectRules.For(dialect);
                switched = true;
            }
        }

        return lexer.Position;
    }
}
