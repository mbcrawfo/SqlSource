namespace SqlSource.Generation.Types;

/// <summary>Whether a database type has a C# type, and if not, why.</summary>
internal enum TypeMappingKind
{
    /// <summary>The type has a C# type.</summary>
    Mapped,

    /// <summary>
    /// Npgsql cannot read the type as an object, and only the type override can give it a C# type: a composite, or a
    /// base type outside <c>pg_catalog</c> that the map does not know.
    /// </summary>
    Unreadable,

    /// <summary>Npgsql cannot read the type as an object, and a cast to <c>oid</c> reads it.</summary>
    CastToOid,

    /// <summary>
    /// No Npgsql can read the type as an object, and a cast to <c>text</c> reads it: <c>pg_snapshot</c>,
    /// <c>txid_snapshot</c>, <c>pg_node_tree</c>, and a base type of <c>pg_catalog</c> that the map does not know.
    /// </summary>
    CastToText,

    /// <summary>
    /// Npgsql 8 and 9 cannot read the type as an object, and Npgsql 10 can: <c>cube</c>.  A cast to <c>text</c> reads
    /// it on any.
    /// </summary>
    NeedsNpgsql10,

    /// <summary>
    /// A type that the tool never writes in a sidecar: <c>record</c>, <c>unknown</c>, <c>void</c>, a kind this version
    /// does not know, or an array or a domain without its element or base.  Only a sidecar edited by hand, or written
    /// by a newer tool, has one, and describing the query again mends it.
    /// </summary>
    NotWrittenByTheTool,

    /// <summary>A range of a subtype the map does not list, or a multirange of such a range.</summary>
    UserDefinedRange,

    /// <summary>
    /// With no driver referenced, the type's C# type is the driver's: of the <c>NpgsqlTypes</c> namespace for Npgsql,
    /// or of <c>Microsoft.Data.SqlTypes</c> for SqlClient.
    /// </summary>
    NeedsDriver,

    /// <summary>With no Npgsql referenced, the type's C# type differs between Npgsql 8 and 9 and Npgsql 10.</summary>
    DiffersByDriver,

    /// <summary>The type an override names is not found, or several are.</summary>
    MappedTypeNotFound,

    /// <summary>The type an override names is not accessible from the compilation.</summary>
    MappedTypeNotAccessible,

    /// <summary>The type an override names cannot be a property's type.</summary>
    MappedTypeNotAPropertyType,

    /// <summary>
    /// An array outside <c>pg_catalog</c> whose own key, its name in the catalog, has an entry, which maps nothing:
    /// the type override names such an array as its element's key with <c>[]</c>.
    /// </summary>
    ArrayNamedByItsOwnKey,

    /// <summary>
    /// SqlClient reads the type only with the package Microsoft.SqlServer.Types: <c>hierarchyid</c>,
    /// <c>geography</c> and <c>geometry</c>.  <see cref="TypeMapping.Detail" /> is that package's C# type for it.
    /// </summary>
    NeedsSqlServerTypes,

    /// <summary>
    /// The project references a SqlClient older than the first version that has the type's C# type, which is
    /// <see cref="TypeMapping.Detail" />: <c>vector</c> below 6.1.
    /// </summary>
    NeedsNewerSqlClient,
}
