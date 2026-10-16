using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

// The database "realistic" of the corpus: made beside "fixtures", with its schema and its seed applied, and the
// shared database left as it was; and the harness's work that needs it, the oracle's run that leaves the seed as it
// was and a case described and rendered.
[Collection(UsesPostgres.Name)]
public sealed class RealisticDatabaseTests(PostgresContainer postgres)
{
    private const string FirstCase = "aggregate-cte-left-joined";

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
        var partitions = await RealisticRun.ScalarAsync(
            realistic,
            "SELECT count(*) FROM pg_catalog.pg_inherits WHERE inhparent = 'events'::regclass"
        );
        var sharedUntouched = await RealisticRun.ScalarAsync(
            postgres.ConnectionString,
            "SELECT to_regclass('public.accounts') IS NULL"
        );

        tables.ShouldContain("billing.invoices");
        foreach (var table in tables)
        {
            var rows = await RealisticRun.ScalarAsync(realistic, $"SELECT count(*) FROM {table}");
            ((long)rows!).ShouldBeGreaterThan(0, $"{table} has no rows");
        }

        ((long)partitions!).ShouldBe(2);
        ((bool)sharedUntouched!).ShouldBeTrue();
    }

    [Fact]
    public async Task RunAsync_InsertReturning_LeavesTheTableAsItWas()
    {
        const string Insert =
            "INSERT INTO reservations (account_id, during) SELECT id, '[2024-01-01,2024-01-02)' FROM accounts "
            + "RETURNING id";
        var connection = postgres.RealisticConnectionString;
        var before = (long)(await RealisticRun.ScalarAsync(connection, "SELECT count(*) FROM reservations"))!;
        var accounts = (long)(await RealisticRun.ScalarAsync(connection, "SELECT count(*) FROM accounts"))!;

        var first = await RealisticOracle.RunAsync(connection, Insert);
        var second = await RealisticOracle.RunAsync(connection, Insert);

        ((long)(await RealisticRun.ScalarAsync(connection, "SELECT count(*) FROM reservations"))!).ShouldBe(before);
        first.Rows.ShouldBe((int)accounts);
        second.Rows.ShouldBe((int)accounts);
        first.Names.ShouldBe(["id"]);
        first.SawNull.ShouldBe([false]);
    }

    [Fact]
    public async Task RunAsync_NullableColumn_SawNullWhereARowHasOne()
    {
        var run = await RealisticOracle.RunAsync(
            postgres.RealisticConnectionString,
            "SELECT id, email FROM accounts ORDER BY id"
        );

        run.Names.ShouldBe(["id", "email"]);
        run.SawNull.ShouldBe([false, true]);
    }

    // The rollback of a connection that broke fails too, and the query's own error is what says why.
    [Fact]
    public async Task RunAsync_QueryThatEndsItsConnection_ThrowsTheQuerysError()
    {
        var thrown = await Should.ThrowAsync<PostgresException>(() =>
            RealisticOracle.RunAsync(
                postgres.RealisticConnectionString,
                "SELECT pg_terminate_backend(pg_backend_pid())"
            )
        );

        thrown.SqlState.ShouldBe(PostgresErrorCodes.AdminShutdown);
    }

    [Fact]
    public async Task DescribeAsync_FirstCase_IsDescribedAndRendered()
    {
        var found = RealisticCases.Read(Path.Combine(RepositoryPaths.RealisticCases, FirstCase));

        var described = await RealisticRun.DescribeAsync(found, postgres.RealisticConnectionString);

        described.Failure.ShouldBeNull();
        RealisticRun.Render(described).ShouldStartWith("kind: rows\n");
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
