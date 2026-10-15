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
    // The one place that names the image.  tools/record-fixtures.sh reads it from this line.
    public const string DefaultImage = "postgres:18";

    // Names another image: CI runs the tests again on the oldest PostgreSQL that is supported.
    public const string ImageVariable = "SQLSOURCE_TESTS_POSTGRES_IMAGE";

    public const string Database = "fixtures";

    // The second database, for the corpus of realistic queries: its own schema and seed, so that the fixture schema
    // and the rows that tests count in it stay as they are.
    public const string RealisticDatabase = "realistic";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(Image).WithDatabase(Database).Build();

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

            await using var realistic = NpgsqlDataSource.Create(RealisticConnectionString);
            foreach (var path in new[] { RepositoryPaths.RealisticSchema, RepositoryPaths.RealisticSeed })
            {
                await using var script = realistic.CreateCommand(await File.ReadAllTextAsync(path, token));
                _ = await script.ExecuteNonQueryAsync(token);
            }
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
