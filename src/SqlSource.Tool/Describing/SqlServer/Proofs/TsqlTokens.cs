using System.Collections.Generic;
using SqlSource.Parsing;
using SqlSource.Parsing.Quoting;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>A T-SQL text as the tokens the proofs read it by.</summary>
/// <remarks>
/// The lexer of <c>mssql</c> gives the quoted regions, the parameters and the comments; this splits the text between
/// them into words, numbers and symbols.  A comment and a hint are dropped, and whitespace only separates.  It is no
/// parser: what it cannot place is a symbol of one character, which no form of the proofs reads.
/// </remarks>
internal static class TsqlTokens
{
    // The operators of two characters; every other symbol is one.
    private static readonly string[] Pairs = ["<=", ">=", "<>", "!=", "!<", "!>"];

    /// <summary>The tokens of the SQL, in order.</summary>
    /// <exception cref="System.InvalidOperationException">The SQL does not lex: a bug of the plan.</exception>
    public static IReadOnlyList<TsqlToken> Read(string sql)
    {
        var tokens = new List<TsqlToken>();
        foreach (var lexeme in DescriberSql.Lex(sql, SqlDialectRules.SqlServer))
        {
            var start = lexeme.Span.Start;
            var end = lexeme.Span.End;
            if (lexeme.Kind == SqlLexemeKind.Text)
            {
                ReadText(sql, start, end, tokens);
            }
            else if (lexeme.Kind == SqlLexemeKind.Parameter)
            {
                tokens.Add(new TsqlToken(TsqlTokenKind.Parameter, sql[start..end], start, end));
            }
            else if (lexeme.Kind == SqlLexemeKind.Quoted)
            {
                AddQuoted(sql, start, end, tokens);
            }

            // A comment or a hint is dropped.
        }

        return tokens;
    }

    // A region that opens with ' is a string, and an N directly before the quote makes it a Unicode one and is part
    // of it.  One that opens with [ or " is a name.
    private static void AddQuoted(string sql, int start, int end, List<TsqlToken> tokens)
    {
        if (sql[start] != '\'')
        {
            tokens.Add(new TsqlToken(TsqlTokenKind.QuotedName, sql[start..end], start, end));
            return;
        }

        if (
            tokens.Count > 0
            && tokens[^1] is { Kind: TsqlTokenKind.Word, Text: "N" or "n" } prefix
            && prefix.End == start
        )
        {
            tokens.RemoveAt(tokens.Count - 1);
            start = prefix.Start;
        }

        tokens.Add(new TsqlToken(TsqlTokenKind.String, sql[start..end], start, end));
    }

    private static void ReadText(string sql, int at, int end, List<TsqlToken> tokens)
    {
        // The lexer ends a parameter at the first character that is not a letter, a digit or "_", but a variable's
        // name goes on through "#", "$" and "@", as every name of SQL Server does: "@a#b" is one variable.
        if (tokens.Count > 0 && tokens[^1] is { Kind: TsqlTokenKind.Parameter } parameter && parameter.End == at)
        {
            while (at < end && QuoteReader.IsNameCharacterOfSqlServer(sql[at]))
            {
                at++;
            }

            tokens[^1] = parameter with { Text = sql[parameter.Start..at], End = at };
        }

        while (at < end)
        {
            var character = sql[at];
            if (char.IsWhiteSpace(character))
            {
                at++;
                continue;
            }

            var start = at;
            TsqlTokenKind kind;

            if (char.IsLetter(character) || character is '_' or '#' or '@')
            {
                kind = TsqlTokenKind.Word;
                at++;
                while (at < end && QuoteReader.IsNameCharacterOfSqlServer(sql[at]))
                {
                    at++;
                }
            }
            else if (char.IsAsciiDigit(character))
            {
                kind = TsqlTokenKind.Number;
                at = NumberEnd(sql, at, end);
            }
            else
            {
                kind = TsqlTokenKind.Symbol;
                at += at + 1 < end && IsPair(sql, at) ? 2 : 1;
            }

            tokens.Add(new TsqlToken(kind, sql[start..at], start, at));
        }
    }

    // A number is 0x and hex digits, or digits with a "." and an exponent with its sign.  An "e" after the digits is
    // the exponent's, as the server reads it, and its sign only when a digit follows.
    private static int NumberEnd(string sql, int at, int end)
    {
        if (sql[at] == '0' && at + 1 < end && sql[at + 1] is 'x' or 'X')
        {
            at += 2;
            while (at < end && char.IsAsciiHexDigit(sql[at]))
            {
                at++;
            }

            return at;
        }

        at = DigitsEnd(sql, at, end);
        if (at < end && sql[at] == '.')
        {
            at = DigitsEnd(sql, at + 1, end);
        }

        if (at < end && sql[at] is 'e' or 'E')
        {
            at++;
            if (at + 1 < end && sql[at] is '+' or '-' && char.IsAsciiDigit(sql[at + 1]))
            {
                at++;
            }

            at = DigitsEnd(sql, at, end);
        }

        return at;
    }

    private static int DigitsEnd(string sql, int at, int end)
    {
        while (at < end && char.IsAsciiDigit(sql[at]))
        {
            at++;
        }

        return at;
    }

    private static bool IsPair(string sql, int at)
    {
        foreach (var pair in Pairs)
        {
            if (sql[at] == pair[0] && sql[at + 1] == pair[1])
            {
                return true;
            }
        }

        return false;
    }
}
