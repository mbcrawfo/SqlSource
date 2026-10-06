using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing;

/// <summary>
/// Splits a block's SQL into literal text and <c>{{name}}</c> tokens.
/// </summary>
internal static class TokenScanner
{
    /// <summary>
    /// Scans <paramref name="sql" />.  A token whose name is in <paramref name="ignoredNames" /> stays literal text.
    /// </summary>
    public static TokenScanResult Scan(string sql, ISet<string> ignoredNames)
    {
        var segments = ImmutableArray.CreateBuilder<SqlSegment>();
        var errors = ImmutableArray.CreateBuilder<SqlParseError>();
        var literalStart = 0;
        var index = 0;
        while (index < sql.Length)
        {
            if (!TryReadToken(sql, index, out var name, out var end))
            {
                index++;
            }
            else if (ignoredNames.Contains(name))
            {
                index = end;
            }
            else if (SqlIdentifier.IsReservedKeyword(name))
            {
                var span = TextSpan.FromBounds(index, end);
                errors.Add(SqlParseError.Create(SqlParseErrorKind.ReservedTokenName, span, name));
                index = end;
            }
            else
            {
                AddLiteral(segments, sql, literalStart, index);
                segments.Add(new SqlSegment(SqlSegmentKind.Token, name));
                index = end;
                literalStart = end;
            }
        }

        AddLiteral(segments, sql, literalStart, sql.Length);
        return new TokenScanResult(
            new EquatableArray<SqlSegment>(segments.ToImmutable()),
            new EquatableArray<SqlParseError>(errors.ToImmutable())
        );
    }

    private static void AddLiteral(ImmutableArray<SqlSegment>.Builder segments, string sql, int start, int end)
    {
        if (end > start)
        {
            segments.Add(new SqlSegment(SqlSegmentKind.Literal, sql.Substring(start, end - start)));
        }
    }

    // A token is "{{", blanks, an identifier, blanks, "}}".  Anything else that starts with "{{" is literal text.
    private static bool TryReadToken(string sql, int start, out string name, out int end)
    {
        name = string.Empty;
        end = 0;
        if (CharAt(sql, start) != '{' || CharAt(sql, start + 1) != '{')
        {
            return false;
        }

        var nameStart = SkipBlanks(sql, start + 2);
        var nameEnd = nameStart;
        while (SyntaxFacts.IsIdentifierPartCharacter(CharAt(sql, nameEnd)))
        {
            nameEnd++;
        }

        var close = SkipBlanks(sql, nameEnd);
        if (CharAt(sql, close) != '}' || CharAt(sql, close + 1) != '}')
        {
            return false;
        }

        name = sql.Substring(nameStart, nameEnd - nameStart);
        end = close + 2;
        return SqlIdentifier.IsValid(name);
    }

    private static int SkipBlanks(string sql, int index)
    {
        while (CharAt(sql, index) is ' ' or '\t')
        {
            index++;
        }

        return index;
    }

    private static char CharAt(string sql, int index) => index < sql.Length ? sql[index] : '\0';
}
