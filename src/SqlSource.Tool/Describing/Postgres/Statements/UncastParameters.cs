using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres.Statements;

/// <summary>
/// The parameters a type error at <c>prepare</c> may be the fault of: those of no declared type with a use that no
/// cast fixes, whose type the server took from their first use.
/// </summary>
/// <remarks>
/// The SQL is read as pieces, words by PostgreSQL's rule (<see cref="PostgresIdentifiers.IsWordPart" />), so
/// <c>€CAST</c> is one word and no <c>CAST</c>.
/// </remarks>
internal static class UncastParameters
{
    /// <summary>
    /// The parameters of no declared type that the SQL uses at least once without a cast that fixes the type there,
    /// in the order of the query.  A SQL that does not lex, which the plan's parser read, names every one of them:
    /// the help is no place to fail, so the SQL is lexed here without throwing.
    /// </summary>
    public static List<SqlQueryParameter> Of(DescribeRequest query)
    {
        var lexed = SqlLexer.Lex(query.Sql, SqlDialectRules.PostgreSql);
        var uncast = lexed.Error is null ? Uncast(query.Sql, lexed.Lexemes) : null;
        return
        [
            .. query.Parameters.Where(parameter =>
                parameter.Type is null && (uncast is null || uncast.Contains(parameter.Name))
            ),
        ];
    }

    // The names, ignoring case as the plan matches them, of the parameters with a use that is not "@x::t" or
    // "CAST(@x AS t)": white space and comments may stand between any two of the pieces, and a string, a quoted
    // name or a comment holds no piece.  The name is the parameter's text after its one-character prefix.
    private static HashSet<string> Uncast(string sql, EquatableArray<SqlLexeme> lexemes)
    {
        var pieces = Pieces(sql, lexemes);
        var uncast = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < pieces.Count; index++)
        {
            if (pieces[index] is not { Parameter: true } parameter)
            {
                continue;
            }

            var cast =
                PieceAt(pieces, index + 1) == "::"
                || (
                    IsWord(PieceAt(pieces, index - 2), "CAST")
                    && PieceAt(pieces, index - 1) == "("
                    && IsWord(PieceAt(pieces, index + 1), "AS")
                );
            if (!cast)
            {
                _ = uncast.Add(parameter.Text[1..]);
            }
        }

        return uncast;
    }

    // The SQL as a list of pieces, without white space or comments: a word, a "::", any other character, a string or
    // a quoted name whole, and a parameter.
    private static List<(string Text, bool Parameter)> Pieces(string sql, EquatableArray<SqlLexeme> lexemes)
    {
        var pieces = new List<(string Text, bool Parameter)>();
        foreach (var lexeme in lexemes)
        {
            var text = sql.Substring(lexeme.Span.Start, lexeme.Span.Length);
            if (lexeme.Kind == SqlLexemeKind.Text)
            {
                TextPieces(text, pieces);
            }
            else if (lexeme.Kind is SqlLexemeKind.Parameter or SqlLexemeKind.Quoted)
            {
                pieces.Add((text, lexeme.Kind == SqlLexemeKind.Parameter));
            }
        }

        return pieces;
    }

    private static void TextPieces(string text, List<(string Text, bool Parameter)> pieces)
    {
        var index = 0;
        while (index < text.Length)
        {
            var start = index;
            if (char.IsWhiteSpace(text[index]))
            {
                index++;
                continue;
            }

            if (PostgresIdentifiers.IsWordPart(text[index]))
            {
                while (index < text.Length && PostgresIdentifiers.IsWordPart(text[index]))
                {
                    index++;
                }
            }
            else
            {
                index += text[index] == ':' && index + 1 < text.Length && text[index + 1] == ':' ? 2 : 1;
            }

            pieces.Add((text[start..index], false));
        }
    }

    private static string? PieceAt(List<(string Text, bool Parameter)> pieces, int index) =>
        index >= 0 && index < pieces.Count && !pieces[index].Parameter ? pieces[index].Text : null;

    private static bool IsWord(string? piece, string word) =>
        string.Equals(piece, word, StringComparison.OrdinalIgnoreCase);
}
