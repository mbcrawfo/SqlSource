using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace SqlSource.Tests.Postgres;

// One PostgreSQL for every test of the collection UsesPostgres: the image, the database "fixtures" with the fixture
// schema, and the databases "realistic", "app" and "billing" with their schemas and seeds, each made once.
// Testcontainers removes the container when the tests end, and its reaper does when the process is killed.
public sealed class PostgresContainer : IAsyncLifetime
{
    public const string Database = "fixtures";

    // The second database, for the corpus of realistic queries: its own schema and seed, so that the fixture schema
    // and the rows that tests count in it stay as they are.
    public const string RealisticDatabase = "realistic";

    // The databases of the end-to-end tests, in the folders of tests/SqlSource.Tests.Postgres/EndToEnd/ that bear their
    // names: "app" holds the tables that the queries read and every type of the type map, "billing" a second database
    // for a query that names its own.
    public const string AppDatabase = "app";

    public const string BillingDatabase = "billing";

    // Why a test of old and new in RETURNING skips before PostgreSQL 18.
    public const string NoOldAndNewInReturning = "PostgreSQL before 18 has no old and new in RETURNING.";

    // Set by InitializeAsync, which starts it through EngineImage.
    private PostgreSqlContainer? _container;

    // The server's major version, read once when the container has started.
    public int Major { get; private set; }

    // The databases that the end-to-end tests are described and run against, each built from its folder of
    // tests/SqlSource.Tests.Postgres/EndToEnd/.
    public static IReadOnlyList<string> EndToEndDatabases { get; } = [AppDatabase, BillingDatabase];

    // Of the database "fixtures".
    public string ConnectionString => Container.GetConnectionString();

    // Of the database "realistic".
    public string RealisticConnectionString => ConnectionStringOf(RealisticDatabase);

    // Of the named database of the container.
    public string ConnectionStringOf(string database) =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Database = database }.ConnectionString;

    // A data source of the named database of the container.
    private DbDataSource SourceOf(string database) => NpgsqlDataSource.Create(ConnectionStringOf(database));

    // Runs one statement of the test's own on the database "fixtures".
    public async Task ExecuteAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        await using var command = source.CreateCommand(sql);
        _ = await command.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
    }

    // One value from the server, on the database "fixtures".
    public async Task<object?> ScalarAsync(string sql)
    {
        await using var source = NpgsqlDataSource.Create(ConnectionString);
        await using var command = source.CreateCommand(sql);
        return await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
    }

    // Skips the test on a server before that major, which lacks what the reason names.
    public void SkipBelow(int major, string reason) => EngineImage.SkipBelow(Major, major, reason);

    // Skips the test on a server whose major is not recorded, where what it pins is not known.
    public void SkipUnlessRecordedMajor(string what) => EngineImage.Postgres.SkipUnlessRecordedMajor(Major, what);

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        _container = await EngineImage.Postgres.StartAsync(
            new PostgreSqlBuilder(EngineImage.Postgres.Image).WithDatabase(Database).Build,
            token
        );
        try
        {
            await using var source = NpgsqlDataSource.Create(ConnectionString);
            await EndToEndFiles.RunAsync(source, RepositoryPaths.PostgresSchema, EndToEndEngine.Postgres, token);

            // The seed relies on a hole of PostgreSQL: the NOT NULL of a domain is not checked again for a NULL that
            // is already of the domain, which gives invoice 4 its NULL amount.  A major that closes it fails the seed
            // here, and so the start of the container, and the truths of table-statement, cte-materialized,
            // distinct-on-latest-per-account and view-joined-left, which count on that NULL, are to be read again.
            await EndToEndFiles.CreateAsync(
                source,
                EndToEndEngine.Postgres,
                RealisticDatabase,
                SourceOf,
                [RepositoryPaths.RealisticSchema, RepositoryPaths.RealisticSeed],
                token
            );

            // The databases of the end-to-end tests: each is created, then built from its schema and its seed, in the
            // order that tools/describe-end-to-end.sh applies them.
            foreach (var database in EndToEndDatabases)
            {
                await EndToEndFiles.CreateAsync(source, EndToEndEngine.Postgres, database, SourceOf, token);
            }

            await using var opened = await source.OpenConnectionAsync(token);
            Major = opened.PostgreSqlVersion.Major;
        }
        catch
        {
            // The container has started, so it is removed here and not left to xunit or to the reaper.
            await _container.DisposeAsync();
            throw;
        }
    }

    public ValueTask DisposeAsync() => _container?.DisposeAsync() ?? ValueTask.CompletedTask;

    private PostgreSqlContainer Container =>
        _container ?? throw new InvalidOperationException("The container has not started.");
}
