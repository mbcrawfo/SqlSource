namespace SqlSource.Settings;

/// <summary>
/// A database whose types the type override names, and whose rules read its keys.  <c>Settings/</c> does not use
/// <c>Parsing/</c>, so a dialect's engine is <c>SqlDescribableDialects.EngineOf</c>'s.  Every decision on an engine is
/// a switch expression that names each member; adding one follows <c>docs/architecture/generator.md</c>, "Adding a
/// database engine".
/// </summary>
internal enum DatabaseEngine
{
    /// <summary>PostgreSQL.</summary>
    PostgreSql,

    /// <summary>SQL Server.</summary>
    SqlServer,
}
