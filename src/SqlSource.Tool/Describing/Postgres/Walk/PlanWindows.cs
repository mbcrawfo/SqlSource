using System;
using System.Collections.Frozen;
using System.Collections.Generic;
using System.Linq;
using SqlSource.Tool.Describing.Postgres.Walk.Expressions;
using Kind = SqlSource.Tool.Describing.Postgres.Walk.Expressions.PlanTokens.Kind;

namespace SqlSource.Tool.Describing.Postgres.Walk;

/// <summary>
/// What the <c>Window</c> key of a <c>WindowAgg</c> says of its frame, as PostgreSQL 18 prints it:
/// <c>w1 AS (PARTITION BY t.k ORDER BY t.id ROWS UNBOUNDED PRECEDING)</c>.  PostgreSQL 14 to 17 print no such key.
/// </summary>
/// <remarks>
/// An aggregate over a frame that holds the current row has a row to read, so one of <see cref="TotalFunctions" /> over
/// a value that is never <c>NULL</c> is never <c>NULL</c> either.  Only a bound of <c>UNBOUNDED</c> or <c>CURRENT
/// ROW</c> is read: an offset's frame depends on the order type's <c>in_range</c> function for <c>RANGE</c>, and an
/// <c>EXCLUDE</c> may take the current row out.  A window with no frame clause has the default frame, from the start of
/// the partition to the current row and its peers, or the whole partition with no <c>ORDER BY</c>.
/// </remarks>
internal static class PlanWindows
{
    // What may follow ROWS, RANGE or GROUPS, up to the window's closing parenthesis.
    private static readonly FrozenSet<string> FramesWithTheCurrentRow = FrozenSet.ToFrozenSet(
        [
            "UNBOUNDED PRECEDING",
            "CURRENT ROW",
            "BETWEEN UNBOUNDED PRECEDING AND CURRENT ROW",
            "BETWEEN UNBOUNDED PRECEDING AND UNBOUNDED FOLLOWING",
            "BETWEEN CURRENT ROW AND CURRENT ROW",
            "BETWEEN CURRENT ROW AND UNBOUNDED FOLLOWING",
        ],
        StringComparer.Ordinal
    );

    private static readonly FrozenSet<string> FrameModes = FrozenSet.ToFrozenSet(
        ["ROWS", "RANGE", "GROUPS"],
        StringComparer.Ordinal
    );

    /// <summary>
    /// Whether the text is the definition of the window of that name, and its frame holds the current row.
    /// </summary>
    /// <param name="window">The node's <c>Window</c>, or null for a node that prints none.</param>
    /// <param name="name">The window's name as the aggregate prints it after <c>OVER</c>.</param>
    public static bool HoldsCurrentRow(string? window, string name)
    {
        if (window is null || PlanTokens.Read(window) is not { Count: >= 4 } tokens)
        {
            return false;
        }

        string TextOf(PlanTokens.Token token) => window[token.Start..token.End];

        if (
            !string.Equals(TextOf(tokens[0]), name, StringComparison.Ordinal)
            || tokens[1].Kind != Kind.Word
            || !string.Equals(TextOf(tokens[1]), "AS", StringComparison.Ordinal)
            || tokens[2].Kind != Kind.Open
            || tokens[^1].Kind != Kind.Close
        )
        {
            return false;
        }

        // The tokens at the depth of the window's own parentheses, which must close at the last token.
        var depth = 0;
        var top = new List<PlanTokens.Token>();
        for (var at = 3; at < tokens.Count - 1; at++)
        {
            var token = tokens[at];
            if (token.Kind is Kind.Close or Kind.CloseBracket)
            {
                depth--;
                if (depth < 0)
                {
                    return false;
                }
            }

            if (depth == 0)
            {
                top.Add(token);
            }

            if (token.Kind is Kind.Open or Kind.OpenBracket)
            {
                depth++;
            }
        }

        if (depth != 0)
        {
            return false;
        }

        var mode = top.FindIndex(token => token.Kind == Kind.Word && FrameModes.Contains(TextOf(token)));
        return mode < 0 || FramesWithTheCurrentRow.Contains(string.Join(' ', top.Skip(mode + 1).Select(TextOf)));
    }
}
