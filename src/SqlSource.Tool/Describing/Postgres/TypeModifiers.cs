namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// Decodes a type modifier, the <c>atttypmod</c> that the server sends with a column, into the facets of the
/// built-in types that carry one.
/// </summary>
/// <remarks>
/// The layout is PostgreSQL's own (<c>varchar.c</c>, <c>numeric.c</c>, <c>timestamp.c</c>, <c>varbit.c</c>) and the
/// tests hold numbers read from a server.  The text of a modifier is the server's: this decodes only the numbers
/// that the sidecar records as <c>length</c>, <c>precision</c> and <c>scale</c>.
/// </remarks>
internal static class TypeModifiers
{
    /// <summary>The modifier of a type that has none, as the protocol and the catalog write it.</summary>
    public const int NoModifier = -1;

    // varchar and bpchar store the length and VARHDRSZ.
    private const int HeaderSize = 4;

    // numeric packs the precision above 16 bits and the scale in the low 11, as a signed number.
    private const int NumericPrecisionMask = 0xFFFF;
    private const int NumericScaleMask = 0x7FF;
    private const int NumericScaleSign = 1024;

    // interval keeps its precision in the low 16 bits, and INTERVAL_FULL_PRECISION in them says that it has none.
    private const int IntervalPrecisionMask = 0xFFFF;

    /// <summary>
    /// The facets of a modifier, by the internal name of the type.  A modifier of <c>-1</c> and a type that carries
    /// none give <see cref="PostgresFacets.None" />.
    /// </summary>
    public static PostgresFacets Decode(string typname, int modifier)
    {
        if (modifier == NoModifier)
        {
            return PostgresFacets.None;
        }

        return typname switch
        {
            "varchar" or "bpchar" => new PostgresFacets(modifier - HeaderSize, null, null),
            "bit" or "varbit" => new PostgresFacets(modifier, null, null),
            "time" or "timetz" or "timestamp" or "timestamptz" => new PostgresFacets(null, modifier, null),
            "numeric" => Numeric(modifier - HeaderSize),
            "interval" => Interval(modifier),
            _ => PostgresFacets.None,
        };
    }

    private static PostgresFacets Numeric(int packed)
    {
        var scale = ((packed & NumericScaleMask) ^ NumericScaleSign) - NumericScaleSign;
        return new PostgresFacets(null, (packed >> 16) & NumericPrecisionMask, scale);
    }

    private static PostgresFacets Interval(int modifier)
    {
        var precision = modifier & IntervalPrecisionMask;
        return precision == IntervalPrecisionMask ? PostgresFacets.None : new PostgresFacets(null, precision, null);
    }
}
