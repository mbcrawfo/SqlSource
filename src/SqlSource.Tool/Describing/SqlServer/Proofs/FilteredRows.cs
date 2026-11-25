using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// What the <c>FROM</c> and <c>WHERE</c> of one <c>SELECT</c> keep from being <c>NULL</c> in its rows.
/// </summary>
/// <remarks>
/// <para>
/// A column is kept from <c>NULL</c> when a conjunct of the <c>WHERE</c> rejects it (<see cref="RowFilter" />), or when
/// it is read from a CTE that no join here can make <c>NULL</c>, and that CTE's own <c>WHERE</c> keeps the column it
/// returns from <c>NULL</c>, through earlier CTEs too.  A CTE of several branches keeps nothing from <c>NULL</c> this
/// way.  <see cref="CteColumnOf" /> names the column of a CTE of one branch or more, which <see cref="CteProofs" />
/// proves by its branches.
/// </para>
/// <para>
/// A <c>LEFT JOIN</c> whose source a conjunct rejects, by a column qualified with that source's name, returns only rows
/// that found a match.  So <see cref="Cuts" /> leaves its <c>LEFT</c> and <c>OUTER</c> out of the text of
/// <c>operands</c>, and the server answers that source's columns as an inner join's over the same rows, provided that
/// it names one of those columns as stored (<see cref="CutGuards" />).
/// </para>
/// </remarks>
internal sealed class FilteredRows
{
    private readonly IReadOnlyList<TsqlToken> _tokens;
    private readonly TokenRange? _from;
    private readonly RowFilter _filter;
    private readonly IReadOnlyList<JoinSource>? _sources;
    private readonly IReadOnlyList<CommonTableExpression> _ctes;
    private readonly HashSet<string> _madeInner;

    private FilteredRows(
        IReadOnlyList<TsqlToken> tokens,
        TokenRange? from,
        RowFilter filter,
        IReadOnlyList<JoinSource>? sources,
        IReadOnlyList<CommonTableExpression> ctes,
        HashSet<string> madeInner,
        IReadOnlyList<int> cuts,
        IReadOnlyList<IReadOnlyList<TokenRange>> cutGuards
    )
    {
        _tokens = tokens;
        _from = from;
        _filter = filter;
        _sources = sources;
        _ctes = ctes;
        _madeInner = madeInner;
        Cuts = cuts;
        CutGuards = cutGuards;
    }

    /// <summary>The tokens to leave out of the <c>FROM</c> in the text of <c>operands</c>, in order.</summary>
    public IReadOnlyList<int> Cuts { get; }

    /// <summary>
    /// For each <c>LEFT JOIN</c> of <see cref="Cuts" />, the columns of the filter that name its source.  One of them
    /// must be a stored column for the cut to hold: a name under the alias can be a property of a CLR column of another
    /// source, as <c>loc.Lat</c> of <c>p.loc</c>, which a row with no match does not make <c>NULL</c>.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<TokenRange>> CutGuards { get; }

    /// <summary>What the <c>FROM</c> and <c>WHERE</c> of a <c>SELECT</c> keep from being <c>NULL</c>.</summary>
    /// <param name="tokens">The query's tokens.</param>
    /// <param name="body">The <c>SELECT</c>.</param>
    /// <param name="ctes">The CTEs its <c>FROM</c> can read.</param>
    /// <param name="makesJoinsInner">
    /// False for a CTE's body, which has no call of <c>operands</c> to show a cut's <see cref="CutGuards" />.
    /// </param>
    public static FilteredRows Read(
        IReadOnlyList<TsqlToken> tokens,
        SelectBody body,
        IReadOnlyList<CommonTableExpression> ctes,
        bool makesJoinsInner = true
    )
    {
        var filter = RowFilter.Read(tokens, body.Where);
        var sources = body.From is { } from ? JoinChain.Read(tokens, from) : null;
        var madeInner = new HashSet<string>(StringComparer.Ordinal);
        var cuts = new List<int>();
        var guards = new List<IReadOnlyList<TokenRange>>();
        foreach (var source in makesJoinsInner ? sources ?? [] : [])
        {
            var named = filter.ColumnsOf(source.Name);
            if (
                source.Outer.Count > 0
                && named.Count > 0
                && sources!.Count(other => string.Equals(other.Name, source.Name, StringComparison.Ordinal)) == 1
            )
            {
                _ = madeInner.Add(source.Name);
                cuts.AddRange(source.Outer);
                guards.Add(named);
            }
        }

        return new FilteredRows(tokens, body.From, filter, sources, ctes, madeInner, cuts, guards);
    }

