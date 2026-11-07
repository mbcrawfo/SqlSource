using System;
using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

// The calls: a call read into its parts, and the proof of each form, from the one table of forms that the type walk
// (ExpressionProofs.Types.cs) reads too.
internal sealed partial class ExpressionProofs
{
    // The bare names of the calls that are forms, each with its kind.  A form whose conditions fail proves nothing
    // and is not asked.  A new form is a row here and an arm of Form and of FormType, whose switches name every kind.
    private static readonly Dictionary<string, FormKind> FormsByName = new(StringComparer.OrdinalIgnoreCase)
    {
        ["COUNT"] = FormKind.Count,
        ["COUNT_BIG"] = FormKind.Count,
        ["ROW_NUMBER"] = FormKind.Ranking,
        ["RANK"] = FormKind.Ranking,
        ["DENSE_RANK"] = FormKind.Ranking,
        ["NTILE"] = FormKind.Ntile,
        ["SUM"] = FormKind.Sum,
        ["MAX"] = FormKind.MaxOrMin,
        ["MIN"] = FormKind.MaxOrMin,
        ["COALESCE"] = FormKind.CoalesceGreatestLeast,
        ["GREATEST"] = FormKind.CoalesceGreatestLeast,
        ["LEAST"] = FormKind.CoalesceGreatestLeast,
        ["CAST"] = FormKind.Cast,
        ["CONVERT"] = FormKind.Convert,
        ["JSON_OBJECT"] = FormKind.Json,
        ["JSON_ARRAY"] = FormKind.Json,
    };

    // The forms, grouped where Form and FormType read the names of a group alike.
    private enum FormKind
    {
        Count,
        Ranking,
        Ntile,
        Sum,
        MaxOrMin,
        CoalesceGreatestLeast,
        Cast,
        Convert,
        Json,
    }

    // "name ( ... ) [WITHIN GROUP ( ... )] [OVER ( ... ) | OVER name]", the name bare.
    private Proof? Call(TokenRange piece, bool rows)
    {
        if (CallOf(piece) is not { } call)
        {
            return null;
        }

        if (call.Kind is not { } kind)
        {
            return Leaf(piece, rows);
        }

        return call.WithinGroup || call.NamedWindow ? Proof.No : Form(kind, call.Arguments, call.Window, rows);
    }

    // The parts of "name ( ... ) [WITHIN GROUP ( ... )] [OVER ( ... ) | OVER name]", or null when the piece is not one.
    private CallParts? CallOf(TokenRange piece)
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
        FormKind? kind =
            name.Kind == TsqlTokenKind.Word && FormsByName.TryGetValue(name.Text, out var form) ? form : null;
        return new CallParts(kind, new TokenRange(open + 1, close), window, withinGroup, namedWindow);
    }

    // The proof of a form, Proof.No when its condition fails.  No arm has a "when", which IDE0072 would count as naming
    // its kind, so that a kind left out fails the build and never reaches the throw.
    private Proof Form(FormKind kind, TokenRange arguments, TokenRange? window, bool rows) =>
        kind switch
        {
            FormKind.Count => Proof.Yes,
            FormKind.Ranking => window is not null && arguments.IsEmpty ? Proof.Yes : Proof.No,
            FormKind.Ntile => window is not null && arguments.Count == 1 && tokens[arguments.Start].IsInteger
                ? Proof.Yes
                : Proof.No,
            FormKind.Sum or FormKind.MaxOrMin => Aggregate(arguments, window, rows),
            FormKind.CoalesceGreatestLeast => window is null
                ? AllOf([
                    AnyOf(Arguments(arguments).Select(argument => Expression(argument, rows))),
                    .. Arguments(arguments).Select(TypeOf),
                ])
                : Proof.No,
            FormKind.Cast => window is null ? Cast(arguments, rows) : Proof.No,
            FormKind.Convert => window is null ? Convert(arguments, rows) : Proof.No,
            FormKind.Json => window is null ? Proof.Yes : Proof.No,
            _ => throw UnexpectedEnumValueException.Of(kind),
        };

    // SUM, MAX or MIN of a value that is not null.  Without OVER, over a group, which always has a row: the operand
    // is in the rows' context.  With OVER, over a frame that holds the current row: the operand keeps the context.
    private Proof Aggregate(TokenRange arguments, TokenRange? window, bool rows)
    {
        if (AggregateOperand(arguments) is not { } operand)
        {
            return Proof.No;
        }

        if (window is { } over)
        {
            return HoldsCurrentRow(over) ? Expression(operand, rows) : Proof.No;
        }

        return body.Grouping == SelectGrouping.None ? Proof.No : Expression(operand, rows: true);
    }

    // The e of SUM, MAX or MIN of e, ALL e or DISTINCT e, or null when there is not one argument.
    private TokenRange? AggregateOperand(TokenRange arguments)
    {
        var operand = arguments;
        if (!operand.IsEmpty && (tokens[operand.Start].Is("ALL") || tokens[operand.Start].Is("DISTINCT")))
        {
            operand = new TokenRange(operand.Start + 1, operand.End);
        }

        return Arguments(operand).Count == 1 ? operand : null;
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
            || ((first.Is("UNBOUNDED") || first.IsInteger) && second.Is(direction));
        return isBound ? at + 2 : -1;
    }

    // CAST(e AS type), the type a built-in one that is no CLR type, and e of no CLR type: a CLR type's conversion to a
    // string is its ToString, which a user's type may answer null.
    private Proof Cast(TokenRange arguments, bool rows) =>
        CastOperand(arguments) is { } operand ? AllOf([Expression(operand, rows), TypeOf(operand)]) : Proof.No;

    // CONVERT(type, e) or CONVERT(type, e, style), the style an integer literal: a NULL style makes it NULL.
    private Proof Convert(TokenRange arguments, bool rows) =>
        ConvertOperand(arguments) is { } operand ? AllOf([Expression(operand, rows), TypeOf(operand)]) : Proof.No;

    // The e of CAST(e AS type), the type a target of CastTargets, or null.
    private TokenRange? CastOperand(TokenRange arguments)
    {
        var asWords = SelectBody.AtLevel(tokens, arguments).Where(index => tokens[index].Is("AS")).ToList();
        return asWords is [var asWord] && CastTargets.IsBuiltIn(tokens, new TokenRange(asWord + 1, arguments.End))
            ? new TokenRange(arguments.Start, asWord)
            : null;
    }

    // The e of CONVERT(type, e) or CONVERT(type, e, style), the type a target of CastTargets and the style an integer
    // literal, or null.
    private TokenRange? ConvertOperand(TokenRange arguments)
    {
        var parts = Arguments(arguments);
        var styleProves =
            parts.Count == 2 || (parts.Count == 3 && parts[2].Count == 1 && tokens[parts[2].Start].IsInteger);
        return styleProves && CastTargets.IsBuiltIn(tokens, parts[0]) ? parts[1] : null;
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

    // A call: its form, or null when it is none; its arguments; its window, "OVER ( ... )"; and whether it has WITHIN
    // GROUP or names a window, "OVER name".
    private readonly record struct CallParts(
        FormKind? Kind,
        TokenRange Arguments,
        TokenRange? Window,
        bool WithinGroup,
        bool NamedWindow
    );
}
