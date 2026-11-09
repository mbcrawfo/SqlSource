using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Bundles.Renaming.Postgres;

namespace SqlSource.DatabaseTests.Postgres.Renaming;

// The words of a PostgreSQL server that the renaming's vocabulary holds, read by the queries the vocabulary is made
// with.
internal static class PostgresCatalogWords
{
    // The five sources of the vocabulary, each read whole.  A keyword is folded as unquoted, as the user writes one,
    // and a catalog's name is kept as the catalog holds it.
    public static readonly IReadOnlyList<(string Sql, bool Quoted)> All =
    [
        ("SELECT word FROM pg_catalog.pg_get_keywords()", false),
        (
            "SELECT DISTINCT p.proname FROM pg_catalog.pg_proc p"
                + " JOIN pg_catalog.pg_namespace n ON n.oid = p.pronamespace WHERE n.nspname = 'pg_catalog'",
            true
        ),
        (
            "SELECT DISTINCT o.oprname FROM pg_catalog.pg_operator o"
                + " JOIN pg_catalog.pg_namespace n ON n.oid = o.oprnamespace WHERE n.nspname = 'pg_catalog'",
            true
        ),
        (
            "SELECT DISTINCT t.typname FROM pg_catalog.pg_type t"
                + " JOIN pg_catalog.pg_namespace n ON n.oid = t.typnamespace"
                + " WHERE n.nspname IN ('pg_catalog', 'information_schema')",
            true
        ),
        (
            "SELECT a.attname FROM pg_catalog.pg_attribute a"
                + " WHERE a.attrelid = 'pg_catalog.pg_class'::pg_catalog.regclass AND a.attnum < 0",
            true
        ),
    ];

    // The subset a name of the user's may not be: its keywords, functions and types.  An operator is no name, and a
    // table's column may not take a system column's name, so neither is read.
    public static readonly IReadOnlyList<(string Sql, bool Quoted)> Names = [All[0], All[1], All[3]];

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a constant of this test."
    )]
    public static async Task<SortedSet<string>> ReadAsync(
        DbDataSource source,
        IEnumerable<(string Sql, bool Quoted)> sources,
        CancellationToken token
    )
    {
        await using var connection = await source.OpenConnectionAsync(token);
        var words = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var (sql, quoted) in sources)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            await using var reader = await command.ExecuteReaderAsync(token);
            while (await reader.ReadAsync(token))
            {
                _ = words.Add(PostgresWords.Fold(reader.GetString(0), quoted));
            }
        }

        return words;
    }
}
