using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Realistic;

// The database "realistic" of the corpus: made beside "fixtures", with its schema and its seed applied, and the
// shared database left as it was.
[Collection(UsesPostgres.Name)]
public sealed class RealisticDatabaseTests(PostgresDatabases postgres)
{
    // Every table of schema.sql, a partitioned one by its parent: the partitions are counted apart.
    private const string Tables = """
        SELECT format('%I.%I', n.nspname, c.relname)
        FROM pg_catalog.pg_class c
        JOIN pg_catalog.pg_namespace n ON n.oid = c.relnamespace
        WHERE c.relkind IN ('r', 'p') AND NOT c.relispartition AND n.nspname IN ('public', 'billing')
        ORDER BY 1
        """;

    [Fact]
    public async Task Database_AfterStart_HasRowsInEveryTableAndLeavesTheFixturesAlone()
    {
        var realistic = postgres.RealisticConnectionString;

        var tables = await TablesAsync(realistic);
        var partitions = await RealisticDatabase.ScalarAsync(
            realistic,
            "SELECT count(*) FROM pg_catalog.pg_inherits WHERE inhparent = 'events'::regclass"
        );
        var sharedUntouched = await RealisticDatabase.ScalarAsync(
            postgres.ConnectionString,
            "SELECT to_regclass('public.accounts') IS NULL"
        );

        tables.ShouldContain("billing.invoices");
        foreach (var table in tables)
        {
            var rows = await RealisticDatabase.ScalarAsync(realistic, $"SELECT count(*) FROM {table}");
            ((long)rows!).ShouldBeGreaterThan(0, $"{table} has no rows");
        }

        ((long)partitions!).ShouldBe(2);
        ((bool)sharedUntouched!).ShouldBeTrue();
    }

    private static async Task<List<string>> TablesAsync(string connection)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand(Tables);
        await using var reader = await command.ExecuteReaderAsync(token);
        var tables = new List<string>();
        while (await reader.ReadAsync(token))
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }
}
