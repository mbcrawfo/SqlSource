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
/// A CLR type's <c>Parse</c> may answer its Null for a string, as geometry's does for <c>'NULL'</c>.  So <c>CAST</c>
/// and <c>CONVERT</c> prove only to a target of <see cref="CastTargets" />, and <c>COALESCE</c>, <c>GREATEST</c> and
/// <c>LEAST</c>, which convert every argument to the type of highest precedence, a CLR type's when one has it, only
/// when no argument is of a CLR type.  An argument's type is known from its form, or asked of the server as a
/// <see cref="Proof.NotClr" /> leaf, which holds in every context, a grouping set's included, and of a parameter too.
/// </para>
/// <para>
/// An operand is numbered only when the item's proof still needs it.  The forms assume <c>ANSI_WARNINGS</c> on, under
/// which an overflow is an error and not a <c>NULL</c>.
/// </para>
/// </remarks>
internal sealed partial class ExpressionProofs(IReadOnlyList<TsqlToken> tokens, SelectBody body)
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

    private readonly List<TokenRange> _operands = [];

    // The leaves of the item being proved, which a Proof.Operand indexes until Of numbers the ones its proof keeps.
    private readonly List<TokenRange> _leaves = [];

    /// <summary>
    /// The operands to ask, each text once, in the order the items' proofs first use them; a
    /// <see cref="Proof.Operand" /> indexes it.
    /// </summary>
    public IReadOnlyList<TokenRange> Operands => _operands;

    /// <summary>The proof of an item's expression, in the select's context.</summary>
    /// <remarks>
    /// Under <c>GROUP BY ALL</c>, which returns a group that no row holds, nothing is proved.  An item that is one leaf
    /// alone, its outer parentheses aside, proves nothing and asks nothing: the server has already answered that very
    /// expression over the same rows.  A lone leaf inside a form, as in <c>CAST(x AS int)</c>, is still asked, since
    /// the form's answer can differ from its operand's.
    /// </remarks>
    public Proof Of(TokenRange expression)
    {
        if (body.Grouping == SelectGrouping.All)
        {
            return Proof.No;
        }

        _leaves.Clear();
        var proof = Expression(expression, rows: false);
        return proof is Proof.Operand leaf && IsTheWholeItem(_leaves[leaf.Index], expression)
            ? Proof.No
            : Numbered(proof);
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

    // The proof with each leaf it kept numbered as an operand, a text asked before taking its number.
    private Proof Numbered(Proof proof) =>
        proof switch
        {
            Proof.Operand leaf => new Proof.Operand(OperandIndex(_leaves[leaf.Index])),
            Proof.NotClr leaf => new Proof.NotClr(OperandIndex(_leaves[leaf.Index])),
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

        var (at, parts) = DottedName(piece);
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

    // The end of the name that starts the piece, "a" or "a.b. ...", and the number of its parts.
    private (int End, int Parts) DottedName(TokenRange piece)
    {
        var at = piece.Start + 1;
        var parts = 1;
        while (at + 1 < piece.End && tokens[at].IsSymbol(".") && tokens[at + 1].IsName)
        {
            at += 2;
            parts++;
        }

        return (at, parts);
    }

    // True when the leaf is the item's expression, or that expression with one or more outer parentheses removed.
    private bool IsTheWholeItem(TokenRange leaf, TokenRange expression)
    {
        var range = expression;
        while (true)
        {
            if (leaf == range)
            {
                return true;
            }

            if (range.Count < 2 || !tokens[range.Start].IsSymbol("(") || !IsOneGroup(range))
            {
                return false;
            }

            range = new TokenRange(range.Start + 1, range.End - 1);
        }
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
    private Proof Subquery(TokenRange piece, TokenRange inner, bool rows) =>
        IsOneCount(inner) ? Proof.Yes : Leaf(piece, rows);

    private bool IsOneCount(TokenRange inner) =>
        SelectBody.Read(tokens, inner, [])
            is {
                Grouping: SelectGrouping.None,
                HasHaving: false,
                HasTop: false,
                HasOffset: false,
                Items: [{ IsStar: false } item],
            }
        && IsCountAlone(item.Expression);

    // COUNT( ... ) or COUNT_BIG( ... ), with no OVER.
    private bool IsCountAlone(TokenRange expression) =>
        expression.Count >= 3
        && (tokens[expression.Start].Is("COUNT") || tokens[expression.Start].Is("COUNT_BIG"))
        && tokens[expression.Start + 1].IsSymbol("(")
        && SelectBody.ClosingParenthesis(tokens, expression.Start + 1, expression.End) == expression.End - 1;

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