    /// <summary>
    /// The pieces of the user's <c>FROM</c> that give the rows of the scope, each a run of its tokens, in order; or
    /// null for <see cref="OperandScope.Rows" />, whose rows are the body's own <c>FROM</c> through its
    /// <c>WHERE</c>, and for a side when the <c>FROM</c> has no <c>FULL JOIN</c> this reads.
    /// </summary>
    /// <remarks>
    /// The left side of the last <c>FULL JOIN</c> is the <c>FROM</c> from its word <c>FROM</c> to the first word of
    /// that join, and its right side the word <c>FROM</c> and the source that join adds, without its <c>ON</c>.
    /// Neither has the <c>WHERE</c>.
    /// </remarks>
    public IReadOnlyList<TokenRange>? FromOf(OperandScope scope)
    {
        if (scope == OperandScope.Rows || _from is not { } from || _sources is null)
        {
            return null;
        }

        var full = _sources.LastOrDefault(static source => source.IsFull);
        if (full is null)
        {
            return null;
        }

        return scope switch
        {
            OperandScope.LeftSide => [new TokenRange(from.Start, full.JoinStart)],
            OperandScope.RightSide => [new TokenRange(from.Start, from.Start + 1), full.Reference],
            OperandScope.Rows => null,
            _ => throw UnexpectedEnumValueException.Of(scope),
        };
    }

    /// <summary>
    /// The side of the last <c>FULL JOIN</c> whose source the range reads: a column <c>q.c</c> of two parts whose
    /// <c>q</c> names, ordinally, exactly one source, which is before that join or is the source it adds; or null.
    /// </summary>
    /// <remarks>
    /// Every row of a <c>FULL JOIN</c> has its left side or its right side, and a later join that is no <c>RIGHT</c> or
    /// <c>FULL</c> one keeps that true of every row of the <c>FROM</c>, and the <c>WHERE</c> keeps some of them.  So
    /// there is no side when no <c>FULL JOIN</c> is read or a <c>RIGHT JOIN</c> comes after the last one, and a source
    /// added after it is of neither.
    /// </remarks>
    public OperandScope? SideOf(TokenRange range)
    {
        if (_sources is not { } sources || RowFilter.NameOf(_tokens, range) is not [var qualifier, _])
        {
            return null;
        }

        var full = Enumerable.Range(0, sources.Count).LastOrDefault(index => sources[index].IsFull, -1);
        if (
            full < 0
            || sources.Skip(full + 1).Any(static source => source.IsRight)
            || Enumerable
                .Range(0, sources.Count)
                .Where(index => string.Equals(sources[index].Name, qualifier, StringComparison.Ordinal))
                .ToList()
                is not [var at]
        )
        {
            return null;
        }

        if (at < full)
        {
            return OperandScope.LeftSide;
        }

        return at == full ? OperandScope.RightSide : null;
    }

    /// <summary>
    /// True when the range, a column alone or a <c>q.*</c>, reads a source that is a table no join of these rows can
    /// make <c>NULL</c>: its one source is no derived table, is not optional, and has no one-part name that a CTE has,
    /// compared ignoring case.
    /// </summary>
    /// <remarks>
    /// The server names a table as the source of a column that a derived table or a CTE passes on, and names it still
    /// where an outer join makes the column <c>NULL</c>, so a row of the result is a row of that table only so.  A
    /// CTE's name is compared ignoring case, as a database whose collation ignores it resolves the name; whether the
    /// source is a table, and no view or function, the caller reads from the column's row.
    /// </remarks>
    public bool IsOfATableNoJoinMakesNull(TokenRange range)
    {
        string[]? parts;
        if (range.Count == 3 && _tokens[range.End - 1].IsSymbol("*"))
        {
            parts =
                _tokens[range.End - 2].IsSymbol(".")
                && RowFilter.NameOf(_tokens, new TokenRange(range.Start, range.Start + 1)) is [var qualifier]
                    ? [qualifier, "*"]
                    : null;
        }
        else
        {
            parts = RowFilter.NameOf(_tokens, range);
        }

        return parts is not null
            && SourceOf(parts) is { IsDerived: false, IsOptional: false } source
            && (
                source.OnePartName is not { } name
                || !_ctes.Any(cte => string.Equals(cte.Name, name, StringComparison.OrdinalIgnoreCase))
            );
    }

