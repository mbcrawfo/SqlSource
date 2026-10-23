using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>The <c>GROUP BY</c> of a <c>SELECT</c>.</summary>
internal enum SelectGrouping
{
    /// <summary>No <c>GROUP BY</c>.</summary>
    None,

    /// <summary>A <c>GROUP BY</c> of expressions alone.</summary>
    Plain,

    /// <summary><c>GROUP BY ALL</c>, which returns a group that no row of the <c>WHERE</c> holds.</summary>
    All,

    /// <summary>
    /// <c>ROLLUP</c>, <c>CUBE</c>, <c>GROUPING SETS</c> or <c>()</c>, whose rows of a total hold a grouping column as
    /// <c>NULL</c>.
    /// </summary>
    Sets,
}

/// <summary>One <c>SELECT</c> without its <c>WITH</c>: its items and what the proofs need of its clauses.</summary>
/// <param name="Items">The select list's items, in order.</param>
/// <param name="FromThroughWhere">
/// From its <c>FROM</c> to the end of its <c>WHERE</c>, or of its <c>FROM</c> when it has none; null without a
/// <c>FROM</c>.
/// </param>
/// <param name="Grouping">Its <c>GROUP BY</c>.</param>
/// <param name="HasHaving">True when it has a <c>HAVING</c>.</param>
/// <param name="HasTop">True when it has a <c>TOP</c>.</param>
/// <param name="HasOffset">True when its <c>ORDER BY</c> has an <c>OFFSET</c>.</param>
/// <param name="HasFrom">True when it has a <c>FROM</c>.</param>
/// <param name="IsOneTable">
/// True when its <c>FROM</c> is one table alone, <c>FROM name [[AS] alias] [WITH (hints)]</c>, the name not one of the
/// <c>WITH</c> clause's.
/// </param>
internal sealed record SelectBody(
    IReadOnlyList<SelectItem> Items,
    TokenRange? FromThroughWhere,
    SelectGrouping Grouping,
    bool HasHaving,
    bool HasTop,
    bool HasOffset,
    bool HasFrom,
    bool IsOneTable
)
{
    // The words that start another statement.  T-SQL needs no ";" between two, so one of them at the select's level
    // is a batch of several.
    private static readonly HashSet<string> StatementWords =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "SELECT",
        "INSERT",
        "UPDATE",
        "DELETE",
        "DECLARE",
        "SET",
        "EXEC",
        "EXECUTE",
        "IF",
        "WHILE",
        "BEGIN",
        "RETURN",
        "PRINT",
        "RAISERROR",
        "THROW",
        "CREATE",
        "ALTER",
        "DROP",
        "TRUNCATE",
        "WAITFOR",
        "USE",
    ];

    // The clauses after the select list, in the order T-SQL takes them.
    private enum Clause
    {
        From,
        Where,
        GroupBy,
        Having,
        Window,
        OrderBy,
        Option,
    }

    /// <summary>
    /// The <c>SELECT</c> that a range of tokens holds, or null when the range is not one the reader can follow.
    /// </summary>
    /// <param name="tokens">The query's tokens.</param>
    /// <param name="range">The <c>SELECT</c>'s tokens, from the word <c>SELECT</c> to its end.</param>
    /// <param name="cteNames">The names the query's <c>WITH</c> clause defines, which are no table.</param>
    /// <remarks>
    /// <para>
    /// Depth counts parentheses and <c>CASE</c> ... <c>END</c>, so a subquery's clauses and a <c>CASE</c>'s words are
    /// never the select's.  The select list ends at the first clause; a clause that comes twice or out of its order,
    /// an <c>INTO</c>, a <c>FOR XML</c>, <c>FOR JSON</c> or <c>FOR BROWSE</c>, a <c>UNION</c>, <c>EXCEPT</c> or
    /// <c>INTERSECT</c>, a <c>;</c>, and a word that starts another statement give null.  A <c>FOR</c> that is none
    /// of those three, as <c>FOR SYSTEM_TIME</c>, is part of the clause it stands in.
    /// </para>
    /// <para>The list is split into items at its commas, and an empty item gives null.</para>
    /// </remarks>
    public static SelectBody? Read(
        IReadOnlyList<TsqlToken> tokens,
        TokenRange range,
        IReadOnlyCollection<string> cteNames
    )
    {
        if (range.IsEmpty || !tokens[range.Start].Is("SELECT") || !IsBalanced(tokens, range))
        {
            return null;
        }

        var at = range.Start + 1;
        if (at < range.End && (tokens[at].Is("DISTINCT") || tokens[at].Is("ALL")))
        {
            at++;
        }

        var hasTop = at < range.End && tokens[at].Is("TOP");
        if (hasTop)
        {
            at = TopEnd(tokens, at + 1, range.End);
            if (at < 0)
            {
                return null;
            }
        }

        var clauses = Clauses(tokens, at, range.End);
        if (clauses is null)
        {
            return null;
        }

        var items = SplitItems(tokens, new TokenRange(at, clauses.Count > 0 ? clauses[0].Start : range.End));
        if (items is null)
        {
            return null;
        }

        var from = RangeOf(clauses, Clause.From);
        var where = RangeOf(clauses, Clause.Where);
        var groupBy = RangeOf(clauses, Clause.GroupBy);
        var orderBy = RangeOf(clauses, Clause.OrderBy);
        var grouping = groupBy is { } group ? GroupingOf(tokens, new TokenRange(group.Start + 2, group.End)) : null;
        if (groupBy is not null && grouping is null)
        {
            return null;
        }

        return new SelectBody(
            items,
            from is { } fromRange ? new TokenRange(fromRange.Start, (where ?? fromRange).End) : null,
            grouping ?? SelectGrouping.None,
            HasHaving: RangeOf(clauses, Clause.Having) is not null,
            hasTop,
            HasOffset: orderBy is { } order && AtLevel(tokens, order).Any(index => tokens[index].Is("OFFSET")),
            HasFrom: from is not null,
            IsOneTable: from is { } table
                && IsOneTableAlone(tokens, new TokenRange(table.Start + 1, table.End), cteNames)
        );
    }

    /// <summary>
    /// The change of depth a token makes: one for <c>(</c> and <c>CASE</c>, minus one for <c>)</c> and <c>END</c>.
    /// </summary>
    internal static int DepthChange(TsqlToken token)
    {
        if (token.Is("CASE"))
        {
            return 1;
        }

        return token.Is("END") ? -1 : ParenthesisChange(token);
    }

    /// <summary>The index of the <c>)</c> that closes the <c>(</c> at <paramref name="open" />, or -1.</summary>
    internal static int ClosingParenthesis(IReadOnlyList<TsqlToken> tokens, int open, int end)
    {
        var depth = 0;
        for (var index = open; index < end; index++)
        {
            depth += ParenthesisChange(tokens[index]);
            if (depth == 0)
            {
                return index;
            }
        }

        return -1;
    }

    private static int ParenthesisChange(TsqlToken token)
    {
        if (token.IsSymbol("("))
        {
            return 1;
        }

        return token.IsSymbol(")") ? -1 : 0;
    }

    private static bool IsBalanced(IReadOnlyList<TsqlToken> tokens, TokenRange range)
    {
        var depth = 0;
        for (var index = range.Start; index < range.End && depth >= 0; index++)
        {
            depth += DepthChange(tokens[index]);
        }

        return depth == 0;
    }

    /// <summary>
    /// The indexes of a balanced range's tokens that stand at its own level, by <see cref="DepthChange" />.
    /// </summary>
    internal static IEnumerable<int> AtLevel(IReadOnlyList<TsqlToken> tokens, TokenRange range)
    {
        var depth = 0;
        for (var index = range.Start; index < range.End; index++)
        {
            if (depth == 0)
            {
                yield return index;
            }

            depth += DepthChange(tokens[index]);
        }
    }

    // The end of "TOP n" or "TOP (expr)", with PERCENT and WITH TIES, or -1.
    private static int TopEnd(IReadOnlyList<TsqlToken> tokens, int at, int end)
    {
        if (at < end && tokens[at].IsSymbol("("))
        {
            var close = ClosingParenthesis(tokens, at, end);
            if (close < 0)
            {
                return -1;
            }

            at = close + 1;
        }
        else if (at < end && tokens[at].Kind == TsqlTokenKind.Number)
        {
            at++;
        }
        else
        {
            return -1;
        }

        if (at < end && tokens[at].Is("PERCENT"))
        {
            at++;
        }

        if (at + 1 < end && tokens[at].Is("WITH") && tokens[at + 1].Is("TIES"))
        {
            at += 2;
        }

        return at;
    }

    // Each clause after the select list, with where it starts and ends, or null when the select is not one to read.
    private static List<(Clause Kind, int Start, int End)>? Clauses(IReadOnlyList<TsqlToken> tokens, int at, int end)
    {
        var starts = new List<(Clause Kind, int Start)>();
        foreach (var index in AtLevel(tokens, new TokenRange(at, end)))
        {
            if (IsRefused(tokens, index, end))
            {
                return null;
            }

            if (ClauseAt(tokens, index, end) is { } clause)
            {
                if (starts.Count > 0 && starts[^1].Kind >= clause)
                {
                    return null;
                }

                starts.Add((clause, index));
            }
        }

        return
        [
            .. starts.Select(
                (start, position) =>
                    (start.Kind, start.Start, position + 1 < starts.Count ? starts[position + 1].Start : end)
            ),
        ];
    }

    private static bool IsRefused(IReadOnlyList<TsqlToken> tokens, int index, int end)
    {
        var token = tokens[index];
        return token.IsSymbol(";")
            || token.Is("INTO")
            || token.Is("UNION")
            || token.Is("EXCEPT")
            || token.Is("INTERSECT")
            || (token.Kind == TsqlTokenKind.Word && StatementWords.Contains(token.Text))
            || (
                token.Is("FOR")
                && Next(tokens, index, end) is { } next
                && (next.Is("XML") || next.Is("JSON") || next.Is("BROWSE"))
            );
    }

    private static Clause? ClauseAt(IReadOnlyList<TsqlToken> tokens, int index, int end)
    {
        var token = tokens[index];
        if (token.Kind != TsqlTokenKind.Word)
        {
            return null;
        }

        var byFollows = Next(tokens, index, end) is { } next && next.Is("BY");
        return token.Text.ToUpperInvariant() switch
        {
            "FROM" => Clause.From,
            "WHERE" => Clause.Where,
            "GROUP" when byFollows => Clause.GroupBy,
            "HAVING" => Clause.Having,
            "WINDOW" => Clause.Window,
            "ORDER" when byFollows => Clause.OrderBy,
            "OPTION" => Clause.Option,
            _ => null,
        };
    }

    private static TsqlToken? Next(IReadOnlyList<TsqlToken> tokens, int index, int end) =>
        index + 1 < end ? tokens[index + 1] : null;

    private static TokenRange? RangeOf(List<(Clause Kind, int Start, int End)> clauses, Clause kind)
    {
        foreach (var (clause, start, end) in clauses)
        {
            if (clause == kind)
            {
                return new TokenRange(start, end);
            }
        }

        return null;
    }

    // The items, split at the list's commas, or null when the list or one of its items is empty.
    private static List<SelectItem>? SplitItems(IReadOnlyList<TsqlToken> tokens, TokenRange list)
    {
        if (list.IsEmpty)
        {
            return null;
        }

        var items = new List<SelectItem>();
        var start = list.Start;
        foreach (var index in AtLevel(tokens, list).Append(list.End))
        {
            if (index < list.End && !tokens[index].IsSymbol(","))
            {
                continue;
            }

            if (index == start)
            {
                return null;
            }

            items.Add(SelectItem.Read(tokens, new TokenRange(start, index)));
            start = index + 1;
        }

        return items;
    }

    // What follows GROUP BY, or null when nothing does.  Any ROLLUP, CUBE, GROUPING SETS or "()" makes it Sets, which
    // proves the least: a misread Sets only leaves a column nullable.
    private static SelectGrouping? GroupingOf(IReadOnlyList<TsqlToken> tokens, TokenRange group)
    {
        if (group.IsEmpty)
        {
            return null;
        }

        if (tokens[group.Start].Is("ALL"))
        {
            return SelectGrouping.All;
        }

        for (var index = group.Start; index < group.End; index++)
        {
            var token = tokens[index];
            if (
                token.Is("ROLLUP")
                || token.Is("CUBE")
                || (token.Is("GROUPING") && Next(tokens, index, group.End) is { } sets && sets.Is("SETS"))
                || (token.IsSymbol("(") && Next(tokens, index, group.End) is { } close && close.IsSymbol(")"))
            )
            {
                return SelectGrouping.Sets;
            }
        }

        return SelectGrouping.Plain;
    }

    // "name [[AS] alias] [WITH (hints)]" and nothing else, the name of one to four parts and not a CTE's.
    private static bool IsOneTableAlone(
        IReadOnlyList<TsqlToken> tokens,
        TokenRange from,
        IReadOnlyCollection<string> cteNames
    )
    {
        var at = from.Start;
        var parts = 0;
        while (at < from.End && tokens[at].IsName)
        {
            parts++;
            at++;
            if (at < from.End && tokens[at].IsSymbol("."))
            {
                at++;
                if (at >= from.End || !tokens[at].IsName)
                {
                    return false;
                }
            }
            else
            {
                break;
            }
        }

        if (
            parts == 0
            || parts > 4
            || (parts == 1 && cteNames.Contains(tokens[from.Start].Name, StringComparer.OrdinalIgnoreCase))
        )
        {
            return false;
        }

        if (at < from.End && tokens[at].Is("AS"))
        {
            at++;
            if (at >= from.End || !tokens[at].IsName)
            {
                return false;
            }
        }

        if (at < from.End && tokens[at].IsName && !tokens[at].Is("WITH"))
        {
            at++;
        }

        if (at + 1 < from.End && tokens[at].Is("WITH") && tokens[at + 1].IsSymbol("("))
        {
            at = ClosingParenthesis(tokens, at + 1, from.End) + 1;
        }

        return at == from.End;
    }
}
