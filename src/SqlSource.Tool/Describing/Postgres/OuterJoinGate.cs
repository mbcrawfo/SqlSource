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
/// searched for the words.  A quoted identifier counts for <c>OLD</c> and <c>NEW</c> alone, since PostgreSQL 18
/// resolves <c>"old"</c> and <c>"new"</c> as the old and the new row: one whose name, with <c>""</c> read as one
/// quote, is either word in any case.  Any Unicode-escaped identifier, <c>U&amp;"..."</c>, counts as both, because the
/// tool does not decode its escapes.  No other word has a quoted form, and a string never counts.  The gate reads
/// words and no structure: <c>left(name, 3)</c> gates a query that has no join, which costs one walk that finds
/// nothing and never a wrong answer.
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

    /// <summary>
    /// Whether the SQL holds one of the words, as a whole word and in any case.  <c>OLD</c> and <c>NEW</c> count as a
    /// quoted identifier too, since PostgreSQL 18 resolves <c>"old"</c> and <c>"new"</c>, and so does any
    /// Unicode-escaped identifier, which the tool does not decode.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static bool IsGated(string sql) => HoldsAnyOf(sql, Words);

    /// <summary>
    /// Whether the SQL holds <c>OLD</c> or <c>NEW</c>, as a whole word and in any case: whether a reference at a
    /// <c>ModifyTable</c> may be PostgreSQL 18's old or new row rather than a table's alias.  A quoted identifier
    /// counts, since PostgreSQL 18 resolves <c>"old"</c> and <c>"new"</c>, and so does any Unicode-escaped
    /// identifier, which the tool does not decode.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static bool NamesOldOrNew(string sql) => HoldsAnyOf(sql, OldAndNew);

    // Whether a lexeme of text, which is no string, comment or quoted identifier, holds one of the words, or a quoted
    // identifier may name the old or the new row.  Both lists hold OLD and NEW, the one pair of words with a quoted
    // form.
    private static bool HoldsAnyOf(string sql, string[] words)
    {
        SqlLexeme? before = null;
        foreach (var lexeme in PostgresSql.Lex(sql))
        {
            var text = sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length);
            if (
                (lexeme.Kind == SqlLexemeKind.Text && HoldsAWord(text, words))
                || (lexeme.Kind == SqlLexemeKind.Quoted && MayNameOldOrNew(sql, text, before))
            )
            {
                return true;
            }

            before = lexeme;
        }

        return false;
    }

    // Whether a quoted lexeme is an identifier that may name the old or the new row: "old" or "new" in any case, or
    // one that U& opens at the end of the text before it, whose escapes are not decoded.  A doubled quote leaves a
    // quote in the name, which neither word holds, so the text between the quotes is compared as it stands.
    private static bool MayNameOldOrNew(string sql, ReadOnlySpan<char> quoted, SqlLexeme? before)
    {
        if (quoted.Length < 2 || quoted[0] != '"')
        {
            return false;
        }

        var name = quoted[1..^1];
        if (
            name.Equals(OldAndNew[0], StringComparison.OrdinalIgnoreCase)
            || name.Equals(OldAndNew[1], StringComparison.OrdinalIgnoreCase)
        )
        {
            return true;
        }

        return before is { Kind: SqlLexemeKind.Text, Span: var span }
            && sql.AsSpan(span.Start, span.Length).EndsWith("U&", StringComparison.OrdinalIgnoreCase);
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

    // Whether the character there continues a word, as PostgreSQL reads one, so that "x$left" and "€left" hold no
    // "left".  The edge of a lexeme does not.
    private static bool IsOfAName(ReadOnlySpan<char> text, int index) =>
        index >= 0 && index < text.Length && PostgresIdentifiers.IsWordPart(text[index]);
}
