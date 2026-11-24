using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// The columns that a <c>WHERE</c> clause keeps from being <c>NULL</c>, each as the parts of its name.
/// </summary>
/// <remarks>
/// <para>
/// Only a conjunct at the top level counts, and a column written alone in one of three shapes: <c>x IS NOT NULL</c>,
/// <c>x [NOT] LIKE p</c>, and a comparison with <c>x</c> on either side.  An <c>OR</c> at a level gives nothing at that
/// level, since no conjunct of it holds of every row.  A <c>NOT</c> before a conjunct gives nothing, and the <c>AND</c>
/// of a <c>BETWEEN</c> splits nothing.
/// </para>
/// <para>
/// A comparison with <c>ALL</c>, <c>ANY</c> or <c>SOME</c> gives nothing, since <c>x &gt; ALL (empty)</c> is true of a
/// <c>NULL</c> <c>x</c>, and neither does one with the literal <c>NULL</c>.  The comparisons assume <c>ANSI_NULLS</c>
/// on: with it off, <c>x = @p</c> keeps a <c>NULL</c> <c>x</c> when <c>@p</c> is <c>NULL</c>, and <c>x &lt;&gt; @p</c>
/// keeps one when <c>@p</c> is not.
/// </para>
/// </remarks>
internal sealed class RowFilter
{
    private static readonly HashSet<string> Comparisons = ["=", "<", ">", "<=", ">=", "<>", "!=", "!<", "!>"];

    private readonly List<string[]> _columns = [];

    private RowFilter() { }

    /// <summary>The names the filter keeps from being <c>NULL</c>, each as its parts.</summary>
    public IReadOnlyList<string[]> Columns => _columns;

    /// <summary>The filter of a <c>WHERE</c> clause, from the word <c>WHERE</c> to its end, or of none.</summary>
    public static RowFilter Read(IReadOnlyList<TsqlToken> tokens, TokenRange? where)
    {
        var filter = new RowFilter();
        if (where is { } clause)
        {
            filter.ReadConjuncts(tokens, new TokenRange(clause.Start + 1, clause.End));
        }

        return filter;
    }

    /// <summary>
    /// The parts of the name that a range writes, a column alone, or null.  Parts compare ordinally, so a name written
    /// otherwise, which a case-sensitive collation may make another column, is no match.
    /// </summary>
    internal static string[]? NameOf(IReadOnlyList<TsqlToken> tokens, TokenRange range)
    {
        if (range.IsEmpty || range.Count % 2 == 0)
        {
            return null;
        }

        var parts = new string[(range.Count + 1) / 2];
        for (var index = range.Start; index < range.End; index++)
        {
            var offset = index - range.Start;
            var token = tokens[index];
            if (offset % 2 == 1)
            {
                if (!token.IsSymbol("."))
                {
                    return null;
                }
            }
            else if (!token.IsName || token.Is("NULL"))
            {
                return null;
            }
            else
            {
                parts[offset / 2] = token.Name!;
            }
        }

        return parts;
    }

    /// <summary>True when the filter keeps the column of these parts from being <c>NULL</c>.</summary>
    public bool Keeps(string[] parts) => _columns.Any(column => column.SequenceEqual(parts, StringComparer.Ordinal));

    private static bool IsOneGroup(IReadOnlyList<TsqlToken> tokens, TokenRange range) =>
        range.Count >= 2
        && tokens[range.Start].IsSymbol("(")
        && SelectBody.ClosingParenthesis(tokens, range.Start, range.End) == range.End - 1;

    // True when the other side of a comparison leaves it unable to prove anything: a quantifier, or the literal NULL
    // in any number of parentheses.
    private static bool RefusesComparison(IReadOnlyList<TsqlToken> tokens, TokenRange other)
    {
        if (
            other.IsEmpty
            || tokens[other.Start].Is("ALL")
            || tokens[other.Start].Is("ANY")
            || tokens[other.Start].Is("SOME")
        )
        {
            return true;
        }

        while (IsOneGroup(tokens, other))
        {
            other = new TokenRange(other.Start + 1, other.End - 1);
        }

        return other.Count == 1 && tokens[other.Start].Is("NULL");
    }

    private void ReadConjuncts(IReadOnlyList<TsqlToken> tokens, TokenRange range)
    {
        var level = SelectBody.AtLevel(tokens, range).ToList();
        if (level.Exists(index => tokens[index].Is("OR")))
        {
            return;
        }

        var start = range.Start;
        var inBetween = false;
        foreach (var index in level)
        {
            if (tokens[index].Is("BETWEEN"))
            {
                inBetween = true;
            }
            else if (tokens[index].Is("AND"))
            {
                if (inBetween)
                {
                    inBetween = false;
                    continue;
                }

                ReadConjunct(tokens, new TokenRange(start, index));
                start = index + 1;
            }
        }

        ReadConjunct(tokens, new TokenRange(start, range.End));
    }

    private void ReadConjunct(IReadOnlyList<TsqlToken> tokens, TokenRange conjunct)
    {
        if (conjunct.IsEmpty)
        {
            return;
        }

        if (IsOneGroup(tokens, conjunct))
        {
            ReadConjuncts(tokens, new TokenRange(conjunct.Start + 1, conjunct.End - 1));
            return;
        }

        var level = SelectBody.AtLevel(tokens, conjunct).ToList();
        if (IsNotNullTest(tokens, conjunct))
        {
            Add(tokens, new TokenRange(conjunct.Start, conjunct.End - 3));
            return;
        }

        if (level.Exists(index => tokens[index].Is("LIKE")))
        {
            ReadLike(tokens, conjunct, level);
            return;
        }

        if (level.Exists(index => tokens[index].Is("NOT")))
        {
            return;
        }

        var comparisons = level
            .Where(index => tokens[index].Kind == TsqlTokenKind.Symbol && Comparisons.Contains(tokens[index].Text))
            .ToList();
        if (comparisons is not [var comparison])
        {
            return;
        }

        var left = new TokenRange(conjunct.Start, comparison);
        var right = new TokenRange(comparison + 1, conjunct.End);
        if (left.IsEmpty || RefusesComparison(tokens, right) || RefusesComparison(tokens, left))
        {
            return;
        }

        Add(tokens, left);
        Add(tokens, right);
    }

    private static bool IsNotNullTest(IReadOnlyList<TsqlToken> tokens, TokenRange conjunct) =>
        conjunct.Count >= 4
        && tokens[conjunct.End - 3].Is("IS")
        && tokens[conjunct.End - 2].Is("NOT")
        && tokens[conjunct.End - 1].Is("NULL");

    // "x LIKE p" or "x NOT LIKE p", with x a column alone.  Any other NOT at the level gives nothing.
    private void ReadLike(IReadOnlyList<TsqlToken> tokens, TokenRange conjunct, List<int> level)
    {
        var like = level.First(index => tokens[index].Is("LIKE"));
        var nameEnd = like > conjunct.Start && tokens[like - 1].Is("NOT") ? like - 1 : like;
        if (level.Exists(index => tokens[index].Is("NOT") && index != nameEnd))
        {
            return;
        }

        Add(tokens, new TokenRange(conjunct.Start, nameEnd));
    }

    private void Add(IReadOnlyList<TsqlToken> tokens, TokenRange side)
    {
        if (NameOf(tokens, side) is { } parts)
        {
            _columns.Add(parts);
        }
    }
}
