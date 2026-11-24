using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>One CTE of a <c>WITH</c> clause.</summary>
/// <param name="Name">Its name, without quotes.</param>
/// <param name="Columns">The names of its column list, or null when it has none.</param>
/// <param name="Body">Its query when that is one <c>SELECT</c> the reader follows, or null.</param>
internal sealed record CommonTableExpression(string Name, IReadOnlyList<string>? Columns, SelectBody? Body);

/// <summary>
/// A query of one <c>SELECT</c>, or of several joined by set operators, with the <c>WITH</c> clause before it.
/// </summary>
/// <param name="With">
/// The <c>WITH</c> clause, from the word <c>WITH</c> to the <c>)</c> of its last CTE, or an empty range before the
/// <c>SELECT</c> when there is none.
/// </param>
/// <param name="Branches">Each <c>SELECT</c>, in order: one, or one for each side of its set operators.</param>
/// <param name="Ctes">The CTEs of the <c>WITH</c> clause, in order.</param>
internal sealed record SelectStatement(
    TokenRange With,
    IReadOnlyList<SelectBody> Branches,
    IReadOnlyList<CommonTableExpression> Ctes
)
{
    /// <summary>The first <c>SELECT</c>, whose items name the result's columns.</summary>
    public SelectBody Body => Branches[0];

    /// <summary>
    /// The query, or null when its text is not an optional <c>;</c>, an optional <c>WITH</c> clause and one
    /// <c>SELECT</c> or several joined by <c>UNION [ALL]</c>, <c>EXCEPT</c> or <c>INTERSECT</c>, each of which
    /// <see cref="SelectBody.Read" /> follows, ending the text or followed by <c>;</c> alone.
    /// </summary>
    /// <remarks>A misread must prove nothing, so whatever the reader cannot follow gives null.</remarks>
    public static SelectStatement? Read(IReadOnlyList<TsqlToken> tokens)
    {
        var at = tokens.Count > 0 && tokens[0].IsSymbol(";") ? 1 : 0;
        var end = tokens.Count;
        if (end > at && tokens[end - 1].IsSymbol(";"))
        {
            end--;
        }

        var withStart = at;
        var ctes = new List<CommonTableExpression>();
        if (at < end && tokens[at].Is("WITH"))
        {
            at = CtesEnd(tokens, at + 1, end, ctes);
            if (at < 0)
            {
                return null;
            }
        }

        var names = ctes.Select(static cte => cte.Name).ToList();
        var branches = ReadBranches(tokens, new TokenRange(at, end), names);
        return branches is null ? null : new SelectStatement(new TokenRange(withStart, at), branches, ctes);
    }

    // The SELECTs of a range, split at the set operators of its own level, or null when one cannot be read.  A side in
    // parentheses is read inside them, and anything after its ")" gives null.  Every branch must prove a column for it
    // to be proved, so how the operators nest does not matter.
    private static List<SelectBody>? ReadBranches(
        IReadOnlyList<TsqlToken> tokens,
        TokenRange range,
        IReadOnlyCollection<string> names
    )
    {
        var sides = Sides(tokens, range);
        if (sides.Count == 1)
        {
            return SelectBody.Read(tokens, range, names) is { } body ? [body] : null;
        }

        var branches = new List<SelectBody>();
        foreach (var side in sides)
        {
            if (ReadSide(tokens, side, names) is not { } read)
            {
                return null;
            }

            branches.AddRange(read);
        }

        return branches;
    }

    private static List<SelectBody>? ReadSide(
        IReadOnlyList<TsqlToken> tokens,
        TokenRange side,
        IReadOnlyCollection<string> names
    )
    {
        if (side.IsEmpty)
        {
            return null;
        }

        if (!tokens[side.Start].IsSymbol("("))
        {
            return SelectBody.Read(tokens, side, names) is { } body ? [body] : null;
        }

        return SelectBody.ClosingParenthesis(tokens, side.Start, side.End) == side.End - 1
            ? ReadBranches(tokens, new TokenRange(side.Start + 1, side.End - 1), names)
            : null;
    }

    // The range cut at each UNION [ALL], EXCEPT and INTERSECT of its own level.
    private static List<TokenRange> Sides(IReadOnlyList<TsqlToken> tokens, TokenRange range)
    {
        var sides = new List<TokenRange>();
        var start = range.Start;
        foreach (var index in SelectBody.AtLevel(tokens, range))
        {
            if (!tokens[index].Is("UNION") && !tokens[index].Is("EXCEPT") && !tokens[index].Is("INTERSECT"))
            {
                continue;
            }

            sides.Add(new TokenRange(start, index));
            start = index + 1;
            if (tokens[index].Is("UNION") && start < range.End && tokens[start].Is("ALL"))
            {
                start++;
            }
        }

        sides.Add(new TokenRange(start, range.End));
        return sides;
    }

    // The end of the CTEs after WITH, "name [(columns)] AS (query)" separated by commas, or -1.
    private static int CtesEnd(IReadOnlyList<TsqlToken> tokens, int at, int end, List<CommonTableExpression> ctes)
    {
        while (true)
        {
            // WITH XMLNAMESPACES declares namespaces, and no CTE.
            if (at >= end || !tokens[at].IsName || tokens[at].Is("XMLNAMESPACES"))
            {
                return -1;
            }

            var name = tokens[at].Name!;
            List<string>? columns = null;
            at++;
            if (at < end && tokens[at].IsSymbol("("))
            {
                columns = [];
                at = ColumnsEnd(tokens, at + 1, end, columns);
                if (at < 0)
                {
                    return -1;
                }
            }

            if (at + 1 >= end || !tokens[at].Is("AS") || !tokens[at + 1].IsSymbol("("))
            {
                return -1;
            }

            var close = SelectBody.ClosingParenthesis(tokens, at + 1, end);
            if (close < 0)
            {
                return -1;
            }

            // A CTE of several branches, a recursive one among them, is left unread, so that it proves nothing.
            string[] visible = [.. ctes.Select(static cte => cte.Name), name];
            ctes.Add(
                new CommonTableExpression(
                    name,
                    columns,
                    SelectBody.Read(tokens, new TokenRange(at + 2, close), visible)
                )
            );

            at = close + 1;
            if (at < end && tokens[at].IsSymbol(","))
            {
                at++;
                continue;
            }

            return at;
        }
    }

    // The end of a CTE's list of columns, "a, b)", or -1.
    private static int ColumnsEnd(IReadOnlyList<TsqlToken> tokens, int at, int end, List<string> columns)
    {
        while (at + 1 < end && tokens[at].IsName)
        {
            columns.Add(tokens[at].Name!);
            if (tokens[at + 1].IsSymbol(")"))
            {
                return at + 2;
            }

            if (!tokens[at + 1].IsSymbol(","))
            {
                return -1;
            }

            at += 2;
        }

        return -1;
    }
}
