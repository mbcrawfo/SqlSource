using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Spike10;

// The phase 10.0 spike, finding 3: the vocabulary PostgreSQL itself holds, from pg_get_keywords() and the catalog, of
// the image the run uses.  Run twice, once with SQLSOURCE_TESTS_POSTGRES_IMAGE=postgres:14.24 and once without:
// SPIKE10_OUT=<folder> dotnet test --project tests/DatabaseTests/Postgres -- --filter-class
// "SqlSource.DatabaseTests.Postgres.Spike10.VocabularyTests" --explicit only
[Collection(UsesPostgres.Name)]
public sealed class VocabularyTests(PostgresDatabases databases)
{
    private const string KeywordsSql = "SELECT word, catcode::text, catdesc FROM pg_get_keywords() ORDER BY word";

    private static readonly (string Kind, string Sql)[] CatalogQueries =
    [
        (
            "function",
            "SELECT DISTINCT p.proname FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace"
                + " WHERE n.nspname = 'pg_catalog' ORDER BY 1"
        ),
        (
            "operator",
            "SELECT DISTINCT o.oprname FROM pg_operator o JOIN pg_namespace n ON n.oid = o.oprnamespace"
                + " WHERE n.nspname = 'pg_catalog' ORDER BY 1"
        ),
        (
            "type",
            "SELECT DISTINCT t.typname FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace"
                + " WHERE n.nspname = 'pg_catalog' ORDER BY 1"
        ),
        (
            "information_schema-type",
            "SELECT DISTINCT t.typname FROM pg_type t JOIN pg_namespace n ON n.oid = t.typnamespace"
                + " WHERE n.nspname = 'information_schema' ORDER BY 1"
        ),
    ];

    [Fact(Explicit = true)]
    public async Task Dump_Vocabulary_WritesTheLists()
    {
        var token = TestContext.Current.CancellationToken;
        var root = Environment.GetEnvironmentVariable("SPIKE10_OUT") is { Length: > 0 } variable
            ? variable
            : throw new InvalidOperationException("SPIKE10_OUT is not set.");
        var results = Path.Combine(root, "spike10", "results");
        _ = Directory.CreateDirectory(results);
        var major = databases.Major.ToString(CultureInfo.InvariantCulture);

        await using var source = databases.SourceOf(PostgresDatabases.Database);
        await using var connection = await source.OpenConnectionAsync(token);

        var keywords = new StringBuilder("word\tcatcode\tcatdesc\n");
        foreach (var row in await RowsAsync(connection, KeywordsSql, 3, token))
        {
            _ = keywords.AppendJoin('\t', row).Append('\n');
        }

        await File.WriteAllTextAsync(Path.Combine(results, $"t4-pg-{major}-keywords.tsv"), keywords.ToString(), token);

        var catalog = new StringBuilder("kind\tname\n");
        foreach (var (kind, sql) in CatalogQueries)
        {
            foreach (var row in await RowsAsync(connection, sql, 1, token))
            {
                _ = catalog.Append(kind).Append('\t').Append(row[0]).Append('\n');
            }
        }

        await File.WriteAllTextAsync(Path.Combine(results, $"t4-pg-{major}-catalog.tsv"), catalog.ToString(), token);
        Assert.True(keywords.Length > 100 && catalog.Length > 1000);
    }

    [SuppressMessage(
        "Security",
        "CA2100:Review SQL queries for security vulnerabilities",
        Justification = "The SQL is a constant of this spike."
    )]
    private static async Task<List<string[]>> RowsAsync(
        System.Data.Common.DbConnection connection,
        string sql,
        int columns,
        System.Threading.CancellationToken token
    )
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync(token);
        var rows = new List<string[]>();
        while (await reader.ReadAsync(token))
        {
            rows.Add([.. Enumerable.Range(0, columns).Select(reader.GetString)]);
        }

        return rows;
    }
}
