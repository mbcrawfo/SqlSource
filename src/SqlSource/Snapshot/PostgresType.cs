namespace SqlSource.Snapshot;

/// <summary>A PostgreSQL type.</summary>
/// <param name="Name">The spelled type, facets included.</param>
/// <param name="Kind">
/// The kind as written.  It may be one this version does not know: read it with <see cref="TryGetKind" />.
/// </param>
/// <param name="Schema">The type's schema.</param>
/// <param name="InternalName">The type's name in the catalog.</param>
/// <param name="Length">The length the modifier sets, or null.</param>
/// <param name="Precision">The precision the modifier sets, or null.</param>
/// <param name="Scale">The scale the modifier sets, or null.  It may be negative.</param>
/// <param name="Element">The element type of an array.</param>
/// <param name="Base">The base type of a domain.</param>
/// <param name="Labels">The labels of an enum, in order.</param>
/// <param name="Subtype">The element type of a range or a multirange.</param>
internal sealed record PostgresType(
    string Name,
    string Kind,
    string Schema,
    string InternalName,
    int? Length,
    int? Precision,
    int? Scale,
    PostgresType? Element,
    PostgresType? Base,
    EquatableArray<string>? Labels,
    PostgresType? Subtype
) : SidecarType(Name)
{
    /// <summary>
    /// Reads <see cref="Kind" />.  Returns false for a value that is not one of the format's seven, and never throws.
    /// </summary>
    public bool TryGetKind(out PostgresTypeKind kind)
    {
        var found = Kind switch
        {
            SidecarValues.Kind.Base => PostgresTypeKind.Base,
            SidecarValues.Kind.Array => PostgresTypeKind.Array,
            SidecarValues.Kind.Domain => PostgresTypeKind.Domain,
            SidecarValues.Kind.Enum => PostgresTypeKind.Enum,
            SidecarValues.Kind.Range => PostgresTypeKind.Range,
            SidecarValues.Kind.Multirange => PostgresTypeKind.Multirange,
            SidecarValues.Kind.Composite => PostgresTypeKind.Composite,
            _ => (PostgresTypeKind?)null,
        };
        kind = found ?? default;
        return found is not null;
    }
}
