using System.Collections.Generic;
using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

// COALESCE, which reads an argument twice, ISNULL, which reads each once, and the proof that a value gives one value
// in a row.
internal sealed partial class ExpressionProofs
{
    // COALESCE(a, b, c) is CASE WHEN a IS NOT NULL THEN a WHEN b IS NOT NULL THEN b ELSE c END, and SQL Server can
    // evaluate each reference apart, so an argument that changes between its test and its result can make it NULL.  An
    // argument proved not null proves the COALESCE only when every argument before it gives one value in a row; those
    // after it are never reached.  Every argument is converted to the result's type, so none may be of a CLR type.
    //
    // Every row of a FULL JOIN has its left side or its right side, whatever its ON.  So a column of one side, an
    // earlier argument, and a column of the other, a later one, prove the COALESCE together when each is not null over
    // its side's rows alone and the server names each as a stored column, which an absent side makes NULL and not a
    // property of another source's CLR column: in a row of the first's side the first is reached and not null, and in
    // any other the second is.  The arguments before the second must give one value, and so must the second unless it
    // is the last, which is read once.
    private Proof Coalesce(TokenRange arguments, bool rows)
    {
        var parts = Arguments(arguments);
        var sides = parts.Select(part => filtered?.SideOf(part)).ToList();
        var before = new List<Proof>(parts.Count);
        var alternatives = new List<Proof>(parts.Count);
        for (var at = 0; at < parts.Count; at++)
        {
            var part = parts[at];
            alternatives.Add(AllOf([.. before, Expression(part, rows)]));

            var oneValue = OneValue(part);
            if (sides[at] is { } side)
            {
                var read = at == parts.Count - 1 ? Proof.Yes : oneValue;
                for (var earlier = 0; earlier < at; earlier++)
                {
                    if (sides[earlier] is { } other && other != side)
                    {
                        alternatives.Add(
                            AllOf([
                                .. before,
                                read,
                                SideLeaf(parts[earlier], other, rows),
                                SideLeaf(part, side, rows),
                                StoredLeaf(part),
                            ])
                        );
                    }
                }
            }

            if (oneValue is Proof.Never)
            {
                break;
            }

            before.Add(oneValue);
        }

        return AllOf([AnyOf(alternatives), .. parts.Select(TypeOf)]);
    }

    // ISNULL(a, b) is a when a is not null, and b converted to a's type otherwise.  It reads a once, as the
    // documentation of COALESCE says, so one proved argument proves it, whatever the other gives.  It converts b to a's
    // type, and is of b's type when a is NULL, so neither may be of a CLR type.
    //
    // A column of each side of the last FULL JOIN prove it together, as for COALESCE, with no argument read twice: in a
    // row of a's side a is not null, and in any other b is.  Each must be stored, which an absent side makes NULL and
    // not a property of another source's CLR column.
    private Proof IsNull(TokenRange arguments, bool rows)
    {
        if (Arguments(arguments) is not [var value, var replacement])
        {
            return Proof.No;
        }

        var alternatives = new List<Proof> { Expression(value, rows), Expression(replacement, rows) };
        if (filtered?.SideOf(value) is { } side && filtered.SideOf(replacement) is { } other && side != other)
        {
            alternatives.Add(
                AllOf([
                    SideLeaf(value, side, rows),
                    SideLeaf(replacement, other, rows),
                    StoredLeaf(value),
                    StoredLeaf(replacement),
                ])
            );
        }

        return AllOf([AnyOf(alternatives), TypeOf(value), TypeOf(replacement)]);
    }

    // The proof that two reads of a value in one row agree: arithmetic of pieces that each do.  Anything this does not
    // read may change, whatever a plan measured so far does with it, so it proves nothing.
    private Proof OneValue(TokenRange range) =>
        Pieces(range) is { } pieces ? AllOf(pieces.Select(OneValueOfPiece)) : Proof.No;

    // A literal, NULL or a parameter does.  A column does when it is stored in a table of the session's database, the
    // only one whose source type "columns" reads, and whose row holds one value; a view's, a computed column and a
    // derived table's expression may be evaluated once for each reference.  A subquery does not, since it can read
    // another row each time.
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
    // an aggregate without OVER, computed once over its group's rows.  AVG has no type proof (PrimaryType), so it
    // counts only inside a CAST or CONVERT, whose type is its target.  No other call is known to be deterministic.
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
