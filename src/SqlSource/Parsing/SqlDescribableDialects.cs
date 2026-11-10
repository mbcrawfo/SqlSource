using System.Collections.Immutable;
using SqlSource.Settings;

namespace SqlSource.Parsing;

/// <summary>
/// The dialects whose database can be asked to describe a query.  An output of <c>Models</c> or <c>Methods</c> needs
/// one of them.  The <c>sqlsource</c> tool reads this list, and so does the generator.
/// </summary>
internal static class SqlDescribableDialects
{
    /// <summary>The dialects, in the order a message names them.</summary>
    public static ImmutableArray<SqlDialect> All { get; } =
        ImmutableArray.Create(SqlDialect.PostgreSql, SqlDialect.SqlServer);

    public static bool Contains(SqlDialect dialect) => All.Contains(dialect);

    /// <summary>
    /// The describable dialect of the engine, the inverse of <see cref="EngineOf" /> on <see cref="All" />.
    /// </summary>
    public static SqlDialect DialectOf(DatabaseEngine engine) =>
        engine switch
        {
            DatabaseEngine.PostgreSql => SqlDialect.PostgreSql,
            DatabaseEngine.SqlServer => SqlDialect.SqlServer,
            _ => throw UnexpectedEnumValueException.Of(engine),
        };

    /// <summary>
    /// The engine whose rules read a file's type override: SQL Server for <see cref="SqlDialect.SqlServer" />, and
    /// PostgreSQL for every other dialect.
    /// </summary>
    public static DatabaseEngine EngineOf(SqlDialect dialect) =>
        dialect switch
        {
            SqlDialect.SqlServer => DatabaseEngine.SqlServer,
            SqlDialect.PostgreSql => DatabaseEngine.PostgreSql,
            // These cannot be described, so only a marker's type override reads their keys, by PostgreSQL's rules.  A
            // dialect that becomes describable gets an engine of its own: see docs/architecture/generator.md, "Adding
            // a database engine".
            SqlDialect.Ansi
            or SqlDialect.MySql
            or SqlDialect.MariaDb
            or SqlDialect.Sqlite
            or SqlDialect.Oracle
            or SqlDialect.CockroachDb => DatabaseEngine.PostgreSql,
            _ => throw UnexpectedEnumValueException.Of(dialect),
        };
}
