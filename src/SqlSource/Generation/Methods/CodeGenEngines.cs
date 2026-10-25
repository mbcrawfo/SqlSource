using System;

namespace SqlSource.Generation.Methods;

/// <summary>
/// The engines that a type has a query of whose output is <c>CodeGen</c>, which need their driver (<c>SQLSRC273</c>).
/// </summary>
[Flags]
internal enum CodeGenEngines
{
    /// <summary>No engine.</summary>
    None = 0,

    /// <summary>A query of the <c>postgres</c> dialect, which needs Npgsql.</summary>
    Postgres = 1,

    /// <summary>A query of the <c>mssql</c> dialect, which needs Microsoft.Data.SqlClient.</summary>
    SqlServer = 2,
}
