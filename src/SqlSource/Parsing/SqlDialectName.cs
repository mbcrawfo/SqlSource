using System;

namespace SqlSource.Parsing;

/// <summary>
/// The names a dialect is set by: in the <c>dialect=</c> directive, in the <c>SqlSourceDialect</c> MSBuild property
/// and in the metadata of the same name.  This is the only place the names are known.
/// </summary>
internal static class SqlDialectName
{
    /// <summary>
    /// The names as a message lists them.  The text of <c>SQLSRC011</c> repeats this list, and a test compares the two.
    /// </summary>
    public const string Accepted = "ansi, mssql, postgres, mysql, mariadb, sqlite and oracle";

    private static readonly (string Name, SqlDialect Dialect)[] Names =
    [
        ("ansi", SqlDialect.Ansi),
        ("mssql", SqlDialect.SqlServer),
        ("sqlserver", SqlDialect.SqlServer),
        ("tsql", SqlDialect.SqlServer),
        ("postgres", SqlDialect.PostgreSql),
        ("postgresql", SqlDialect.PostgreSql),
        ("mysql", SqlDialect.MySql),
        ("mariadb", SqlDialect.MariaDb),
        ("sqlite", SqlDialect.Sqlite),
        ("oracle", SqlDialect.Oracle),
    ];

    /// <summary>
    /// Reads a name or an alias, ignoring case and surrounding whitespace.  False for anything else, and for null.
    /// </summary>
    public static bool TryParse(string? value, out SqlDialectChoice choice) => TryParse(value.AsSpan(), out choice);

    /// <inheritdoc cref="TryParse(string?, out SqlDialectChoice)" />
    public static bool TryParse(ReadOnlySpan<char> value, out SqlDialectChoice choice)
    {
        var name = value.Trim();
        foreach (var (candidate, candidateDialect) in Names)
        {
            if (name.Equals(candidate.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                choice = candidateDialect;
                return true;
            }
        }

        choice = default;
        return false;
    }
}