    /// <summary>True when the range is a column alone that these rows hold no <c>NULL</c> of.</summary>
    public bool KeepsFromNull(TokenRange range) =>
        RowFilter.NameOf(_tokens, range) is { } parts && (_filter.Keeps(parts) || IsKeptByACte(parts));

    /// <summary>
    /// The place of the CTE, among those these rows can read, and of its column, that the range reads: a column alone
    /// of a source that names a CTE of one branch or more, which no join here can make <c>NULL</c>; or null.
    /// </summary>
    /// <remarks>
    /// The CTE's columns are its column list, or else the names of its first branch's items, and each branch must have
    /// as many items, none a <c>*</c>.  The column is the one place whose name is the range's last part, compared
    /// ordinally.  A recursive member reads its own CTE so.
    /// </remarks>
    public (int Cte, int Column)? CteColumnOf(TokenRange range)
    {
        if (
            RowFilter.NameOf(_tokens, range) is not { } parts
            || SourceOf(parts) is not { OnePartName: { } name } source
            || IsOptional(source)
            || LastCteNamed(name) is not { } at
            || _ctes[at] is not { Branches: { Count: > 0 } branches } cte
        )
        {
            return null;
        }

        var count = cte.Columns?.Count ?? branches[0].Items.Count;
        if (branches.Any(branch => branch.Items.Count != count || branch.Items.Any(static item => item.IsStar)))
        {
            return null;
        }

        var places = Enumerable
            .Range(0, count)
            .Where(index =>
                string.Equals(cte.Columns?[index] ?? branches[0].Items[index].Name, parts[^1], StringComparison.Ordinal)
            )
            .ToList();
        return places is [var place] ? (at, place) : null;
    }

    private bool IsKeptByACte(string[] parts)
    {
        return SourceOf(parts) is { OnePartName: { } name } source
            && !IsOptional(source)
            && LastCteNamed(name) is { } at
            && CteKeeps(at, parts[^1]);
    }

    // The place of the last CTE these rows can read of that name, compared ordinally, or null.
    private int? LastCteNamed(string name)
    {
        for (var index = _ctes.Count - 1; index >= 0; index--)
        {
            if (string.Equals(_ctes[index].Name, name, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return null;
    }

    // The one source a column's name reads: the one its qualifier names, or the only one for a bare name.
    private JoinSource? SourceOf(string[] parts)
    {
        if (_sources is null)
        {
            return null;
        }

        if (parts.Length == 1)
        {
            return _sources.Count == 1 ? _sources[0] : null;
        }

        if (parts.Length != 2)
        {
            return null;
        }

        var named = _sources.Where(source => string.Equals(source.Name, parts[0], StringComparison.Ordinal)).ToList();
        return named.Count == 1 ? named[0] : null;
    }

    private bool IsOptional(JoinSource source) =>
        _madeInner.Contains(source.Name) ? source.IsOptionalBesidesItsLeftJoin : source.IsOptional;

    // True when the CTE's column of that name is an item that is a column alone, kept from NULL in the CTE's own rows.
    // Its rows read only the CTEs before it.  A grouping set's total and GROUP BY ALL hold rows its WHERE did not keep.
    private bool CteKeeps(int at, string column)
    {
        var cte = _ctes[at];
        if (cte.Body is not { Grouping: SelectGrouping.None or SelectGrouping.Plain } body)
        {
            return false;
        }

        if (body.Items.Any(static item => item.IsStar) || (cte.Columns is { } names && names.Count != body.Items.Count))
        {
            return false;
        }

        var places = Enumerable
            .Range(0, body.Items.Count)
            .Where(index =>
                string.Equals(cte.Columns?[index] ?? body.Items[index].Name, column, StringComparison.Ordinal)
            )
            .ToList();
        if (places is not [var place] || !body.Items[place].IsColumn(_tokens))
        {
            return false;
        }

        return Read(_tokens, body, [.. _ctes.Take(at)], makesJoinsInner: false)
            .KeepsFromNull(body.Items[place].Expression);
    }
}
