using System.Linq;

namespace SqlSource.Tool.Describing.SqlServer.Proofs;

// The forms that are no call: "a AT TIME ZONE z", and the CASE that replaces a column's NULL.
internal sealed partial class ExpressionProofs
{
    // The value and the zone of the last AT TIME ZONE at the piece's own level, or null when it has none.  AT is no
    // reserved word, so only the three words together are the operator.  The value of a chain holds its earlier links.
    private AtTimeZoneParts? AtTimeZoneOf(TokenRange piece)
    {
        var at = SelectBody
            .AtLevel(tokens, piece)
            .Where(index =>
                index > piece.Start
                && index + 3 < piece.End
                && tokens[index].Is("AT")
                && tokens[index + 1].Is("TIME")
                && tokens[index + 2].Is("ZONE")
            )
            .DefaultIfEmpty(-1)
            .Last();
        return at < 0 ? null : new AtTimeZoneParts(new TokenRange(piece.Start, at), new TokenRange(at + 3, piece.End));
    }

    // NULL only when its value or its zone is: a value out of range and a zone the server does not know are errors.
    // A side that is no primary leaves the whole piece a leaf.  Neither side can be of a CLR type, which the server
    // refuses.
    private Proof? AtTimeZone(AtTimeZoneParts parts, bool rows) =>
        Primary(parts.Value, rows) is { } value && Primary(parts.Zone, rows) is { } zone ? AllOf([value, zone]) : null;

    // The parts of "CASE WHEN x IS NULL THEN e ELSE x END", x a name written the same both times, or null.
    private GuardParts? GuardOf(TokenRange piece)
    {
        var inner = new TokenRange(piece.Start + 1, piece.End - 1);
        if (
            inner.Count < 8
            || !tokens[inner.Start].Is("WHEN")
            || !tokens[inner.Start + 1].IsName
            || tokens[inner.Start + 1].Is("NULL")
        )
        {
            return null;
        }

        var (nameEnd, _) = DottedName(new TokenRange(inner.Start + 1, inner.End));
        if (
            nameEnd + 2 >= inner.End
            || !tokens[nameEnd].Is("IS")
            || !tokens[nameEnd + 1].Is("NULL")
            || !tokens[nameEnd + 2].Is("THEN")
        )
        {
            return null;
        }

        var clauses = SelectBody
            .AtLevel(tokens, inner)
            .Where(index => tokens[index].Is("WHEN") || tokens[index].Is("THEN") || tokens[index].Is("ELSE"))
            .ToList();
        if (clauses is not [_, var then, var otherwise] || then != nameEnd + 2 || !tokens[otherwise].Is("ELSE"))
        {
            return null;
        }

        var column = new TokenRange(inner.Start + 1, nameEnd);
        var fallback = new TokenRange(then + 1, otherwise);
        var repeated = new TokenRange(otherwise + 1, inner.End);
        return !fallback.IsEmpty && IsSameText(column, repeated) ? new GuardParts(column, fallback) : null;
    }

    // Not null when e is, as ISNULL(x, e) is, but the CASE reads x twice, and SQL Server may evaluate an expression
    // once for each reference: x must be a stored column, or be answered not null, for the second read to agree with
    // the first.  The CASE converts both to the type of higher precedence, so neither may be of a CLR type, whose
    // conversion of a string may be its Null.
    private Proof Guarded(GuardParts guard, bool rows) =>
        AllOf([
            Expression(guard.Fallback, rows),
            TypeOf(guard.Fallback),
            TypeLeaf(guard.Column),
            AnyOf([StoredLeaf(guard.Column), Leaf(guard.Column, rows)]),
        ]);

    // A column whose source the server is asked.  Its source is the same in every context, a grouping set's included.
    private Proof.Stored StoredLeaf(TokenRange column)
    {
        _leaves.Add(column);
        return new Proof.Stored(_leaves.Count - 1);
    }

    private readonly record struct AtTimeZoneParts(TokenRange Value, TokenRange Zone);

    private readonly record struct GuardParts(TokenRange Column, TokenRange Fallback);
}
