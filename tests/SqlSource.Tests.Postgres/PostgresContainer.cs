using System;
using System.IO;
using System.Threading.Tasks;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace SqlSource.Tests.Postgres;

// One PostgreSQL for every test of the collection UsesPostgres: the image, the database "fixtures", and the fixture
// schema applied to it once.  Testcontainers removes the container when the tests end, and its reaper does when
// the process is killed.
public sealed class PostgresContainer : IAsyncLifetime
{
    // The one place that names the image.  tools/record-fixtures.sh and tools/record-realistic.sh read it from this
    // line, by its shape.
    public const string DefaultImage = "postgres:18";

    // Names another image: CI runs the tests again on the oldest PostgreSQL that is supported.
    public const string ImageVariable = "SQLSOURCE_TESTS_POSTGRES_IMAGE";

    public const string Database = "fixtures";

    // The second database, for the corpus of realistic queries: its own schema and seed, so that the fixture schema
    // and the rows that tests count in it stay as they are.
    public const string RealisticDatabase = "realistic";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).WithDatabase(Database).Build();

    // The server's major version, read once when the container has started.
    public int Major { get; private set; }

    public static string Image =>
        Environment.GetEnvironmentVariable(ImageVariable) is { Length: > 0 } image ? image : DefaultImage;

    // Of the database "fixtures".
    public string ConnectionString => _container.GetConnectionString();

    // Of the database "realistic".
    public string RealisticConnectionString =>
        new NpgsqlConnectionStringBuilder(ConnectionString) { Database = RealisticDatabase }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await _container.StartAsync(token);
        try
        {
            await using var source = NpgsqlDataSource.Create(ConnectionString);
            await using var command = source.CreateCommand(
                await File.ReadAllTextAsync(RepositoryPaths.PostgresSchema, token)
            );
            _ = await command.ExecuteNonQueryAsync(token);

            await using (var create = source.CreateCommand($"CREATE DATABASE {RealisticDatabase}"))
            {
                _ = await create.ExecuteNonQueryAsync(token);
            }

            // The seed relies on a hole of PostgreSQL: the NOT NULL of a domain is not checked again for a NULL that
            // is already of the domain, which gives invoice 4 its NULL amount.  A major that closes it fails the seed
            // here, and so the start of the container, and the truths of table-statement, cte-materialized,
            // distinct-on-latest-per-account and view-joined-left, which count on that NULL, are to be read again.
            await using var realistic = NpgsqlDataSource.Create(RealisticConnectionString);
            foreach (var path in new[] { RepositoryPaths.RealisticSchema, RepositoryPaths.RealisticSeed })
            {
                await using var script = realistic.CreateCommand(await File.ReadAllTextAsync(path, token));
                _ = await script.ExecuteNonQueryAsync(token);
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
