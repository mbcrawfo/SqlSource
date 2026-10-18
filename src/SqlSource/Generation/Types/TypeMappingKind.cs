namespace SqlSource.Generation.Types;

/// <summary>Whether a PostgreSQL type has a C# type, and if not, why.</summary>
internal enum TypeMappingKind
{
    /// <summary>The type has a C# type.</summary>
    Mapped,

    /// <summary>Npgsql cannot read the type as an object.</summary>
    Unreadable,

    /// <summary>Npgsql cannot read the type as an object, and a cast to <c>oid</c> reads it.</summary>
    CastToOid,

    /// <summary>A range of a subtype the map does not list, or a multirange of such a range.</summary>
    UserDefinedRange,

    /// <summary>With no Npgsql referenced, the type's C# type is of the <c>NpgsqlTypes</c> namespace.</summary>
    NeedsDriver,

    /// <summary>With no Npgsql referenced, the type's C# type differs between Npgsql 8 and 9 and Npgsql 10.</summary>
    DiffersByDriver,

    /// <summary>The type an override names is not found, or several are.</summary>
    MappedTypeNotFound,

    /// <summary>The type an override names is not accessible from the compilation.</summary>
    MappedTypeNotAccessible,

    /// <summary>The type an override names cannot be a property's type.</summary>
    MappedTypeNotAPropertyType,
}
