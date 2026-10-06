namespace SqlSource.Parsing;

/// <summary>
/// The database whose rules decide where comments, strings and quoted identifiers start and end.
/// </summary>
internal enum SqlDialect
{
    /// <summary>
    /// ANSI SQL, plus the quoting forms of PostgreSQL and MySQL that cannot be mistaken for anything else.  The
    /// default.
    /// </summary>
    Ansi,

    /// <summary>SQL Server and Azure SQL.</summary>
    SqlServer,

    /// <summary>PostgreSQL.</summary>
    PostgreSql,

    /// <summary>MySQL.</summary>
    MySql,

    /// <summary>MariaDB.</summary>
    MariaDb,

    /// <summary>SQLite.</summary>
    Sqlite,

    /// <summary>Oracle.</summary>
    Oracle,
}
