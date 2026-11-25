using System;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The outer-join gate: whether a query holds a word that can make a <c>NOT NULL</c> column of a table come back
/// <c>NULL</c>.  For a query that is gated the catalog's <c>NOT NULL</c> is believed only where the walk proves that
/// no join extends the column.
/// </summary>
/// <remarks>
/// <para>
/// The words are the four of an outer join, the three of the grouping sets and three more.  The grouping sets put
/// <c>NULL</c> into a grouping column on a subtotal row with no join in sight; <c>GROUPING</c> covers
/// <c>GROUPING SETS</c> and the function.  <c>OLD</c> and <c>NEW</c> are the old and the new row of a <c>RETURNING</c>
/// list of PostgreSQL 18, all <c>NULL</c> for the row an <c>INSERT</c> had no old one of and a <c>DELETE</c> has no new
/// one of; <c>MERGE</c> has a <c>RETURNING</c> list that holds the source's columns as <c>NULL</c> for a row that
/// <c>WHEN NOT MATCHED BY SOURCE</c> acted on.  Each keeps the table's origin in the row description.  The walk answers
/// for them as for any gated query: a <c>MERGE</c> with <c>WHEN NOT MATCHED BY SOURCE</c> plans as a join that
/// null-extends its source, and at a root <c>ModifyTable</c> an alias that may name the old or the new row is not
/// mapped (<see cref="Statements.OldAndNewNames" />).  What the list must keep is the rule that a column is never
/// <c>false</c> when the server can return <c>NULL</c> for it.  The lexer of the generator says what is a string, a
/// comment or a quoted identifier, and only what is left is searched for the words.
/// </para>
/// <para>
/// A quoted identifier counts for <c>OLD</c> and <c>NEW</c> alone, since PostgreSQL 18 resolves <c>"old"</c> and
/// <c>"new"</c> as the old and the new row: one whose name is <c>old</c> or <c>new</c> as the server compares a name,
/// exactly, with <c>""</c> read as one quote and a <c>U&amp;"..."</c> one decoded (<see cref="PostgresIdentifiers" />).
/// A Unicode-escaped identifier that cannot be decoded counts.  No other word has a quoted form, and a string never
/// counts.
/// </para>
/// <para>
/// A word of a join that a <c>(</c> follows, past white space and comments, does not count: <c>LEFT</c>,
/// <c>RIGHT</c>, <c>FULL</c> and <c>OUTER</c> are reserved and no alias can take them, so a <c>(</c> after one is a
/// call, <c>left(name, 3)</c>, which joins nothing.  The words of the grouping sets are followed by one in their own
/// syntax and always count.  Otherwise the gate reads words and no structure: a word may gate a query that has no
/// join, which costs one walk that finds nothing and never a wrong answer.
/// </para>
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

    // The words of a join, each reserved as no alias can be, so that one a "(" follows is a function's name.
    private static readonly string[] JoinWords = ["LEFT", "RIGHT", "FULL", "OUTER"];

    private static readonly string[] OldAndNew = ["OLD", "NEW"];

    private static readonly string[] Insert = ["INSERT"];

    /// <summary>
    /// Whether the SQL holds one of the words, as a whole word and in any case, but a word of a join that is a
    /// function's name.  <c>OLD</c> and <c>NEW</c> count as a quoted identifier too, since PostgreSQL 18 resolves
    /// <c>"old"</c> and <c>"new"</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static bool IsGated(string sql) => HoldsAnyOf(sql, Words);

    /// <summary>
    /// Whether the SQL holds <c>OLD</c> or <c>NEW</c>, as a whole word and in any case, or a quoted identifier of
    /// either name: whether a reference may be PostgreSQL 18's old or new row.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static bool NamesOldOrNew(string sql) => HoldsAnyOf(sql, OldAndNew);

    /// <summary>
    /// Whether the SQL holds the word <c>INSERT</c>: whether the statement may write the values a <c>RETURNING</c> list
    /// returns.  A word and no structure, which costs a walk that finds nothing and never a wrong answer.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static bool NamesInsert(string sql) => HoldsAnyOf(sql, Insert);

    // Whether a lexeme of text, which is no string, comment or quoted identifier, holds one of the words, or, for a
    // list that holds OLD, a quoted identifier names the old or the new row: OLD and NEW are the one pair of words with
    // a quoted form.
    private static bool HoldsAnyOf(string sql, string[] words)
    {
        var quotedCounts = Array.IndexOf(words, OldAndNew[0]) >= 0;
        var lexemes = DescriberSql.Lex(sql, SqlDialectRules.PostgreSql);
        for (var index = 0; index < lexemes.Count; index++)
        {
            var lexeme = lexemes[index];
            if (
                (lexeme.Kind == SqlLexemeKind.Text && HoldsAWord(sql, lexemes, index, words))
                || (quotedCounts && NamesOldOrNewQuoted(sql, lexemes, index))
            )
            {
                return true;
            }
        }

        return false;
    }

    // Whether the lexeme is a quoted identifier whose name is old or new, as the server compares a name, or a
    // Unicode-escaped one whose escapes cannot be decoded.
    private static bool NamesOldOrNewQuoted(string sql, EquatableArray<SqlLexeme> lexemes, int index) =>
        PostgresIdentifiers.TryReadQuotedName(sql, lexemes, index, out var name, out _)
        && name is null or "old" or "new";

    private static bool HoldsAWord(string sql, EquatableArray<SqlLexeme> lexemes, int index, string[] words)
    {
        var span = lexemes[index].Span;
        var text = sql.AsSpan(span.Start, span.Length);
        foreach (var word in words)
        {
            var from = 0;
            while (text[from..].IndexOf(word, StringComparison.OrdinalIgnoreCase) is >= 0 and var found)
            {
                var start = from + found;
                var end = start + word.Length;
                if (
                    !IsOfAName(text, start - 1)
                    && !IsOfAName(text, end)
                    && !(IsJoinWord(word) && CallFollows(sql, lexemes, index, span.Start + end))
                )
                {
                    return true;
                }

                from = start + 1;
            }
        }

        return false;
    }

    private static bool IsJoinWord(string word) => Array.IndexOf(JoinWords, word) >= 0;

    // Whether the first character after the index, past white space and comments, is "(": the word before it is then
    // a function's name, since no join's word is ever followed by one.
    private static bool CallFollows(string sql, EquatableArray<SqlLexeme> lexemes, int index, int from)
    {
        for (var at = index; at < lexemes.Count; at++)
        {
            var lexeme = lexemes[at];
            if (DescriberSql.IsCommentOrHint(lexeme.Kind))
            {
                continue;
            }

            if (lexeme.Kind != SqlLexemeKind.Text)
            {
                return false;
            }

            var start = Math.Max(from, lexeme.Span.Start);
            var rest = sql.AsSpan(start, lexeme.Span.Start + lexeme.Span.Length - start).TrimStart();
            if (!rest.IsEmpty)
            {
                return rest[0] == '(';
            }
        }

        return false;
    }

    // Whether the character there continues a word, as PostgreSQL reads one, so that "x$left" and "€left" hold no
    // "left".  The edge of a lexeme does not.
    private static bool IsOfAName(ReadOnlySpan<char> text, int index) =>
        index >= 0 && index < text.Length && PostgresIdentifiers.IsWordPart(text[index]);
}
