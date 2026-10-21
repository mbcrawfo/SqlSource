using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
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
    // Names another image: CI runs the tests again on the oldest PostgreSQL that is supported.
    public const string ImageVariable = "SQLSOURCE_TESTS_POSTGRES_IMAGE";

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

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).WithDatabase(Database).Build();

    // The server's major version, read once when the container has started.
    public int Major { get; private set; }

    // The majors that CI runs and that the tests pin answers for: a realistic case's committed entry, the noise
    // budget and the forms of a plan.
    public static IReadOnlyList<int> RecordedMajors { get; } = [14, 18];

    public static string Image =>
        Environment.GetEnvironmentVariable(ImageVariable) is { Length: > 0 } image ? image : DefaultImages.Postgres;

    // The tag of an image, which for a pinned image is the server's version: "postgres:18.6" is "18.6".
    public static string TagOf(string image) => image[(image.LastIndexOf(':') + 1)..];

    // The databases that the end-to-end tests are described and run against, each built from its folder of
    // tests/SqlSource.Tests.Postgres/EndToEnd/.
    public static IReadOnlyList<string> EndToEndDatabases { get; } = [AppDatabase, BillingDatabase];

    // Of the database "fixtures".
    public string ConnectionString => _container.GetConnectionString();

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
    public void SkipBelow(int major, string reason) => SkipBelow(Major, major, reason);

    public static void SkipBelow(int server, int major, string reason) => Assert.SkipWhen(server < major, reason);

    // Skips the test on a server whose major is not recorded, where what it pins is not known.
    public void SkipUnlessRecordedMajor(string what) => SkipUnlessRecordedMajor(Major, what);

    public static void SkipUnlessRecordedMajor(int server, string what) =>
        Assert.SkipUnless(
            RecordedMajors.Contains(server),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{what} are pinned for PostgreSQL {string.Join(" and ", RecordedMajors)} alone."
            )
        );

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await _container.StartAsync(token);
        try
        {
            await using var source = NpgsqlDataSource.Create(ConnectionString);
            await EndToEndFiles.RunAsync(source, RepositoryPaths.PostgresSchema, token);

            // The seed relies on a hole of PostgreSQL: the NOT NULL of a domain is not checked again for a NULL that
            // is already of the domain, which gives invoice 4 its NULL amount.  A major that closes it fails the seed
            // here, and so the start of the container, and the truths of table-statement, cte-materialized,
            // distinct-on-latest-per-account and view-joined-left, which count on that NULL, are to be read again.
            await EndToEndFiles.CreateAsync(
                source,
                RealisticDatabase,
                SourceOf,
                [RepositoryPaths.RealisticSchema, RepositoryPaths.RealisticSeed],
                token
            );

            // The databases of the end-to-end tests: each is created, then built from its schema and its seed, in the
            // order that tools/describe-end-to-end.sh applies them.
            foreach (var database in EndToEndDatabases)
            {
                await EndToEndFiles.CreateAsync(source, database, SourceOf, token);
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

    public ValueTask DisposeAsync() => _container.DisposeAsync();
}
