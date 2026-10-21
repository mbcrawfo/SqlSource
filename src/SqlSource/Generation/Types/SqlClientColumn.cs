namespace SqlSource.Generation.Types;

/// <summary>The column of the SQL Server base map that a project's Microsoft.Data.SqlClient chooses.</summary>
internal enum SqlClientColumn
{
    /// <summary>SqlClient 6.1 up to 7, or an older one.</summary>
    SqlClient61,

    /// <summary>SqlClient 7 or later.</summary>
    SqlClient7,

    /// <summary>
    /// No SqlClient is referenced.  A type whose C# type is SqlClient's, such as <c>vector</c>, has none.
    /// </summary>
    None,
}
