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
/// returns from <c>NULL</c>, through earlier CTEs too.  A CTE of several branches is not read.
/// </para>
/// <para>
/// A <c>LEFT JOIN</c> whose source a conjunct rejects, by a column qualified with that source's name, returns only rows
/// that found a match.  So <see cref="Cuts" /> leaves its <c>LEFT</c> and <c>OUTER</c> out of the text of
/// <c>operands</c>, and the server answers that source's columns as an inner join's over the same rows.
/// </para>
/// </remarks>
internal sealed class FilteredRows
{
    private readonly IReadOnlyList<TsqlToken> _tokens;
    private readonly RowFilter _filter;
    private readonly IReadOnlyList<JoinSource>? _sources;
    private readonly IReadOnlyList<CommonTableExpression> _ctes;
    private readonly HashSet<string> _madeInner;

    private FilteredRows(
        IReadOnlyList<TsqlToken> tokens,
        RowFilter filter,
        IReadOnlyList<JoinSource>? sources,
        IReadOnlyList<CommonTableExpression> ctes,
        HashSet<string> madeInner,
        IReadOnlyList<int> cuts
    )
    {
        _tokens = tokens;
        _filter = filter;
        _sources = sources;
        _ctes = ctes;
        _madeInner = madeInner;
        Cuts = cuts;
    }

    /// <summary>The tokens to leave out of the <c>FROM</c> in the text of <c>operands</c>, in order.</summary>
    public IReadOnlyList<int> Cuts { get; }

    /// <summary>What the <c>FROM</c> and <c>WHERE</c> of a <c>SELECT</c> keep from being <c>NULL</c>.</summary>
    /// <param name="tokens">The query's tokens.</param>
    /// <param name="body">The <c>SELECT</c>.</param>
    /// <param name="ctes">The CTEs its <c>FROM</c> can read.</param>
    public static FilteredRows Read(
        IReadOnlyList<TsqlToken> tokens,
        SelectBody body,
        IReadOnlyList<CommonTableExpression> ctes
    )
    {
        var filter = RowFilter.Read(tokens, body.Where);
        var sources = body.From is { } from ? JoinChain.Read(tokens, from) : null;
        var madeInner = new HashSet<string>(StringComparer.Ordinal);
        var cuts = new List<int>();
        foreach (var source in sources ?? [])
        {
            if (
                source.Outer.Count > 0
                && sources!.Count(other => string.Equals(other.Name, source.Name, StringComparison.Ordinal)) == 1
                && filter.Columns.Any(column =>
                    column.Length == 2 && string.Equals(column[0], source.Name, StringComparison.Ordinal)
                )
            )
            {
                _ = madeInner.Add(source.Name);
                cuts.AddRange(source.Outer);
            }
        }

        return new FilteredRows(tokens, filter, sources, ctes, madeInner, cuts);
    }

    /// <summary>True when the range is a column alone that these rows hold no <c>NULL</c> of.</summary>
    public bool KeepsFromNull(TokenRange range) =>
        RowFilter.NameOf(_tokens, range) is { } parts && (_filter.Keeps(parts) || IsKeptByACte(parts));

    private bool IsKeptByACte(string[] parts)
    {
        if (SourceOf(parts) is not { OnePartName: { } name } source || IsOptional(source))
        {
            return false;
        }

        var at = -1;
        for (var index = 0; index < _ctes.Count; index++)
        {
            if (string.Equals(_ctes[index].Name, name, StringComparison.Ordinal))
            {
                at = index;
            }
        }

        return at >= 0 && CteKeeps(at, parts[^1]);
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

        return Read(_tokens, body, [.. _ctes.Take(at)]).KeepsFromNull(body.Items[place].Expression);
    }
}
