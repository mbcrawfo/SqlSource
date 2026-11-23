using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

// COALESCE, which reads an argument twice, and the proof that a value gives one value in a row.
internal sealed partial class ExpressionProofs
{
    // COALESCE(a, b, c) is CASE WHEN a IS NOT NULL THEN a WHEN b IS NOT NULL THEN b ELSE c END, and SQL Server can
    // evaluate each reference apart, so an argument that changes between its test and its result can make it NULL.  An
    // argument proved not null proves the COALESCE only when every argument before it gives one value in a row; those
    // after it are never reached.  Every argument is converted to the result's type, so none may be of a CLR type.
    private Proof Coalesce(TokenRange arguments, bool rows)
    {
        var parts = Arguments(arguments);
        var before = new List<Proof>(parts.Count);
        var alternatives = new List<Proof>(parts.Count);
        foreach (var part in parts)
        {
            alternatives.Add(AllOf([.. before, Expression(part, rows)]));

            var oneValue = OneValue(part);
            if (oneValue is Proof.Never)
            {
                break;
            }

            before.Add(oneValue);
        }

        return AllOf([AnyOf(alternatives), .. parts.Select(TypeOf)]);
    }

    // The proof that two reads of a value in one row agree: arithmetic of pieces that each do.  Anything this does not
    // read may change, whatever a plan measured so far does with it, so it proves nothing.
    private Proof OneValue(TokenRange range) =>
        Pieces(range) is { } pieces ? AllOf(pieces.Select(OneValueOfPiece)) : Proof.No;

    // A literal, NULL or a parameter does.  A column does when it is stored in a table, whose row holds one value; a
    // view's, a computed column and a derived table's expression may be evaluated once for each reference.  A subquery
    // does not, since it can read another row each time.
    private Proof OneValueOfPiece(TokenRange piece)
    {
        var first = tokens[piece.Start];
        if (
            piece.Count == 1
            && (
                first.Kind is TsqlTokenKind.String or TsqlTokenKind.Number or TsqlTokenKind.Parameter
                || first.Is("NULL")
            )
        )
        {
            return Proof.Yes;
        }

        if (first.IsSymbol("("))
        {
            var inner = new TokenRange(piece.Start + 1, piece.End - 1);
            return IsOneGroup(piece) && !inner.IsEmpty && !tokens[inner.Start].Is("SELECT")
                ? OneValue(inner)
                : Proof.No;
        }

        if (!first.IsName || AtTimeZoneOf(piece) is not null)
        {
            return Proof.No;
        }

        var (at, parts) = DottedName(piece);
        if (at == piece.End)
        {
            return StoredLeaf(piece);
        }

        return parts == 1 && tokens[at].IsSymbol("(") ? OneValueOfCall(piece) : Proof.No;
    }

    // A conversion to a built-in type, COALESCE, ISNULL and NULLIF give one value when what they read does, and so does
    // an aggregate without OVER, computed once over its group's rows.  No other call is known to be deterministic.
    private Proof OneValueOfCall(TokenRange piece)
    {
        if (CallOf(piece) is not { Window: null, WithinGroup: false, NamedWindow: false } call)
        {
            return Proof.No;
        }

        var name = tokens[piece.Start];
        if (call.Kind is FormKind.Cast)
        {
            return CastOperand(call.Arguments) is { } operand ? OneValue(operand) : Proof.No;
        }

        if (call.Kind is FormKind.Convert)
        {
            return ConvertOperand(call.Arguments) is { } operand ? OneValue(operand) : Proof.No;
        }

        if (name.Is("SUM") || name.Is("MAX") || name.Is("MIN") || name.Is("AVG"))
        {
            return AggregateOperand(call.Arguments) is { } operand ? OneValue(operand) : Proof.No;
        }

        return name.Is("COALESCE") || name.Is("ISNULL") || name.Is("NULLIF")
            ? AllOf(Arguments(call.Arguments).Select(OneValue))
            : Proof.No;
    }
}
