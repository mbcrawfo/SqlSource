using System.Collections.Generic;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>A query that is one <c>SELECT</c>, with the <c>WITH</c> clause before it.</summary>
/// <param name="With">
/// The <c>WITH</c> clause, from the word <c>WITH</c> to the <c>)</c> of its last CTE, or an empty range before the
/// <c>SELECT</c> when there is none.
/// </param>
/// <param name="Body">The <c>SELECT</c>.</param>
internal sealed record SelectStatement(TokenRange With, SelectBody Body)
{
    /// <summary>
    /// The query, or null when its text is not an optional <c>;</c>, an optional <c>WITH</c> clause and one
    /// <c>SELECT</c> that <see cref="SelectBody.Read" /> follows, ending the text or followed by <c>;</c> alone.
    /// </summary>
    /// <remarks>A misread must prove nothing, so whatever the reader cannot follow gives null.</remarks>
    public static SelectStatement? Read(IReadOnlyList<TsqlToken> tokens)
    {
        var at = tokens.Count > 0 && tokens[0].IsSymbol(";") ? 1 : 0;
        var end = tokens.Count;
        if (end > at && tokens[end - 1].IsSymbol(";"))
        {
            end--;
        }

        var withStart = at;
        var names = new List<string>();
        if (at < end && tokens[at].Is("WITH"))
        {
            at = CtesEnd(tokens, at + 1, end, names);
            if (at < 0)
            {
                return null;
            }
        }

        var body = SelectBody.Read(tokens, new TokenRange(at, end), names);
        return body is null ? null : new SelectStatement(new TokenRange(withStart, at), body);
    }

    // The end of the CTEs after WITH, "name [(columns)] AS (query)" separated by commas, or -1.
    private static int CtesEnd(IReadOnlyList<TsqlToken> tokens, int at, int end, List<string> names)
    {
        while (true)
        {
            // WITH XMLNAMESPACES declares namespaces, and no CTE.
            if (at >= end || !tokens[at].IsName || tokens[at].Is("XMLNAMESPACES"))
            {
                return -1;
            }

            names.Add(tokens[at].Name!);
            at++;
            if (at < end && tokens[at].IsSymbol("("))
            {
                at = ColumnsEnd(tokens, at + 1, end);
                if (at < 0)
                {
                    return -1;
                }
            }

            if (at + 1 >= end || !tokens[at].Is("AS") || !tokens[at + 1].IsSymbol("("))
            {
                return -1;
            }

            var close = SelectBody.ClosingParenthesis(tokens, at + 1, end);
            if (close < 0)
            {
                return -1;
            }

            at = close + 1;
            if (at < end && tokens[at].IsSymbol(","))
            {
                at++;
                continue;
            }

            return at;
        }
    }

    // The end of a CTE's list of columns, "a, b)", or -1.
    private static int ColumnsEnd(IReadOnlyList<TsqlToken> tokens, int at, int end)
    {
        while (at + 1 < end && tokens[at].IsName)
        {
            if (tokens[at + 1].IsSymbol(")"))
            {
                return at + 2;
            }

            if (!tokens[at + 1].IsSymbol(","))
            {
                return -1;
            }

            at += 2;
        }

        return -1;
    }
}
