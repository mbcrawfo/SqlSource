using System;

namespace SqlSource.Parsing;

/// <summary>
/// The names a dialect and its options are set by: in the <c>-- dialect:</c> marker and in the <c>dialect</c> of a
/// settings file.  This is the only place the names are known.
/// </summary>
/// <remarks>
/// A value is the name of a dialect, then any options of that dialect, separated by commas:
/// <c>mysql,ansi-quotes</c>.  The version of a MySQL or MariaDB server is an option too, <c>version=8.0.36</c>, which
/// says which versioned comments the server runs.
/// </remarks>
internal static class SqlDialectName
{
    /// <summary>
    /// The names as a message lists them: <c>SQLSRC019</c> ends with it for a settings file's <c>dialect</c>.
    /// </summary>
    public const string Accepted = "ansi, mssql, postgres, cockroachdb, mysql, mariadb, sqlite and oracle";

    /// <summary>
    /// Each name and alias, with its dialect: the first name of a dialect is its canonical one.  The schema of a
    /// settings file repeats them in its pattern, and a test compares the two.
    /// </summary>
    internal static readonly (string Name, SqlDialect Dialect)[] Names =
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

    /// <summary>
    /// Each name of an option, with the option.  An option is also accepted as the server spells its SQL mode, with
    /// underscores.  The schema of a settings file repeats them in its pattern, and a test compares the two.
    /// </summary>
    internal static readonly (string Name, SqlDialectOptions Option)[] Options =
    [
        ("ansi-quotes", SqlDialectOptions.AnsiQuotes),
        ("ansi_quotes", SqlDialectOptions.AnsiQuotes),
        ("no-backslash-escapes", SqlDialectOptions.NoBackslashEscapes),
        ("no_backslash_escapes", SqlDialectOptions.NoBackslashEscapes),
    ];

    /// <summary>
    /// The word of the option that gives the server's version, before its <c>=</c>: <c>version=8.0.36</c>.  The schema
    /// of a settings file repeats it in its pattern, and a test compares the two.
    /// </summary>
    internal const string VersionOption = "version";

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
        var serverVersion = 0;
        while (comma >= 0)
        {
            rest = rest.Slice(comma + 1);
            comma = rest.IndexOf(',');
            var part = comma < 0 ? rest : rest.Slice(0, comma);
            if (TryReadVersion(part, out var version))
            {
                // The same version twice counts once, as an option does; two versions are no server.
                if (!TakesVersion(dialect) || (serverVersion != 0 && serverVersion != version))
                {
                    return false;
                }

                serverVersion = version;
            }
            else if (!TryFindOption(part, out var option) || (allowed & option) == 0)
            {
                return false;
            }
            else
            {
                options |= option;
            }
        }

        choice = new SqlDialectChoice(dialect, options, serverVersion);
        return true;
    }

    /// <summary>The options that <paramref name="dialect" /> takes; none for a dialect that takes none.</summary>
    internal static SqlDialectOptions OptionsOf(SqlDialect dialect) =>
        dialect is SqlDialect.MySql or SqlDialect.MariaDb
            ? SqlDialectOptions.AnsiQuotes | SqlDialectOptions.NoBackslashEscapes
            : SqlDialectOptions.None;

    /// <summary>Whether <paramref name="dialect" /> takes the option <c>version=</c>.</summary>
    internal static bool TakesVersion(SqlDialect dialect) => dialect is SqlDialect.MySql or SqlDialect.MariaDb;

    // "version=M.m.p", the word ignoring case and white space around the =, each number one or two ASCII digits and the
    // major not zero, as a versioned comment writes it: M * 10000 + m * 100 + p.
    private static bool TryReadVersion(ReadOnlySpan<char> part, out int version)
    {
        version = 0;
        var trimmed = part.Trim();
        if (
            trimmed.Length <= VersionOption.Length
            || !trimmed
                .Slice(0, VersionOption.Length)
                .Equals(VersionOption.AsSpan(), StringComparison.OrdinalIgnoreCase)
        )
        {
            return false;
        }

        var value = trimmed.Slice(VersionOption.Length).TrimStart();
        if (value.IsEmpty || value[0] != '=')
        {
            return false;
        }

        value = value.Slice(1).TrimStart();
        for (var number = 0; number < 3; number++)
        {
            var digits = 0;
            var read = 0;
            while (digits < value.Length && value[digits] is >= '0' and <= '9')
            {
                read = (read * 10) + (value[digits] - '0');
                digits++;
            }

            if (digits is 0 or > 2 || (number < 2 && (digits == value.Length || value[digits] != '.')))
            {
                return false;
            }

            version = (version * 100) + read;
            value = value.Slice(number < 2 ? digits + 1 : digits);
        }

        return value.IsEmpty && version >= 10000;
    }

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
