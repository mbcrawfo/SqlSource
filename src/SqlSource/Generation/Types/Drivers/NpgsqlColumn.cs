namespace SqlSource.Generation.Types.Drivers;

/// <summary>The column of the PostgreSQL base map that a project's Npgsql chooses.</summary>
internal enum NpgsqlColumn
{
    /// <summary>Npgsql 8 or 9, which box the same type for every type, or an Npgsql older than 8.</summary>
    Npgsql8,

    /// <summary>Npgsql 10 or later.</summary>
    Npgsql10,

    /// <summary>
    /// No Npgsql is referenced.  A type whose C# type differs between the drivers, or that is of the
    /// <c>NpgsqlTypes</c> namespace, has none.
    /// </summary>
    None,
}
