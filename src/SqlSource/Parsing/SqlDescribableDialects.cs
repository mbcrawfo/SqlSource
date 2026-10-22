using System.Collections.Immutable;
using SqlSource.Settings;

namespace SqlSource.Parsing;

/// <summary>
/// The dialects whose database can be asked to describe a query.  An output of <c>Models</c> or <c>CodeGen</c> needs
/// one of them.  The <c>sqlsource</c> tool reads this list, and so does the generator.
/// </summary>
internal static class SqlDescribableDialects
{
    /// <summary>The dialects, in the order a message names them.</summary>
    public static ImmutableArray<SqlDialect> All { get; } =
        ImmutableArray.Create(SqlDialect.PostgreSql, SqlDialect.SqlServer);

    public static bool Contains(SqlDialect dialect) => All.Contains(dialect);

    /// <summary>
    /// The engine whose rules read a file's type override: SQL Server for <see cref="SqlDialect.SqlServer" />, and
    /// PostgreSQL for every other dialect.
    /// </summary>
    public static DatabaseEngine EngineOf(SqlDialect dialect) =>
        dialect == SqlDialect.SqlServer ? DatabaseEngine.SqlServer : DatabaseEngine.PostgreSql;
}
