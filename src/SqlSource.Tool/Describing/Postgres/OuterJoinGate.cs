using System;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The outer-join gate: whether a query holds a word that can make a <c>NOT NULL</c> column of a table come back
/// <c>NULL</c>.  For a query that is gated the catalog's <c>NOT NULL</c> is not believed.
/// </summary>
/// <remarks>
/// The words are the four of an outer join and the three of the grouping sets, which put <c>NULL</c> into a
/// grouping column on a subtotal row with no join in sight; <c>GROUPING</c> covers <c>GROUPING SETS</c> and the
/// function.  The lexer of the generator says what is a string, a comment or a quoted identifier, and only what
/// is left is searched.  The gate reads words and no structure: <c>left(name, 3)</c> gates a query that has no
/// join, which costs one walk that finds nothing and never a wrong answer.
/// </remarks>
internal static class OuterJoinGate
{
    private static readonly string[] Words = ["LEFT", "RIGHT", "FULL", "OUTER", "ROLLUP", "CUBE", "GROUPING"];

    /// <summary>Whether the SQL holds one of the words, as a whole word and in any case.</summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static bool IsGated(string sql)
    {
        var lexed = SqlLexer.Lex(sql, SqlDialectRules.PostgreSql);
        if (lexed.Error is not null)
        {
            throw new InvalidOperationException(
                "The sample SQL of a query does not lex, and the plan's parser read it."
            );
        }

        foreach (var lexeme in lexed.Lexemes)
        {
            if (lexeme.Kind == SqlLexemeKind.Text && HoldsAWord(sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool HoldsAWord(ReadOnlySpan<char> text)
    {
        foreach (var word in Words)
        {
            var from = 0;
            while (text[from..].IndexOf(word, StringComparison.OrdinalIgnoreCase) is >= 0 and var found)
            {
                var start = from + found;
                var end = start + word.Length;
                if (!IsOfAName(text, start - 1) && !IsOfAName(text, end))
                {
                    return true;
                }

                from = start + 1;
            }
        }

        return false;
    }

    // Whether the character there continues an identifier, by the lexer's rule.  The edge of a lexeme does not.
    private static bool IsOfAName(ReadOnlySpan<char> text, int index) =>
        index >= 0 && index < text.Length && SqlLexer.IsParameterNameCharacter(text[index]);
}
