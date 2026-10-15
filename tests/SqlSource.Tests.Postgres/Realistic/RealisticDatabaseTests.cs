using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

// The database "realistic" of the corpus: made beside "fixtures", with its schema and its seed applied, and the
// shared database left as it was.
[Collection(UsesPostgres.Name)]
public sealed class RealisticDatabaseTests(PostgresContainer postgres)
{
    [Fact]
    public async Task Database_AfterStart_HasEveryTableAndTheSeed()
    {
        var token = TestContext.Current.CancellationToken;
        await using var realistic = NpgsqlDataSource.Create(postgres.RealisticConnectionString);
        await using var fixtures = NpgsqlDataSource.Create(postgres.ConnectionString);

        var accounts = await Scalar(realistic, "SELECT count(*) FROM accounts", token);
        var partitions = await Scalar(
            realistic,
            "SELECT count(*) FROM pg_catalog.pg_inherits WHERE inhparent = 'events'::regclass",
            token
        );
        var sharedUntouched = await Scalar(fixtures, "SELECT to_regclass('public.accounts') IS NULL", token);

        ((long)accounts!).ShouldBeGreaterThan(0);
        ((long)partitions!).ShouldBe(2);
        ((bool)sharedUntouched!).ShouldBeTrue();
    }

    private static async Task<object?> Scalar(NpgsqlDataSource source, string sql, CancellationToken token)
    {
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(token);
    }
}
