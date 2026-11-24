using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>One source of a <see cref="JoinChain" />.</summary>
/// <param name="Name">The name its columns are qualified by: its alias, or the last part of its name.</param>
/// <param name="OnePartName">Its name when it is a name of one part, which may be a CTE's; otherwise null.</param>
/// <param name="Outer">
/// The indexes of the <c>LEFT</c>, and the <c>OUTER</c> after it, of the join that adds it; otherwise empty.
/// </param>
/// <param name="IsOptional">
/// True when its own join is <c>LEFT</c> or <c>FULL</c>, or a later join is <c>RIGHT</c> or <c>FULL</c>.
/// </param>
/// <param name="IsOptionalBesidesItsLeftJoin">
/// True when a join other than its own <c>LEFT JOIN</c> can make it <c>NULL</c>.
/// </param>
internal sealed record JoinSource(
    string Name,
    string? OnePartName,
    IReadOnlyList<int> Outer,
    bool IsOptional,
    bool IsOptionalBesidesItsLeftJoin
);

/// <summary>A <c>FROM</c> that is a chain of joins read left to right, each source a name or a derived table.</summary>
/// <remarks>
/// Anything else gives null, which proves nothing: a comma, an <c>APPLY</c>, a <c>PIVOT</c>, a join hint, a table
/// function, <c>FOR SYSTEM_TIME</c>, and a nested join, <c>a LEFT JOIN b JOIN c ON p ON q</c>, whose <c>LEFT</c> covers
/// both.  The words <c>LEFT</c> and <c>RIGHT</c> before <c>(</c> are the functions, and part of an <c>ON</c>.
/// </remarks>
internal static class JoinChain
{
    private enum JoinKind
    {
        Inner,
        Left,
        Right,
        Full,
        Cross,
    }

    /// <summary>The sources of a <c>FROM</c> clause, from the word <c>FROM</c> to its end, or null.</summary>
    public static IReadOnlyList<JoinSource>? Read(IReadOnlyList<TsqlToken> tokens, TokenRange from)
    {
        var at = from.Start + 1;
        if (Reference(tokens, ref at, from.End) is not { } first)
        {
            return null;
        }

        var read = new List<(string Name, string? OnePart, JoinKind Kind, int[] Outer)>
        {
            (first.Name, first.OnePart, JoinKind.Inner, []),
        };
        while (at < from.End)
        {
            if (KindAt(tokens, at, from.End) is not { } join)
            {
                return null;
            }

            var outer = join.Kind == JoinKind.Left ? Enumerable.Range(at, join.Length - 1).ToArray() : [];
            at += join.Length;
            if (Reference(tokens, ref at, from.End) is not { } source)
            {
                return null;
            }

            if (join.Kind != JoinKind.Cross && !SkipOn(tokens, ref at, from.End))
            {
                return null;
            }

            read.Add((source.Name, source.OnePart, join.Kind, outer));
        }

        return
        [
            .. read.Select(
                (source, index) =>
                {
                    var byLater = read.Skip(index + 1)
                        .Any(static later => later.Kind is JoinKind.Right or JoinKind.Full);
                    return new JoinSource(
                        source.Name,
                        source.OnePart,
                        source.Outer,
                        byLater || source.Kind is JoinKind.Left or JoinKind.Full,
                        byLater || source.Kind == JoinKind.Full
                    );
                }
            ),
        ];
    }

    // The join that starts at the token, with how many tokens its words take, or null.
    private static (JoinKind Kind, int Length)? KindAt(IReadOnlyList<TsqlToken> tokens, int at, int end)
    {
        bool WordAt(int index, string word) => index < end && tokens[index].Is(word);

        if (WordAt(at, "JOIN"))
        {
            return (JoinKind.Inner, 1);
        }

        if (WordAt(at, "INNER") && WordAt(at + 1, "JOIN"))
        {
            return (JoinKind.Inner, 2);
        }

        if (WordAt(at, "CROSS") && WordAt(at + 1, "JOIN"))
        {
            return (JoinKind.Cross, 2);
        }

        if (at >= end || OuterKind(tokens[at]) is not { } kind)
        {
            return null;
        }

        if (WordAt(at + 1, "JOIN"))
        {
            return (kind, 2);
        }

        return WordAt(at + 1, "OUTER") && WordAt(at + 2, "JOIN") ? (kind, 3) : null;
    }

