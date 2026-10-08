using System;

namespace SqlSource.Parsing;

/// <summary>
/// The names a dialect and its options are set by: in the <c>-- dialect:</c> marker, in the <c>SqlSourceDialect</c>
/// MSBuild property and in the metadata of the same name.  This is the only place the names are known.
/// </summary>
/// <remarks>
/// A value is the name of a dialect, then any options of that dialect, separated by commas:
/// <c>mysql,ansi-quotes</c>.
/// </remarks>
internal static class SqlDialectName
{
    /// <summary>
    /// The names as a message lists them.  The text of <c>SQLSRC011</c> repeats this list, and a test compares the two.
    /// </summary>
    public const string Accepted = "ansi, mssql, postgres, cockroachdb, mysql, mariadb, sqlite and oracle";

    private static readonly (string Name, SqlDialect Dialect)[] Names =
    [
        ("ansi", SqlDialect.Ansi),
        ("mssql", SqlDialect.SqlServer),
        ("sqlserver", SqlDialect.SqlServer),
        ("tsql", SqlDialect.SqlServer),
        ("postgres", SqlDialect.PostgreSql),
        ("postgresql", SqlDialect.PostgreSql),
        ("cockroachdb", SqlDialect.CockroachDb),
        ("cockroach", SqlDialect.CockroachDb),
        ("mysql", SqlDialect.MySql),
        ("mariadb", SqlDialect.MariaDb),
        ("sqlite", SqlDialect.Sqlite),
        ("oracle", SqlDialect.Oracle),
    ];

    // An option is also accepted as the server spells its SQL mode, with underscores.
    private static readonly (string Name, SqlDialectOptions Option)[] Options =
    [
        ("ansi-quotes", SqlDialectOptions.AnsiQuotes),
        ("ansi_quotes", SqlDialectOptions.AnsiQuotes),
        ("no-backslash-escapes", SqlDialectOptions.NoBackslashEscapes),
        ("no_backslash_escapes", SqlDialectOptions.NoBackslashEscapes),
    ];

    /// <summary>
    /// The name that stands for <paramref name="dialect" /> wherever one name is needed: its first in the list.
    /// </summary>
    public static string Canonical(SqlDialect dialect)
    {
        foreach (var (name, candidate) in Names)
        {
            if (candidate == dialect)
            {
                return name;
            }
        }

        return Names[0].Name;
    }

    /// <summary>
    /// Reads a name or an alias and the options after it, ignoring case and the whitespace around each part.  False
    /// for anything else, and for null: a name that is not a dialect, an option that does not exist or that the
    /// dialect does not have, and an empty part.
    /// </summary>
    public static bool TryParse(string? value, out SqlDialectChoice choice) => TryParse(value.AsSpan(), out choice);

    /// <inheritdoc cref="TryParse(string?, out SqlDialectChoice)" />
    public static bool TryParse(ReadOnlySpan<char> value, out SqlDialectChoice choice)
    {
        choice = default;
        var rest = value;
        var comma = rest.IndexOf(',');
        if (!TryFindDialect(comma < 0 ? rest : rest.Slice(0, comma), out var dialect))
        {
            return false;
        }

        var allowed = OptionsOf(dialect);
        var options = SqlDialectOptions.None;
        while (comma >= 0)
        {
            rest = rest.Slice(comma + 1);
            comma = rest.IndexOf(',');
            if (!TryFindOption(comma < 0 ? rest : rest.Slice(0, comma), out var option) || (allowed & option) == 0)
            {
                return false;
            }

            options |= option;
        }

        choice = new SqlDialectChoice(dialect, options);
        return true;
    }

    private static SqlDialectOptions OptionsOf(SqlDialect dialect) =>
        dialect is SqlDialect.MySql or SqlDialect.MariaDb
            ? SqlDialectOptions.AnsiQuotes | SqlDialectOptions.NoBackslashEscapes
            : SqlDialectOptions.None;

    private static bool TryFindDialect(ReadOnlySpan<char> part, out SqlDialect dialect)
    {
        var name = part.Trim();
        foreach (var (candidate, candidateDialect) in Names)
        {
            if (name.Equals(candidate.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                dialect = candidateDialect;
                return true;
            }
        }

        dialect = SqlDialect.Ansi;
        return false;
    }

    private static bool TryFindOption(ReadOnlySpan<char> part, out SqlDialectOptions option)
    {
        var name = part.Trim();
        foreach (var (candidate, candidateOption) in Options)
        {
            if (name.Equals(candidate.AsSpan(), StringComparison.OrdinalIgnoreCase))
            {
                option = candidateOption;
                return true;
            }
        }

        option = SqlDialectOptions.None;
        return false;
    }
}
