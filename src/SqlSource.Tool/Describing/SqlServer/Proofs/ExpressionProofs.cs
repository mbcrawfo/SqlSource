using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

/// <summary>
/// The proofs that the items of one <c>SELECT</c> cannot be <c>NULL</c>, read from their tokens, and the operands they
/// leave to ask the server.
/// </summary>
/// <remarks>
/// <para>
/// An expression is split at its own level on <c>+</c>, <c>-</c>, <c>*</c>, <c>/</c> and <c>%</c>, a sign before a
/// piece being unary, and is proved when every piece is.  A piece is a primary: a literal, <c>NULL</c>, a parameter, a
/// column, <c>( ... )</c>, a subquery, a <c>CASE</c> or a call.  A call of a bare name may be a form that proves it;
/// one of a schema's name is a user's function, which proves nothing.  An expression with anything else at its own
/// level is a leaf, and so are a column, a <c>CASE</c>, a subquery that is not a <c>COUNT</c>'s, and a call that is no
/// form.
/// </para>
/// <para>
/// A leaf is asked when its context allows: the rows' context, an aggregate's operand without <c>OVER</c>, always;
/// the select's context, everything else, unless the <c>GROUP BY</c> is a <see cref="SelectGrouping.Sets" />, whose
/// rows of a total hold a grouping column as <c>NULL</c>.  A leaf that holds anywhere an aggregate's name before
/// <c>(</c>, <c>OVER</c> or a call of a schema's name, or that is a parameter or <c>NULL</c>, is never asked: outside
/// its <c>GROUP BY</c> it means something else, or the server's answer about it proves nothing.
/// </para>
/// <para>
/// An operand is numbered only when the item's proof still needs it, so one whose answer cannot change the proof, as
/// in <c>COALESCE(a, 0)</c>, is not asked.  The forms assume <c>ANSI_WARNINGS</c> on, under which an overflow is an
/// error and not a <c>NULL</c>.
/// </para>
/// </remarks>
internal sealed class ExpressionProofs(IReadOnlyList<TsqlToken> tokens, SelectBody body)
{
    // The built-in aggregates.  A leaf that holds one is never asked, so a name too many only proves less.
    private static readonly HashSet<string> Aggregates =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "COUNT",
        "COUNT_BIG",
        "SUM",
        "AVG",
        "MIN",
        "MAX",
        "STDEV",
        "STDEVP",
        "VAR",
        "VARP",
        "STRING_AGG",
        "GROUPING",
        "GROUPING_ID",
        "CHECKSUM_AGG",
        "APPROX_COUNT_DISTINCT",
        "APPROX_PERCENTILE_CONT",
        "APPROX_PERCENTILE_DISC",
        "JSON_ARRAYAGG",
        "JSON_OBJECTAGG",
        "PRODUCT",
    ];

    // The bare names of the calls that are forms.  A form whose conditions fail proves nothing and is not asked.
    private static readonly HashSet<string> Forms =
    [
        with(StringComparer.OrdinalIgnoreCase),
        "COUNT",
        "COUNT_BIG",
        "ROW_NUMBER",
        "RANK",
        "DENSE_RANK",
        "NTILE",
        "SUM",
        "MAX",
        "MIN",
        "COALESCE",
        "GREATEST",
        "LEAST",
        "CAST",
        "CONVERT",
        "JSON_OBJECT",
        "JSON_ARRAY",
    ];

    private readonly List<TokenRange> _operands = [];

    // The leaves of the item being proved, which a Proof.Operand indexes until Of numbers the ones its proof keeps.
    private readonly List<TokenRange> _leaves = [];

    /// <summary>
    /// The operands to ask, each text once, in the order the items' proofs first use them; a
    /// <see cref="Proof.Operand" /> indexes it.
    /// </summary>
    public IReadOnlyList<TokenRange> Operands => _operands;

    /// <summary>The proof of an item's expression, in the select's context.</summary>
    /// <remarks>Under <c>GROUP BY ALL</c>, which returns a group that no row holds, nothing is proved.</remarks>
    public Proof Of(TokenRange expression)
    {
        if (body.Grouping == SelectGrouping.All)
        {
            return Proof.No;
        }

        _leaves.Clear();
        return Numbered(Expression(expression, rows: false));
    }

    private static Proof AnyOf(IEnumerable<Proof> parts)
    {
        var kept = new List<Proof>();
        foreach (var part in parts)
        {
            if (part is Proof.Always)
            {
                return Proof.Yes;
            }

            if (part is not Proof.Never)
            {
                kept.Add(part);
            }
        }

        return kept.Count switch
        {
            0 => Proof.No,
            1 => kept[0],
            _ => new Proof.AnyOf(kept),
        };
    }

    private static Proof AllOf(IEnumerable<Proof> parts)
    {
        var kept = new List<Proof>();
        foreach (var part in parts)
        {
            if (part is Proof.Never)
            {
                return Proof.No;
            }

            if (part is not Proof.Always)
            {
                kept.Add(part);
            }
        }

        return kept.Count switch
        {
            0 => Proof.Yes,
            1 => kept[0],
            _ => new Proof.AllOf(kept),
        };
    }

    private static bool IsSign(TsqlToken token) => token.IsSymbol("-") || token.IsSymbol("+") || token.IsSymbol("~");

    private static bool IsOperator(TsqlToken token) =>
        token.Kind == TsqlTokenKind.Symbol && token.Text is "+" or "-" or "*" or "/" or "%";

    private static bool IsInteger(TsqlToken token) =>
        token.Kind == TsqlTokenKind.Number && token.Text.All(char.IsAsciiDigit);

    // The proof with each leaf it kept numbered as an operand, a text asked before taking its number.
    private Proof Numbered(Proof proof) =>
        proof switch
        {
            Proof.Operand leaf => new Proof.Operand(OperandIndex(_leaves[leaf.Index])),
            Proof.AnyOf any => new Proof.AnyOf([.. any.Parts.Select(Numbered)]),
            Proof.AllOf all => new Proof.AllOf([.. all.Parts.Select(Numbered)]),
            _ => proof,
        };

    private int OperandIndex(TokenRange range)
    {
        var index = _operands.FindIndex(operand => IsSameText(operand, range));
        if (index >= 0)
        {
            return index;
        }

        _operands.Add(range);
        return _operands.Count - 1;
    }

    private bool IsSameText(TokenRange left, TokenRange right) =>
        left.Count == right.Count
        && Enumerable
            .Range(0, left.Count)
            .All(offset =>
                tokens[left.Start + offset].Kind == tokens[right.Start + offset].Kind
                && string.Equals(
                    tokens[left.Start + offset].Text,
                    tokens[right.Start + offset].Text,
                    StringComparison.Ordinal
                )
            );

    private Proof Expression(TokenRange range, bool rows)
    {
        if (Pieces(range) is not { } pieces)
        {
            return Leaf(range, rows);
        }

        var proofs = new List<Proof>(pieces.Count);
        foreach (var piece in pieces)
        {
            if (Primary(piece, rows) is not { } proof)
            {
                return Leaf(range, rows);
            }

            proofs.Add(proof);
        }

        return AllOf(proofs);
    }

    // The pieces between the operators at the range's own level, without their unary signs, or null when the range
    // is empty or ends with an operator.
    private List<TokenRange>? Pieces(TokenRange range)
    {
        var pieces = new List<TokenRange>();
        var start = -1;
        foreach (var index in SelectBody.AtLevel(tokens, range))
        {
            var token = tokens[index];
            if (start < 0 && IsSign(token))
            {
                continue;
            }

            if (start >= 0 && IsOperator(token))
            {
                pieces.Add(new TokenRange(start, index));
                start = -1;
            }
            else if (start < 0)
            {
                start = index;
            }
        }

        if (start < 0)
        {
            return null;
        }

        pieces.Add(new TokenRange(start, range.End));
        return pieces;
    }

    // The proof of a piece that is a primary, or null when it is none.
    private Proof? Primary(TokenRange piece, bool rows)
    {
        var first = tokens[piece.Start];
        if (piece.Count == 1 && first.Kind is TsqlTokenKind.String or TsqlTokenKind.Number)
        {
            return Proof.Yes;
        }

        if (piece.Count == 1 && (first.Kind == TsqlTokenKind.Parameter || first.Is("NULL")))
        {
            return Proof.No;
        }

        if (first.IsSymbol("(") || first.Is("CASE"))
        {
            return IsOneGroup(piece) ? Group(piece, rows) : null;
        }

        if (!first.IsName)
        {
            return null;
        }

        var at = piece.Start + 1;
        var parts = 1;
        while (at + 1 < piece.End && tokens[at].IsSymbol(".") && tokens[at + 1].IsName)
        {
            at += 2;
            parts++;
        }

        if (at == piece.End)
        {
            return Leaf(piece, rows);
        }

        if (!tokens[at].IsSymbol("("))
        {
            return null;
        }

        // A call of a schema's name is a user's function or a CLR aggregate.
        return parts == 1 ? Call(piece, rows) : Proof.No;
    }

    // True when the piece is one "( ... )" or "CASE ... END": only its first token stands at its own level.
    private bool IsOneGroup(TokenRange piece) => SelectBody.AtLevel(tokens, piece).Skip(1).FirstOrDefault(-1) < 0;

    private Proof Group(TokenRange piece, bool rows)
    {
        if (tokens[piece.Start].Is("CASE"))
        {
            return Leaf(piece, rows);
        }

        var inner = new TokenRange(piece.Start + 1, piece.End - 1);
        return !inner.IsEmpty && tokens[inner.Start].Is("SELECT")
            ? Subquery(piece, inner, rows)
            : Expression(inner, rows);
    }

    // A scalar subquery of COUNT returns one row, and so its count, when nothing at its level can take that row away
    // or add another.
    private Proof Subquery(TokenRange piece, TokenRange inner, bool rows)
    {
        var subquery = SelectBody.Read(tokens, inner, []);
        var isOneCount =
            subquery
                is {
                    Grouping: SelectGrouping.None,
                    HasHaving: false,
                    HasTop: false,
                    HasOffset: false,
                    Items: [{ IsStar: false } item],
                }
            && IsCountAlone(item.Expression);
        return isOneCount ? Proof.Yes : Leaf(piece, rows);
    }

    // COUNT( ... ) or COUNT_BIG( ... ), with no OVER.
    private bool IsCountAlone(TokenRange expression) =>
        expression.Count >= 3
        && (tokens[expression.Start].Is("COUNT") || tokens[expression.Start].Is("COUNT_BIG"))
        && tokens[expression.Start + 1].IsSymbol("(")
        && SelectBody.ClosingParenthesis(tokens, expression.Start + 1, expression.End) == expression.End - 1;

    // "name ( ... ) [WITHIN GROUP ( ... )] [OVER ( ... ) | OVER name]", the name bare.
    private Proof? Call(TokenRange piece, bool rows)
    {
        var open = piece.Start + 1;
        var close = SelectBody.ClosingParenthesis(tokens, open, piece.End);
        if (close < 0)
        {
            return null;
        }

        var at = close + 1;
        var withinGroup = false;
        if (at + 2 < piece.End && tokens[at].Is("WITHIN") && tokens[at + 1].Is("GROUP") && tokens[at + 2].IsSymbol("("))
        {
            withinGroup = true;
            var groupClose = SelectBody.ClosingParenthesis(tokens, at + 2, piece.End);
            if (groupClose < 0)
            {
                return null;
            }

            at = groupClose + 1;
        }

        TokenRange? window = null;
        var namedWindow = false;
        if (at + 1 < piece.End && tokens[at].Is("OVER") && tokens[at + 1].IsSymbol("("))
        {
            var end = SelectBody.ClosingParenthesis(tokens, at + 1, piece.End);
            if (end < 0)
            {
                return null;
            }

            window = new TokenRange(at + 2, end);
            at = end + 1;
        }
        else if (at + 1 < piece.End && tokens[at].Is("OVER") && tokens[at + 1].IsName)
        {
            namedWindow = true;
            at += 2;
        }

        if (at != piece.End)
        {
            return null;
        }

        var name = tokens[piece.Start];
        if (name.Kind != TsqlTokenKind.Word || !Forms.Contains(name.Text))
        {
            return Leaf(piece, rows);
        }

        return withinGroup || namedWindow
            ? Proof.No
            : Form(name.Text.ToUpperInvariant(), new TokenRange(open + 1, close), window, rows);
    }

    private Proof Form(string name, TokenRange arguments, TokenRange? window, bool rows) =>
        name switch
        {
            "COUNT" or "COUNT_BIG" => Proof.Yes,
            "ROW_NUMBER" or "RANK" or "DENSE_RANK" when window is not null && arguments.IsEmpty => Proof.Yes,
            "NTILE" when window is not null && arguments.Count == 1 && IsInteger(tokens[arguments.Start]) => Proof.Yes,
            "SUM" or "MAX" or "MIN" => Aggregate(arguments, window, rows),
            "COALESCE" or "GREATEST" or "LEAST" when window is null => AnyOf(
                Arguments(arguments).Select(argument => Expression(argument, rows))
            ),
            "CAST" when window is null => Cast(arguments, rows),
            "CONVERT" when window is null => Convert(arguments, rows),
            "JSON_OBJECT" or "JSON_ARRAY" when window is null => Proof.Yes,
            _ => Proof.No,
        };

    // SUM, MAX or MIN of a value that is not null.  Without OVER, over a group, which always has a row: the operand
    // is in the rows' context.  With OVER, over a frame that holds the current row: the operand keeps the context.
    private Proof Aggregate(TokenRange arguments, TokenRange? window, bool rows)
    {
        var operand = arguments;
        if (!operand.IsEmpty && (tokens[operand.Start].Is("ALL") || tokens[operand.Start].Is("DISTINCT")))
        {
            operand = new TokenRange(operand.Start + 1, operand.End);
        }

        if (Arguments(operand).Count != 1)
        {
            return Proof.No;
        }

        if (window is { } over)
        {
            return HoldsCurrentRow(over) ? Expression(operand, rows) : Proof.No;
        }

        return body.Grouping == SelectGrouping.None ? Proof.No : Expression(operand, rows: true);
    }

    // A window with no frame holds the current row, as one whose frame starts at or before it and ends at or after
    // it.  One that names another window, "OVER (w ...)", takes that window's frame, which is not read; so a window
    // that does not start with PARTITION BY, ORDER BY or a frame's word may be named, as "partition" may be, and
    // proves nothing.
    private bool HoldsCurrentRow(TokenRange window)
    {
        if (window.IsEmpty)
        {
            return true;
        }

        var first = tokens[window.Start];
        var byFollows = window.Count > 1 && tokens[window.Start + 1].Is("BY");
        var isClause =
            ((first.Is("PARTITION") || first.Is("ORDER")) && byFollows) || first.Is("ROWS") || first.Is("RANGE");
        if (!isClause)
        {
            return false;
        }

        var frame = SelectBody
            .AtLevel(tokens, window)
            .Where(index => tokens[index].Is("ROWS") || tokens[index].Is("RANGE"))
            .FirstOrDefault(-1);
        return frame < 0 || IsFrameOfTheCurrentRow(frame + 1, window.End);
    }

    // "BETWEEN start AND end" or "start", the end then being the current row, and nothing after it.
    private bool IsFrameOfTheCurrentRow(int at, int end)
    {
        if (at < end && tokens[at].Is("BETWEEN"))
        {
            var and = BoundEnd(at + 1, end, "PRECEDING");
            return and > 0 && and < end && tokens[and].Is("AND") && BoundEnd(and + 1, end, "FOLLOWING") == end;
        }

        return BoundEnd(at, end, "PRECEDING") == end;
    }

    // The end of a bound on the current row's side, "CURRENT ROW", "UNBOUNDED <direction>" or "n <direction>", n an
    // integer literal, or -1.
    private int BoundEnd(int at, int end, string direction)
    {
        if (at + 1 >= end)
        {
            return -1;
        }

        var (first, second) = (tokens[at], tokens[at + 1]);
        var isBound =
            (first.Is("CURRENT") && second.Is("ROW"))
            || ((first.Is("UNBOUNDED") || IsInteger(first)) && second.Is(direction));
        return isBound ? at + 2 : -1;
    }

    private Proof Cast(TokenRange arguments, bool rows)
    {
        var asWords = SelectBody.AtLevel(tokens, arguments).Where(index => tokens[index].Is("AS")).ToList();
        return asWords is [var asWord] && asWord + 1 < arguments.End
            ? Expression(new TokenRange(arguments.Start, asWord), rows)
            : Proof.No;
    }

    // CONVERT(type, e) or CONVERT(type, e, style), the style an integer literal: a NULL style makes it NULL.
    private Proof Convert(TokenRange arguments, bool rows)
    {
        var parts = Arguments(arguments);
        var styleProves =
            parts.Count == 2 || (parts.Count == 3 && parts[2].Count == 1 && IsInteger(tokens[parts[2].Start]));
        return styleProves && !parts[0].IsEmpty ? Expression(parts[1], rows) : Proof.No;
    }

    // The arguments of a call, split at the commas of their own level.
    private List<TokenRange> Arguments(TokenRange arguments)
    {
        var parts = new List<TokenRange>();
        var start = arguments.Start;
        foreach (var index in SelectBody.AtLevel(tokens, arguments).Where(index => tokens[index].IsSymbol(",")))
        {
            parts.Add(new TokenRange(start, index));
            start = index + 1;
        }

        parts.Add(new TokenRange(start, arguments.End));
        return parts;
    }

    // True when the token at "at" is the second ":" of a "::", which the tokens give as two adjacent symbols.
    private bool IsDoubleColon(TokenRange range, int at) =>
        at - 1 >= range.Start
        && tokens[at].IsSymbol(":")
        && tokens[at - 1].IsSymbol(":")
        && tokens[at - 1].End == tokens[at].Start;

    // An operand to ask, when the context allows and the server's answer about it would hold in the item.
    private Proof Leaf(TokenRange range, bool rows)
    {
        if (
            range.IsEmpty
            || (!rows && body.Grouping == SelectGrouping.Sets)
            || (
                range.Count == 1
                && (tokens[range.Start].Kind == TsqlTokenKind.Parameter || tokens[range.Start].Is("NULL"))
            )
            || HoldsWhatIsNeverAsked(range)
        )
        {
            return Proof.No;
        }

        _leaves.Add(range);
        return new Proof.Operand(_leaves.Count - 1);
    }

    // OVER, an aggregate's name before "(", or a name after "." or "::" before "(", a call of a schema's name or a
    // type's method, anywhere in the range, its subqueries included.  An old table hint after a schema's table,
    // "FROM dbo.o (NOLOCK)", reads as such a call, and so makes the leaf never asked.
    private bool HoldsWhatIsNeverAsked(TokenRange range)
    {
        for (var index = range.Start; index < range.End; index++)
        {
            if (tokens[index].Is("OVER"))
            {
                return true;
            }

            if (!tokens[index].IsSymbol("(") || index - 1 < range.Start || !tokens[index - 1].IsName)
            {
                continue;
            }

            var name = tokens[index - 1];
            var isQualified =
                index - 2 >= range.Start && (tokens[index - 2].IsSymbol(".") || IsDoubleColon(range, index - 2));
            if (isQualified || Aggregates.Contains(name.Name!))
            {
                return true;
            }
        }

        return false;
    }
}
