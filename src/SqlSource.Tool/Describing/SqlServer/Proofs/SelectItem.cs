using System;
using System.Collections.Generic;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>One item of a select list.</summary>
/// <param name="Expression">Its expression, without its alias.</param>
/// <param name="Name">
/// The name the server gives its column, without quotes, or null where the item's text does not show one.
/// </param>
/// <param name="IsStar">True for <c>*</c> and <c>q.*</c>.</param>
internal sealed record SelectItem(TokenRange Expression, string? Name, bool IsStar)
{
    // The words after which a name is the operand of the word and no alias: "a COLLATE x", "a AT TIME ZONE 'UTC'",
    // "COUNT(*) OVER w", "NEXT VALUE FOR s".
    private static readonly HashSet<string> OperandWords =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "COLLATE",
        "ZONE",
        "OVER",
        "FOR",
    ];

    /// <summary>The item of a range of tokens that holds one, which is not empty.</summary>
    /// <remarks>
    /// Its name is the first of: <c>name = expr</c>, the name a word, a quoted name or a string; <c>expr AS name</c>;
    /// <c>expr name</c>, when the token before the name ends an expression; and, for a column alone, <c>a</c>,
    /// <c>a.b</c> or <c>[s].[t].[c]</c>, its last part.  Anything else has none, since a misread name must never be
    /// taken for the server's.
    /// </remarks>
    public static SelectItem Read(IReadOnlyList<TsqlToken> tokens, TokenRange range)
    {
        var first = tokens[range.Start];
        var last = tokens[range.End - 1];
        if (last.IsSymbol("*") && IsDottedName(tokens, new TokenRange(range.Start, range.End - 1), beforeStar: true))
        {
            return new SelectItem(range, null, IsStar: true);
        }

        if (range.Count >= 3 && IsAlias(first) && tokens[range.Start + 1].IsSymbol("="))
        {
            return new SelectItem(new TokenRange(range.Start + 2, range.End), first.Name, IsStar: false);
        }

        if (range.Count >= 3 && IsAlias(last) && tokens[range.End - 2].Is("AS"))
        {
            return new SelectItem(new TokenRange(range.Start, range.End - 2), last.Name, IsStar: false);
        }

        if (range.Count >= 2 && IsAlias(last) && !last.Is("END") && EndsAnExpression(tokens[range.End - 2]))
        {
            return new SelectItem(new TokenRange(range.Start, range.End - 1), last.Name, IsStar: false);
        }

        var name = IsDottedName(tokens, range, beforeStar: false) ? last.Name : null;
        return new SelectItem(range, name, IsStar: false);
    }

    /// <summary>
    /// True when its expression is a column alone, <c>a</c>, <c>q.a</c> or <c>[s].[t].[a]</c>, with no parentheses
    /// around it.
    /// </summary>
    /// <param name="tokens">The tokens it was read from.</param>
    public bool IsColumn(IReadOnlyList<TsqlToken> tokens) =>
        !IsStar && IsDottedName(tokens, Expression, beforeStar: false);

    private static bool IsAlias(TsqlToken token) =>
        token.Kind is TsqlTokenKind.Word or TsqlTokenKind.QuotedName or TsqlTokenKind.String;

    private static bool EndsAnExpression(TsqlToken token) =>
        token.Kind switch
        {
            TsqlTokenKind.Word => !OperandWords.Contains(token.Text),
            TsqlTokenKind.Number or TsqlTokenKind.String or TsqlTokenKind.QuotedName or TsqlTokenKind.Parameter => true,
            TsqlTokenKind.Symbol => token.Text == ")",
            _ => false,
        };

    // Parts that are names, a word or a quoted name, each followed by "." but the last.  A word that starts with "@",
    // as @@ROWCOUNT, and NULL are no column's.  Before a star, the range may be empty, and every part is followed by
    // ".", as in the "q." of "q.*".
    private static bool IsDottedName(IReadOnlyList<TsqlToken> tokens, TokenRange range, bool beforeStar)
    {
        if (range.IsEmpty)
        {
            return beforeStar;
        }

        for (var index = range.Start; index < range.End; index++)
        {
            var isPart = (index - range.Start) % 2 == 0;
            var token = tokens[index];
            if (isPart ? !token.IsName || token.Is("NULL") : !token.IsSymbol("."))
            {
                return false;
            }
        }

        var endsWithDot = range.Count % 2 == 0;
        return endsWithDot == beforeStar;
    }
}
