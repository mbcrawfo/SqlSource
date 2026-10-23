using System;
using System.Collections.Frozen;
using System.Globalization;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres.Statements;

/// <summary>
/// What kind of statement a query is, read from its lexemes before anything is sent.
/// </summary>
/// <remarks>
/// <para>
/// <c>PREPARE</c> takes <c>SELECT</c>, <c>INSERT</c>, <c>UPDATE</c>, <c>DELETE</c>, <c>VALUES</c> and <c>TABLE</c>, and
/// <c>MERGE</c> from 15, with <c>WITH</c> around any of them.  It refuses every other command with <c>42601</c> at
/// the word, which a typo such as <c>SELEC 1</c> gets too, so the server's error cannot tell the two apart.  The
/// first word is therefore compared with a list of words to refuse, never with a list of words to allow: a word that
/// is on neither list reaches the server and stays <c>SQLSRC225</c>.
/// </para>
/// <para>
/// The list is the first words of the statements in the <c>stmt</c> production of PostgreSQL 18's <c>gram.y</c>
/// (<c>REL_18_STABLE</c>) that <c>PREPARE</c> does not take, <c>ANALYSE</c> among them,
/// the other spelling of <c>ANALYZE</c> that the grammar has.  Every other statement of the production starts with a
/// word on the list: the <c>ALTER</c>, <c>CREATE</c> and <c>DROP</c> forms, <c>SET</c> for the variable and for
/// <c>SET CONSTRAINTS</c>, <c>SECURITY</c> for <c>SECURITY LABEL</c>, and <c>BEGIN</c>, <c>START</c>, <c>COMMIT</c>,
/// <c>END</c>, <c>ROLLBACK</c>, <c>ABORT</c>, <c>SAVEPOINT</c>, <c>RELEASE</c> and <c>PREPARE</c> for the
/// transactions.  <c>SELECT … INTO</c> is no first word: <see cref="RefusedKind" /> reads the <c>INTO</c> itself.  An
/// <c>INTO</c> directly after <c>AS</c> or after a <c>.</c> is a column label, as in <c>SELECT 1 AS into</c> and
/// <c>SELECT t.into FROM t</c>, and is not the clause; the <c>.</c> that ends a number, <c>SELECT 1. INTO t</c>, is
/// no label's.
/// </para>
/// <para>
/// <see cref="PositionalParameter" /> reads the same lexemes for a <c>$n</c> that the SQL writes itself.
/// </para>
/// </remarks>
internal static class StatementShape
{
    private const string SelectInto = "SELECT INTO";

    // The "word" that stands for a "." in front of the next one: no word is spelled so.
    private const string Dot = ".";

    private static readonly FrozenSet<string> Refused = FrozenSet.ToFrozenSet(
        [
            "ABORT",
            "ALTER",
            "ANALYSE",
            "ANALYZE",
            "BEGIN",
            "CALL",
            "CHECKPOINT",
            "CLOSE",
            "CLUSTER",
            "COMMENT",
            "COMMIT",
            "COPY",
            "CREATE",
            "DEALLOCATE",
            "DECLARE",
            "DISCARD",
            "DO",
            "DROP",
            "END",
            "EXECUTE",
            "EXPLAIN",
            "FETCH",
            "GRANT",
            "IMPORT",
            "LISTEN",
            "LOAD",
            "LOCK",
            "MOVE",
            "NOTIFY",
            "PREPARE",
            "REASSIGN",
            "REFRESH",
            "REINDEX",
            "RELEASE",
            "RESET",
            "REVOKE",
            "ROLLBACK",
            "SAVEPOINT",
            "SECURITY",
            "SET",
            "SHOW",
            "START",
            "TRUNCATE",
            "UNLISTEN",
            "VACUUM",
        ],
        StringComparer.OrdinalIgnoreCase
    );

