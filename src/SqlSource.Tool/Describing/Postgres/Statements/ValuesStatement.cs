using System;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres.Statements;

/// <summary>
/// The columns of a statement that is a <c>VALUES</c> alone whose every row holds a literal: the plan's
/// <c>Values Scan</c> prints <c>column1</c> and nothing of what its rows hold, so they are read from the SQL.
/// </summary>
/// <remarks>
/// A cell counts only as one number, with a sign or without, one string in single quotes, <c>TRUE</c> or <c>FALSE</c>.
/// <c>NULL</c>, a parameter, a cast, an expression, a call and a string with a prefix do not, since each may be
/// <c>NULL</c> or is not read.  What may follow the rows is <c>ORDER BY</c>, <c>LIMIT</c>, <c>OFFSET</c> or
/// <c>FETCH</c>, which add no row, or the end of the statement.  The walk uses the answer only for a plan that is a
/// <c>Values Scan</c> under nodes that never project.
/// </remarks>
internal static class ValuesStatement
{
    private static readonly string[] Tails = ["ORDER", "LIMIT", "OFFSET", "FETCH"];

    private enum TokenKind
    {
        Word,
        String,
        Open,
        Close,
        Comma,
        Semicolon,
        Sign,
        Other,
    }

    private readonly record struct Token(TokenKind Kind, string Text);

    /// <summary>
    /// For each column of the statement, whether every row holds a literal in it; or null when the statement is no
    /// <c>VALUES</c> of that shape.
    /// </summary>
    /// <param name="sql">The SQL of the query.</param>
    public static IReadOnlyList<bool>? NeverNullColumns(string sql)
    {
        var tokens = TokensOf(sql);
        if (tokens is not [{ Kind: TokenKind.Word } first, ..] || !IsKeyword(first, "VALUES"))
        {
            return null;
        }

        var at = 1;
        bool[]? columns = null;
        while (true)
        {
            if (RowAt(tokens, ref at) is not { } row || (columns is not null && columns.Length != row.Count))
            {
                return null;
            }

            columns = columns is null ? [.. row] : [.. columns.Zip(row, static (kept, literal) => kept && literal)];

            if (at < tokens.Count && tokens[at].Kind == TokenKind.Comma)
            {
                at++;
                continue;
            }

            return EndsTheStatement(tokens, at) ? columns : null;
        }
    }

    // The cells of the row that opens at the index, each whether it is a literal, with the index moved past its closing
    // parenthesis; or null when no row opens there.
    private static List<bool>? RowAt(List<Token> tokens, ref int at)
    {
        if (at >= tokens.Count || tokens[at].Kind != TokenKind.Open)
        {
            return null;
        }

        at++;
        var row = new List<bool>();
        var cell = new List<Token>();
        var depth = 0;
        while (at < tokens.Count)
        {
            var token = tokens[at];
            at++;
            switch (token.Kind)
            {
                case TokenKind.Open:
                    depth++;
                    break;
                case TokenKind.Close when depth == 0:
                    if (cell.Count == 0)
                    {
                        return null;
                    }

                    row.Add(IsLiteral(cell));
                    return row;
                case TokenKind.Close:
                    depth--;
                    break;
                case TokenKind.Comma when depth == 0:
                    if (cell.Count == 0)
                    {
                        return null;
                    }

                    row.Add(IsLiteral(cell));
                    cell.Clear();
                    continue;
                case TokenKind.Word:
                case TokenKind.String:
                case TokenKind.Comma:
                case TokenKind.Semicolon:
                case TokenKind.Sign:
                case TokenKind.Other:
                    break;
                default:
                    throw UnexpectedEnumValueException.Of(token.Kind);
            }

            cell.Add(token);
        }

        return null;
    }

    private static bool IsLiteral(List<Token> cell) =>
        cell switch
        {
            [{ Kind: TokenKind.String }] => true,
            [{ Kind: TokenKind.Word } word] => IsNumber(word.Text)
                || IsKeyword(word, "TRUE")
                || IsKeyword(word, "FALSE"),
            [{ Kind: TokenKind.Sign }, { Kind: TokenKind.Word } number] => IsNumber(number.Text),
            _ => false,
        };

    // Digits with an optional point, or a point and digits, and an optional exponent of digits.
    private static bool IsNumber(string text)
    {
        var at = 0;
        var digits = SkipDigits(text, ref at);
        if (at < text.Length && text[at] == '.')
        {
            at++;
            digits += SkipDigits(text, ref at);
        }

        if (digits == 0)
        {
            return false;
        }

        if (at < text.Length && text[at] is 'e' or 'E')
        {
            at++;
            if (SkipDigits(text, ref at) == 0)
            {
                return false;
            }
        }

        return at == text.Length;
    }

    private static int SkipDigits(string text, ref int at)
    {
        var start = at;
        while (at < text.Length && char.IsAsciiDigit(text[at]))
        {
            at++;
        }

        return at - start;
    }

    // Whether what is left after the rows is nothing, one semicolon, or a clause that adds no row.
    private static bool EndsTheStatement(List<Token> tokens, int at)
    {
        if (at == tokens.Count)
        {
            return true;
        }

        var next = tokens[at];
        return next.Kind == TokenKind.Semicolon
            ? at + 1 == tokens.Count
            : next.Kind == TokenKind.Word && Tails.Any(tail => IsKeyword(next, tail));
    }

    private static bool IsKeyword(Token token, string keyword) =>
        token.Kind == TokenKind.Word && string.Equals(token.Text, keyword, StringComparison.OrdinalIgnoreCase);

    // The tokens of the SQL without its comments.  A string lexeme is one token, and a prefix before it is a word of
    // its own, so that a cell of the two is no literal.  Anything else the lexer gives is one token of no use.
    private static List<Token> TokensOf(string sql)
    {
        var tokens = new List<Token>();
        foreach (var lexeme in DescriberSql.Lex(sql, SqlDialectRules.PostgreSql))
        {
            if (DescriberSql.IsCommentOrHint(lexeme.Kind))
            {
                continue;
            }

            var text = sql.Substring(lexeme.Span.Start, lexeme.Span.Length);
            if (lexeme.Kind == SqlLexemeKind.Text)
            {
                AddText(text, tokens);
            }
            else if (lexeme.Kind == SqlLexemeKind.Quoted && text.StartsWith('\''))
            {
                tokens.Add(new Token(TokenKind.String, text));
            }
            else
            {
                tokens.Add(new Token(TokenKind.Other, text));
            }
        }

        return tokens;
    }

    private static void AddText(string text, List<Token> tokens)
    {
        for (var at = 0; at < text.Length; )
        {
            var character = text[at];
            if (char.IsWhiteSpace(character))
            {
                at++;
            }
            else if (char.IsAsciiLetterOrDigit(character) || character is '_' or '.')
            {
                var start = at;
                while (at < text.Length && (char.IsAsciiLetterOrDigit(text[at]) || text[at] is '_' or '.'))
                {
                    at++;
                }

                tokens.Add(new Token(TokenKind.Word, text[start..at]));
            }
            else
            {
                tokens.Add(
                    new Token(
                        character switch
                        {
                            '(' => TokenKind.Open,
                            ')' => TokenKind.Close,
                            ',' => TokenKind.Comma,
                            ';' => TokenKind.Semicolon,
                            '+' or '-' => TokenKind.Sign,
                            _ => TokenKind.Other,
                        },
                        character.ToString()
                    )
                );
                at++;
            }
        }
    }
}
