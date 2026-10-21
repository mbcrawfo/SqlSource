namespace SqlSource.Settings;

/// <summary>
/// A database whose types the type override names, and whose rules read its keys.  <c>Settings/</c> does not use
/// <c>Parsing/</c>, so a dialect's engine is <c>SqlDescribableDialects.EngineOf</c>'s.
/// </summary>
internal enum DatabaseEngine
{
    /// <summary>PostgreSQL, and every dialect that is not SQL Server's.</summary>
    PostgreSql,

    /// <summary>SQL Server.</summary>
    SqlServer,
}
