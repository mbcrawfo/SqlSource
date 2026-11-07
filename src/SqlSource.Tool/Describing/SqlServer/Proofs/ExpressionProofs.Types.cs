using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

// The type walk: the proof that a value is of no CLR type, which COALESCE, GREATEST, LEAST, CAST and CONVERT need of
// their arguments (ExpressionProofs.Calls.cs).
internal sealed partial class ExpressionProofs
{
    // The proof that a value is of no CLR type, from its form where that tells, by asking the server otherwise.  A
    // multi-piece expression is arithmetic, which no CLR type has, but each piece is still held to be of none.
    private Proof TypeOf(TokenRange range)
    {
        if (Pieces(range) is not { } pieces)
        {
            return TypeLeaf(range);
        }

        var proofs = new List<Proof>(pieces.Count);
        foreach (var piece in pieces)
        {
            if (PrimaryType(piece) is not { } proof)
            {
                return TypeLeaf(range);
            }

            proofs.Add(proof);
        }

        return AllOf(proofs);
    }

    // The type proof of a piece that is a primary, or null when it is none.  A literal, NULL, a count subquery and the
    // forms whose type is fixed are of no CLR type; MAX and MIN are of their operand's type, COALESCE, GREATEST and
    // LEAST of one of their arguments', and CAST and CONVERT of their target, which proves only when it is built in.
    // A call of a schema's name may return a CLR type and cannot be asked.  Anything else is asked.
    private Proof? PrimaryType(TokenRange piece)
    {
        var first = tokens[piece.Start];
        if (piece.Count == 1 && (first.Kind is TsqlTokenKind.String or TsqlTokenKind.Number || first.Is("NULL")))
        {
            return Proof.Yes;
        }

        if (first.Is("CASE"))
        {
            return IsOneGroup(piece) ? TypeLeaf(piece) : null;
        }

        if (first.IsSymbol("("))
        {
            if (!IsOneGroup(piece))
            {
                return null;
            }

            var inner = new TokenRange(piece.Start + 1, piece.End - 1);
            if (inner.IsEmpty)
            {
                return Proof.No;
            }

            if (tokens[inner.Start].Is("SELECT"))
            {
                return IsOneCount(inner) ? Proof.Yes : TypeLeaf(piece);
            }

            return TypeOf(inner);
        }

        if (!first.IsName)
        {
            return piece.Count == 1 && first.Kind == TsqlTokenKind.Parameter ? TypeLeaf(piece) : null;
        }

        var (at, parts) = DottedName(piece);
        if (at == piece.End)
        {
            return TypeLeaf(piece);
        }

        if (!tokens[at].IsSymbol("("))
        {
            return null;
        }

        if (parts != 1 || CallOf(piece) is not { } call)
        {
            return parts == 1 ? null : Proof.No;
        }

        return call.Kind is { } kind && !call.WithinGroup && !call.NamedWindow
            ? FormType(kind, call.Arguments)
            : TypeLeaf(piece);
    }

    // The type proof of a form, read from the same table of forms as its proof (Form).
    private Proof FormType(FormKind kind, TokenRange arguments) =>
        kind switch
        {
            FormKind.Count or FormKind.Ranking or FormKind.Ntile or FormKind.Sum => Proof.Yes,
            FormKind.Json => Proof.Yes,
            FormKind.MaxOrMin => AggregateOperand(arguments) is { } operand ? TypeOf(operand) : Proof.No,
            FormKind.CoalesceGreatestLeast => AllOf(Arguments(arguments).Select(TypeOf)),
            FormKind.Cast => CastOperand(arguments) is null ? Proof.No : Proof.Yes,
            FormKind.Convert => CastTargets.IsBuiltIn(tokens, Arguments(arguments)[0]) ? Proof.Yes : Proof.No,
            _ => throw UnexpectedEnumValueException.Of(kind),
        };

    // A value whose type the server is asked.  Its type is the same in every context, a grouping set's included, and a
    // parameter's is its declaration, which "operands" is asked with; one that holds what is never asked is no leaf.
    private Proof TypeLeaf(TokenRange range)
    {
        if (range.IsEmpty || HoldsWhatIsNeverAsked(range))
        {
            return Proof.No;
        }

        _leaves.Add(range);
        return new Proof.NotClr(_leaves.Count - 1);
    }
}
