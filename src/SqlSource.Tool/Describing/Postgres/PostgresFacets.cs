namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What a type's modifier sets.  A parameter has none: the protocol gives a parameter an OID and no modifier.  A
/// column's come with the columns.
/// </summary>
/// <param name="Length">The length of a <c>varchar</c>, a <c>char</c>, a <c>bit</c> or a <c>varbit</c>.</param>
/// <param name="Precision">The precision of a <c>numeric</c>, a time type or an <c>interval</c>.</param>
/// <param name="Scale">The scale of a <c>numeric</c>.  It may be negative.</param>
internal sealed record PostgresFacets(int? Length, int? Precision, int? Scale)
{
    public static PostgresFacets None { get; } = new(null, null, null);
}
