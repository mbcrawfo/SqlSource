using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SqlSource.Parsing;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The text a query is prepared with: its one statement with <c>$n</c> in the place of each parameter, the type
/// list, and the <c>PREPARE</c> around both.
/// </summary>
/// <remarks>
/// <para>
/// The lexer of the generator says what is a parameter and what is a string, a comment or a quoted identifier, under
/// the rules of <c>postgres</c>, so the tool and the generator read a query one way.  A text in which the lexer
/// finds a second statement is refused before anything is sent, <c>SQLSRC230</c>, which tells the user what is
/// wrong.
/// </para>
/// <para>
/// That refusal is the first one and no guarantee, since the lexer and the server do not always split a text alike:
/// <c>SELECT 1 AS §$x$; DROP TABLE t; --$x$</c> is one statement to the lexer, which reads <c>$x$</c> after a
/// <c>§</c> as the start of a dollar-quoted string, and two to the server, which reads <c>§$x$</c> as an
/// identifier.  What keeps a second statement from being run is how the text is sent:
/// <see cref="PostgresConnection.ExecuteAsOneStatementAsync" /> gives it to the driver as a batch of one command,
/// which the driver does not split, and the server refuses a text of two statements whole.
/// </para>
/// </remarks>
internal static class ParameterPlaceholders
{
    /// <summary>The item of the type list for a parameter with no declared type: the server infers it.</summary>
    public const string Unknown = "unknown";

    private const string Separator = ", ";

    /// <summary>
    /// The SQL with <c>$</c> and the one-based ordinal of its name in the list for each parameter, and without its
    /// trailing <c>;</c> and what follows it.  Null when a <c>;</c> is followed by anything but white space and
    /// comments: the query holds more than one statement.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The SQL does not lex, or holds a parameter that the list does not: a bug of the plan, whose parser read the
    /// same text.
    /// </exception>
    public static string? Rewrite(string sql, EquatableArray<SqlQueryParameter> parameters)
    {
        var lexed = SqlLexer.Lex(sql, SqlDialectRules.PostgreSql);
        if (lexed.Error is not null)
        {
            throw new InvalidOperationException(
                "The sample SQL of a query does not lex, and the plan's parser read it."
            );
        }

        var rewritten = new StringBuilder(sql.Length);
        for (var index = 0; index < lexed.Lexemes.Count; index++)
        {
            var lexeme = lexed.Lexemes[index];
            var text = sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length);
            if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                _ = rewritten.Append('$').Append(OrdinalOf(text[1..], parameters));
                continue;
            }

            var end = lexeme.Kind == SqlLexemeKind.Text ? text.IndexOf(';') : -1;
            if (end < 0)
            {
                _ = rewritten.Append(text);
                continue;
            }

            // The first ";" of the SQL.  It is the trailing one when nothing but white space and comments follows.
            return text[(end + 1)..].IsWhiteSpace() && OnlyCommentsFollow(sql, lexed.Lexemes, index + 1)
                ? rewritten.Append(text[..end]).ToString()
                : null;
        }

        return rewritten.ToString();
    }

    /// <summary>
    /// One item for each parameter of the list, in order: the declared type as the marker wrote it, or
    /// <see cref="Unknown" />.  A declared parameter that the SQL does not hold is in the list all the same: the
    /// server accepts a list longer than the placeholders used.  The failure is the first parameter whose type is
    /// not written as a type name.
    /// </summary>
    public static Outcome<IReadOnlyList<string>, SqlQueryParameter> TypeList(
        EquatableArray<SqlQueryParameter> parameters
    )
    {
        var types = new List<string>(parameters.Count);
        for (var index = 0; index < parameters.Count; index++)
        {
            var parameter = parameters[index];
            if (parameter.Type is null)
            {
                types.Add(Unknown);
            }
            else if (DeclaredTypeText.TryRead(parameter.Type, out var type))
            {
                types.Add(type);
            }
            else
            {
                return Outcome<IReadOnlyList<string>, SqlQueryParameter>.Failed(parameter);
            }
        }

        return Outcome<IReadOnlyList<string>, SqlQueryParameter>.Of(types);
    }

    /// <summary>
    /// The statement that is sent.  The type list ends with a line break before <c>AS</c>: whatever a type's text
    /// held, the rest of the statement is not on its line.  A query without parameters has no list.
    /// </summary>
    public static string PrepareText(string statement, IReadOnlyList<string> types, string sql) =>
        Head(statement, types) + sql;

    /// <summary>
    /// How many characters of <see cref="PrepareText" /> stand before the SQL, counted as the server counts a
    /// position: a character outside the basic plane once.
    /// </summary>
    public static int HeadLength(string statement, IReadOnlyList<string> types) => Characters(Head(statement, types));

    /// <summary>
    /// The index of the item of the type list that a position of the server, counted from one, falls in.  A position
    /// between two items, or after the last, belongs to the item before it.
    /// </summary>
    public static int TypeAt(string statement, IReadOnlyList<string> types, int position)
    {
        var start = Characters(Opening(statement));
        var found = 0;
        for (var index = 0; index < types.Count; index++)
        {
            if (position > start)
            {
                found = index;
            }

            start += Characters(types[index]) + Separator.Length;
        }

        return found;
    }

    private static string Head(string statement, IReadOnlyList<string> types) =>
        types.Count == 0 ? $"PREPARE {statement} AS " : $"{Opening(statement)}{string.Join(Separator, types)}\n) AS ";

    private static string Opening(string statement) => $"PREPARE {statement} (";

    private static int Characters(string text) => text.EnumerateRunes().Count();

    private static string OrdinalOf(ReadOnlySpan<char> name, EquatableArray<SqlQueryParameter> parameters)
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (name.Equals(parameters[index].Name, StringComparison.OrdinalIgnoreCase))
            {
                return (index + 1).ToString(CultureInfo.InvariantCulture);
            }
        }

        throw new InvalidOperationException($"The SQL holds the parameter '{name}', which the query's list does not.");
    }

    // A hint is a comment to the server.
    private static bool OnlyCommentsFollow(string sql, EquatableArray<SqlLexeme> lexemes, int from)
    {
        for (var index = from; index < lexemes.Count; index++)
        {
            var lexeme = lexemes[index];
            var isComment =
                lexeme.Kind is SqlLexemeKind.LineComment or SqlLexemeKind.BlockComment or SqlLexemeKind.Hint;
            var isSpace =
                lexeme.Kind == SqlLexemeKind.Text && sql.AsSpan(lexeme.Span.Start, lexeme.Span.Length).IsWhiteSpace();
            if (!isComment && !isSpace)
            {
                return false;
            }
        }

        return true;
    }
}