    private static JoinKind? OuterKind(TsqlToken token)
    {
        if (token.Is("LEFT"))
        {
            return JoinKind.Left;
        }

        if (token.Is("RIGHT"))
        {
            return JoinKind.Right;
        }

        return token.Is("FULL") ? JoinKind.Full : null;
    }

    // "name [[AS] alias] [WITH (hints)]" or "( ... ) [AS] alias [(columns)]".
    private static (string Name, string? OnePart)? Reference(IReadOnlyList<TsqlToken> tokens, ref int at, int end)
    {
        if (at < end && tokens[at].IsSymbol("("))
        {
            var close = SelectBody.ClosingParenthesis(tokens, at, end);
            if (close < 0)
            {
                return null;
            }

            at = close + 1;
            if (Alias(tokens, ref at, end) is not { } derived)
            {
                return null;
            }

            if (at < end && tokens[at].IsSymbol("("))
            {
                var columns = SelectBody.ClosingParenthesis(tokens, at, end);
                if (columns < 0)
                {
                    return null;
                }

                at = columns + 1;
            }

            return (derived, null);
        }

        var parts = new List<string>();
        while (at < end && tokens[at].IsName)
        {
            parts.Add(tokens[at].Name!);
            at++;
            if (at >= end || !tokens[at].IsSymbol("."))
            {
                break;
            }

            at++;
        }

        if (parts.Count is 0 or > 4 || tokens[at - 1].IsSymbol("."))
        {
            return null;
        }

        var name = Alias(tokens, ref at, end) ?? parts[^1];
        if (at + 1 < end && tokens[at].Is("WITH") && tokens[at + 1].IsSymbol("("))
        {
            var hints = SelectBody.ClosingParenthesis(tokens, at + 1, end);
            if (hints < 0)
            {
                return null;
            }

            at = hints + 1;
        }

        return (name, parts.Count == 1 ? parts[0] : null);
    }

    private static string? Alias(IReadOnlyList<TsqlToken> tokens, ref int at, int end)
    {
        if (at < end && tokens[at].Is("AS"))
        {
            at++;
            if (at >= end || !tokens[at].IsName)
            {
                return null;
            }

            return tokens[at++].Name;
        }

        return at < end && tokens[at].IsName && !IsWordAfterASource(tokens[at]) ? tokens[at++].Name : null;
    }

    private static bool IsWordAfterASource(TsqlToken token) =>
        token.Kind == TsqlTokenKind.Word
        && token.Text.ToUpperInvariant()
            is "ON"
                or "JOIN"
                or "INNER"
                or "LEFT"
                or "RIGHT"
                or "FULL"
                or "CROSS"
                or "OUTER"
                or "WITH"
                or "PIVOT"
                or "UNPIVOT"
                or "TABLESAMPLE"
                or "FOR";

    // Past "ON condition", to the next join or the end.  A word of a join that starts no join this reads, as a hint's,
    // and an APPLY, a PIVOT, a comma or a second ON at the condition's level, each give false.
    private static bool SkipOn(IReadOnlyList<TsqlToken> tokens, ref int at, int end)
    {
        if (at >= end || !tokens[at].Is("ON"))
        {
            return false;
        }

        var start = at + 1;
        foreach (var index in SelectBody.AtLevel(tokens, new TokenRange(start, end)))
        {
            var token = tokens[index];
            if (KindAt(tokens, index, end) is not null)
            {
                at = index;
                return index > start;
            }

            var isCall = index + 1 < end && tokens[index + 1].IsSymbol("(");
            if (
                token.IsSymbol(",")
                || token.Is("ON")
                || token.Is("JOIN")
                || token.Is("APPLY")
                || token.Is("PIVOT")
                || token.Is("UNPIVOT")
                || token.Is("INNER")
                || token.Is("CROSS")
                || token.Is("FULL")
                || token.Is("OUTER")
                || ((token.Is("LEFT") || token.Is("RIGHT")) && !isCall)
            )
            {
                return false;
            }
        }

        at = end;
        return end > start;
    }
}
