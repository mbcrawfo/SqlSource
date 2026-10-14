using System;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The outer-join gate: whether a query holds a word that can make a <c>NOT NULL</c> column of a table come back
/// <c>NULL</c>.  For a query that is gated the catalog's <c>NOT NULL</c> is believed only where the walk proves that
/// no join extends the column.
/// </summary>
/// <remarks>
/// The words are the four of an outer join, the three of the grouping sets and three more.  The grouping sets put
/// <c>NULL</c> into a grouping column on a subtotal row with no join in sight; <c>GROUPING</c> covers
/// <c>GROUPING SETS</c> and the function.  <c>OLD</c> and <c>NEW</c> are the old and the new row of a <c>RETURNING</c>
/// list of PostgreSQL 18, all <c>NULL</c> for the row an <c>INSERT</c> had no old one of and a <c>DELETE</c> has no new
/// one of; <c>MERGE</c> has a <c>RETURNING</c> list that holds the source's columns as <c>NULL</c> for a row that
/// <c>WHEN NOT MATCHED BY SOURCE</c> acted on.  Each keeps the table's origin in the row description.  The walk answers
/// for them as for any gated query: a <c>MERGE</c> with <c>WHEN NOT MATCHED BY SOURCE</c> plans as a join that
/// null-extends its source, and in a statement that names <c>OLD</c> or <c>NEW</c> only the target's alias is mapped at
/// a root <c>ModifyTable</c>, since an alias of an inner scope can print under the name of the old or the new row.
/// What the list must keep is the rule that a column is never <c>false</c> when the server can return <c>NULL</c> for
/// it.  The lexer of the generator says what is a string, a comment or a quoted identifier, and only what is left is
/// searched.  The gate reads words and no structure: <c>left(name, 3)</c> gates a query that has no join, which costs
/// one walk that finds nothing and never a wrong answer.
/// </remarks>
internal static class OuterJoinGate
{
    private static readonly string[] Words =
    [
        "LEFT",
        "RIGHT",
        "FULL",
        "OUTER",
        "ROLLUP",
        "CUBE",
        "GROUPING",
        "OLD",
        "NEW",
        "MERGE",
    ];

    private static readonly string[] OldAndNew = ["OLD", "NEW"];

    /// <summary>Whether the SQL holds one of the words, as a whole word and in any case.</summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static bool IsGated(string sql) => HoldsAnyOf(sql, Words);

    /// <summary>
    /// Whether the SQL holds <c>OLD</c> or <c>NEW</c>, as a whole word and in any case: whether a reference at a
    /// <c>ModifyTable</c> may be PostgreSQL 18's old or new row rather than a table's alias.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static bool NamesOldOrNew(string sql) => HoldsAnyOf(sql, OldAndNew);

    // Whether a lexeme of text, which is no string, comment or quoted identifier, holds one of the words.
    private static bool HoldsAnyOf(string sql, string[] words)
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
            if (
                lexeme.Kind == SqlLexemeKind.Text
                && HoldsAWord(sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length), words)
            )
            {
                return true;
            }
        }

        return false;
    }

    private static bool HoldsAWord(ReadOnlySpan<char> text, string[] words)
    {
        foreach (var word in words)
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
