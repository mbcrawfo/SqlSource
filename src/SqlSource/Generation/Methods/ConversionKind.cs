namespace SqlSource.Generation.Methods;

/// <summary>
/// How a <see cref="Conversion" /> converts between the property's type and the database's.  The kinds exclude each
/// other; <see cref="Conversion.IsByElement" /> and <see cref="Conversion.MayReturnNull" /> combine with a kind.  Each
/// switch on it names every member with no discard, so a new kind fails the build where it must be handled.
/// </summary>
internal enum ConversionKind
{
    /// <summary>A converter's call, <c>Method(value)</c>: a project's, a mapped type's or a library's.</summary>
    Converter,

    /// <summary>
    /// System.Text.Json's serializer, <c>JsonSerializer.Deserialize&lt;T&gt;</c> or
    /// <c>JsonSerializer.Serialize&lt;T&gt;</c> with its default options, for a project's own class in a type that
    /// holds JSON, rather than a converter.  It is called as a converter is, and has no
    /// <see cref="Conversion.Library" />: no class is emitted for it.
    /// </summary>
    Serializer,

    /// <summary>
    /// SQL Server's spatial read path, NetTopologySuite's: <see cref="Conversion.Method" /> is then the shared reader
    /// or writer of NetTopologySuite.IO.SqlServerBytes that the method calls,
    /// <c>global::SqlSource.NetTopologySuiteConversions.GeographyReader</c>, a column is read through its
    /// <c>GetStream</c>, and <see cref="Conversion.DatabaseText" /> is <c>global::System.Data.SqlTypes.SqlBytes</c>,
    /// which a parameter is bound as.
    /// </summary>
    Spatial,
}
