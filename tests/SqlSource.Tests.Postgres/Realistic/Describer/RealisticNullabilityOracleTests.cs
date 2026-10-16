using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic.Describer;

// The oracle's runs against the server: a query that writes leaves the seed as it was, a column that held a NULL in a
// row is seen, and a query that ends its own connection fails with its own error.
[Collection(UsesPostgres.Name)]
public sealed class RealisticNullabilityOracleTests(PostgresContainer postgres)
{
    [Fact]
    public async Task RunAsync_InsertReturning_LeavesTheTableAsItWas()
    {
        const string Insert =
            "INSERT INTO reservations (account_id, during) SELECT id, '[2024-01-01,2024-01-02)' FROM accounts "
            + "RETURNING id";
        var connection = postgres.RealisticConnectionString;
        var before = (long)(await RealisticDatabase.ScalarAsync(connection, "SELECT count(*) FROM reservations"))!;
        var accounts = (long)(await RealisticDatabase.ScalarAsync(connection, "SELECT count(*) FROM accounts"))!;

        var first = await RealisticNullabilityOracle.RunAsync(connection, Insert);
        var second = await RealisticNullabilityOracle.RunAsync(connection, Insert);

        ((long)(await RealisticDatabase.ScalarAsync(connection, "SELECT count(*) FROM reservations"))!).ShouldBe(
            before
        );
        first.Rows.ShouldBe((int)accounts);
        second.Rows.ShouldBe((int)accounts);
        first.Names.ShouldBe(["id"]);
        first.SawNull.ShouldBe([false]);
    }

    [Fact]
    public async Task RunAsync_NullableColumn_SawNullWhereARowHasOne()
    {
        var run = await RealisticNullabilityOracle.RunAsync(
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
            RealisticNullabilityOracle.RunAsync(
                postgres.RealisticConnectionString,
                "SELECT pg_terminate_backend(pg_backend_pid())"
            )
        );

        thrown.SqlState.ShouldBe(PostgresErrorCodes.AdminShutdown);
    }
}
