namespace SqlSource.Tool.Describing.Postgres.Types;

/// <summary>
/// What a type's modifier sets, decoded by <see cref="TypeModifiers" />, or read from Npgsql's three numbers by
/// <see cref="ColumnFacets" /> for a column of a capture recorded before the modifier was read.  A parameter's own
/// type has none, since the protocol gives a parameter an OID and no modifier; the base of a domain, a parameter's
/// or a column's, has those of the domain's modifier, and the element of an array those of the array's.
/// </summary>
/// <param name="Length">The length of a <c>varchar</c>, a <c>char</c>, a <c>bit</c> or a <c>varbit</c>.</param>
/// <param name="Precision">The precision of a <c>numeric</c>, a time type or an <c>interval</c>.</param>
/// <param name="Scale">The scale of a <c>numeric</c>.  It may be negative.</param>
internal sealed record PostgresFacets(int? Length, int? Precision, int? Scale)
{
    public static PostgresFacets None { get; } = new(null, null, null);
}