    /// <summary>
    /// The kind of the statement in upper case when <c>PREPARE</c> does not take it, or null for a statement to send:
    /// its first word when that is on the list of words to refuse, <c>SELECT INTO</c> for a word <c>INTO</c> at the
    /// parenthesis depth of the first word, so that <c>(SELECT 1 INTO t)</c> counts and a subquery does not, that
    /// does not directly follow <c>INSERT</c>, <c>MERGE</c>, <c>AS</c> or a <c>.</c> after a name, with only white
    /// space and comments between.  Only <c>Text</c> lexemes hold words, so a string, a quoted identifier,
    /// a comment and a parameter never count.  The first word is the first thing after comments, hints, white space
    /// and any leading <c>(</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static string? RefusedKind(string sql)
    {
        var first = true;
        var depth = 0;

        // The "(" before the first word: the statement's own depth.
        var leading = 0;
        string? previous = null;
        foreach (var lexeme in DescriberSql.Lex(sql, SqlDialectRules.PostgreSql))
        {
            if (lexeme.Kind is SqlLexemeKind.LineComment or SqlLexemeKind.BlockComment or SqlLexemeKind.Hint)
            {
                continue;
            }

            if (lexeme.Kind != SqlLexemeKind.Text)
            {
                // A string, a quoted identifier or a parameter is no word, and stands between its neighbours.
                if (first)
                {
                    return null;
                }

                previous = null;
                continue;
            }

            var text = sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length);
            var index = 0;
            while (index < text.Length)
            {
                var c = text[index];
                if (PostgresIdentifiers.IsWordPart(c))
                {
                    var start = index;
                    while (index < text.Length && PostgresIdentifiers.IsWordPart(text[index]))
                    {
                        index++;
                    }

                    var word = text[start..index].ToString();
                    if (first)
                    {
                        first = false;
                        if (Refused.Contains(word))
                        {
                            return word.ToUpper(CultureInfo.InvariantCulture);
                        }
                    }
                    else if (depth == leading && IsInto(word) && !FollowsInsertOrMerge(previous) && !IsLabel(previous))
                    {
                        return SelectInto;
                    }

                    previous = word;
                    continue;
                }

                index++;
                if (char.IsWhiteSpace(c))
                {
                    continue;
                }

                if (c == '(')
                {
                    depth++;
                    if (first)
                    {
                        leading++;
                    }
                }
                else if (c == ')')
                {
                    depth--;
                }

                // Anything but white space and a leading "(" before the first word is not a statement to refuse.
                if (first && c != '(')
                {
                    return null;
                }

                // A "." after a number ends the number, as in "1.", and is no qualifier of a label.
                previous = c == '.' && !IsNumber(previous) ? Dot : null;
            }
        }

        return null;
    }

    /// <summary>
    /// The first positional parameter that the SQL writes itself, <c>$</c> and its digits as written (<c>$2</c>), or
    /// null.  A <c>$</c> and an ASCII digit in a <c>Text</c> lexeme is one unless the <c>$</c> continues a name
    /// (<c>a$1</c>, <c>€$1</c>, <c>1a$1</c>), as PostgreSQL reads one, or follows another <c>$</c> in the same run
    /// (<c>$$1</c>); after a number it is one (<c>1$1</c>).  A
    /// <c>$</c> that starts a lexeme is never part of a name.  A string, a quoted identifier, a dollar-quoted string
    /// and a comment are lexemes of their own and are not read.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static string? PositionalParameter(string sql)
    {
        foreach (var lexeme in DescriberSql.Lex(sql, SqlDialectRules.PostgreSql))
        {
            if (lexeme.Kind != SqlLexemeKind.Text)
            {
                continue;
            }

            var text = sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length);
            for (var index = 0; index < text.Length - 1; index++)
            {
                if (text[index] != '$' || !char.IsAsciiDigit(text[index + 1]))
                {
                    continue;
                }

                if (ContinuesAName(text, index))
                {
                    continue;
                }

                var end = index + 1;
                while (end < text.Length && char.IsAsciiDigit(text[end]))
                {
                    end++;
                }

                return text[index..end].ToString();
            }
        }

        return null;
    }

    private static bool IsInto(string word) => string.Equals(word, "INTO", StringComparison.OrdinalIgnoreCase);

    // An INTO directly after AS or after a "." is a column label, as in "SELECT 1 AS into" and "SELECT t.into".
    private static bool IsLabel(string? previous) =>
        previous is Dot || string.Equals(previous, "AS", StringComparison.OrdinalIgnoreCase);

    // A word that starts with a digit is a number: a name never does.
    private static bool IsNumber(string? word) => word is { Length: > 0 } && char.IsAsciiDigit(word[0]);

    private static bool FollowsInsertOrMerge(string? previous) =>
        previous is not null
        && (
            string.Equals(previous, "INSERT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "MERGE", StringComparison.OrdinalIgnoreCase)
        );

    // Whether the "$" at the index stands in a run of word characters that is no number: one holds a character that
    // starts a word, and the "$" continues that name, or holds another "$", as "$$1" and "1$$1" do, which PostgreSQL
    // reads as a dollar quote.  A run of digits alone is a number, after which the "$" stands apart.
    private static bool ContinuesAName(ReadOnlySpan<char> text, int index)
    {
        for (var at = index - 1; at >= 0 && PostgresIdentifiers.IsWordPart(text[at]); at--)
        {
            if (text[at] == '$' || PostgresIdentifiers.IsWordStart(text[at]))
            {
                return true;
            }
        }

        return false;
    }
}
