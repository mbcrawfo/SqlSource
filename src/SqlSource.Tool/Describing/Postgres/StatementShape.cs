using System;
using System.Collections.Frozen;
using System.Globalization;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing.Postgres;

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
/// (<c>REL_18_STABLE</c>) that <c>PREPARE</c> does not take, which are the words of the design plus <c>ANALYSE</c>,
/// the other spelling of <c>ANALYZE</c> that the grammar has.  Every other statement of the production starts with a
/// word on the list: the <c>ALTER</c>, <c>CREATE</c> and <c>DROP</c> forms, <c>SET</c> for the variable and for
/// <c>SET CONSTRAINTS</c>, <c>SECURITY</c> for <c>SECURITY LABEL</c>, and <c>BEGIN</c>, <c>START</c>, <c>COMMIT</c>,
/// <c>END</c>, <c>ROLLBACK</c>, <c>ABORT</c>, <c>SAVEPOINT</c>, <c>RELEASE</c> and <c>PREPARE</c> for the
/// transactions.  <c>SELECT … INTO</c> is no first word: <see cref="RefusedKind" /> reads the <c>INTO</c> itself.
/// </para>
/// </remarks>
internal static class StatementShape
{
    private const string SelectInto = "SELECT INTO";

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
    /// its first word when that is on the list of words to refuse, <c>SELECT INTO</c> for a word <c>INTO</c> at
    /// parenthesis depth 0 that does not directly follow <c>INSERT</c> or <c>MERGE</c>, with only white space and
    /// comments between.  Only <c>Text</c> lexemes hold words, so a string, a quoted identifier, a comment and a
    /// parameter never count.  The first word is the first thing after comments, hints, white space and any leading
    /// <c>(</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex: a bug of the plan, whose parser read the same text.
    /// </exception>
    public static string? RefusedKind(string sql)
    {
        var first = true;
        var depth = 0;
        string? previous = null;
        foreach (var lexeme in Lexemes(sql))
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
                if (IsWordCharacter(c))
                {
                    var start = index;
                    while (index < text.Length && IsWordCharacter(text[index]))
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
                    else if (depth == 0 && IsInto(word) && !FollowsInsertOrMerge(previous))
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

                previous = null;
            }
        }

        return null;
    }

    private static bool IsInto(string word) => string.Equals(word, "INTO", StringComparison.OrdinalIgnoreCase);

    private static bool FollowsInsertOrMerge(string? previous) =>
        previous is not null
        && (
            string.Equals(previous, "INSERT", StringComparison.OrdinalIgnoreCase)
            || string.Equals(previous, "MERGE", StringComparison.OrdinalIgnoreCase)
        );

    private static bool IsWordCharacter(char c) => char.IsLetterOrDigit(c) || c is '_' or '$';

    private static EquatableArray<SqlLexeme> Lexemes(string sql)
    {
        var lexed = SqlLexer.Lex(sql, SqlDialectRules.PostgreSql);
        if (lexed.Error is not null)
        {
            throw new InvalidOperationException(
                "The sample SQL of a query does not lex, and the plan's parser read it."
            );
        }

        return lexed.Lexemes;
    }
}
