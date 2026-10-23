using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;

namespace SqlSource.Parsing.Tokens;

/// <summary>
/// Splits a block's SQL into literal text and <c>{{name}}</c> and <c>{{name:default}}</c> tokens.
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

        // Created at the first token: most blocks have none.
        ImmutableArray<SqlTokenOccurrence>.Builder? occurrences = null;
        var literalStart = 0;
        var index = 0;

        // From this offset on there is no "}}": a search for one found none.  A later opener with a default need not
        // search again, so a block of many unclosed openers costs one search and not one for each.
        var noCloseFrom = int.MaxValue;
        while (index < sql.Length)
        {
            if (!TryReadToken(sql, index, ref noCloseFrom, out var name, out var defaultSpan, out var end))
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
                var span = TextSpan.FromBounds(index, end);
                AddLiteral(segments, sql, literalStart, index);
                segments.Add(new SqlSegment(SqlSegmentKind.Token, name));
                occurrences ??= ImmutableArray.CreateBuilder<SqlTokenOccurrence>();
                occurrences.Add(
                    new SqlTokenOccurrence(
                        name,
                        defaultSpan is { } place ? sql.Substring(place.Start, place.Length) : null,
                        span
                    )
                );
                index = end;
                literalStart = end;
            }
        }

        AddLiteral(segments, sql, literalStart, sql.Length);
        return new TokenScanResult(
            new EquatableArray<SqlSegment>(segments.ToImmutable()),
            occurrences is null
                ? EquatableArray<SqlTokenOccurrence>.Empty
                : new EquatableArray<SqlTokenOccurrence>(occurrences.ToImmutable()),
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

    /// <summary>
    /// Reads the token that starts at <paramref name="start" />: <c>{{</c>, blanks, an identifier, blanks, then
    /// <c>}}</c>, or a colon and a default that runs to the first <c>}}</c>.  Anything else that starts with
    /// <c>{{</c> is literal text, and the result is false.
    /// </summary>
    /// <param name="sql">The SQL.</param>
    /// <param name="start">Where to look for the opening braces.</param>
    /// <param name="name">The token's name, when the result is true.</param>
    /// <param name="defaultSpan">
    /// Where the default is in <paramref name="sql" />, without the white space around it; empty for
    /// <c>{{name:}}</c>, and null for a token with no colon.
    /// </param>
    /// <param name="end">The offset just after the closing braces, when the result is true.</param>
    /// <returns>True when a token starts at <paramref name="start" />.</returns>
    public static bool TryReadToken(string sql, int start, out string name, out TextSpan? defaultSpan, out int end)
    {
        var noCloseFrom = int.MaxValue;
        return TryReadToken(sql, start, ref noCloseFrom, out name, out defaultSpan, out end);
    }

    // noCloseFrom is what the caller has learned from earlier calls on the same SQL: no "}}" starts at or after that
    // offset.  A search that finds none lowers it.
    private static bool TryReadToken(
        string sql,
        int start,
        ref int noCloseFrom,
        out string name,
        out TextSpan? defaultSpan,
        out int end
    )
    {
        name = string.Empty;
        defaultSpan = null;
        end = 0;
        if (!TryReadOpener(sql, start, out var nameSpan, out var after))
        {
            return false;
        }

        var hasDefault = SqlText.CharAt(sql, after) == ':';
        if (!hasDefault && (SqlText.CharAt(sql, after) != '}' || SqlText.CharAt(sql, after + 1) != '}'))
        {
            return false;
        }

        // The name is checked before the search for the closing braces, so that text such as "{{1a:" costs none.
        name = sql.Substring(nameSpan.Start, nameSpan.Length);
        if (!SqlIdentifier.IsValid(name))
        {
            return false;
        }

        var close = after;
        if (hasDefault)
        {
            close = after + 1 >= noCloseFrom ? -1 : sql.IndexOf("}}", after + 1, StringComparison.Ordinal);
            if (close < 0)
            {
                noCloseFrom = Math.Min(noCloseFrom, after + 1);
                return false;
            }
        }

        if (close > after)
        {
            defaultSpan = SqlText.Trim(sql, after + 1, close);
        }

        end = close + 2;
        return true;
    }

    /// <summary>
    /// Reads what every token starts with: <c>{{</c>, blanks, the characters a name can hold, and blanks.
    /// </summary>
    /// <param name="sql">The SQL.</param>
    /// <param name="start">Where to look for the opening braces.</param>
    /// <param name="nameSpan">Where the name is, which may be empty or not a valid name.</param>
    /// <param name="after">Where the token goes on, with a colon or <c>}}</c>.</param>
    /// <returns>True when <paramref name="start" /> holds <c>{{</c>.</returns>
    internal static bool TryReadOpener(string sql, int start, out TextSpan nameSpan, out int after)
    {
        nameSpan = default;
        after = 0;
        if (SqlText.CharAt(sql, start) != '{' || SqlText.CharAt(sql, start + 1) != '{')
        {
            return false;
        }

        var nameStart = SkipBlanks(sql, start + 2);
        var nameEnd = nameStart;
        while (SyntaxFacts.IsIdentifierPartCharacter(SqlText.CharAt(sql, nameEnd)))
        {
            nameEnd++;
        }

        nameSpan = TextSpan.FromBounds(nameStart, nameEnd);
        after = SkipBlanks(sql, nameEnd);
        return true;
    }

    private static int SkipBlanks(string sql, int index)
    {
        while (SqlText.CharAt(sql, index) is ' ' or '\t')
        {
            index++;
        }

        return index;
    }
}
