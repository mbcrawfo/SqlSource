using System.Threading.Tasks;
using Npgsql;
using SqlSource.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace SqlSource.Tests.Postgres.Npgsql8;

// A PostgreSQL with the end-to-end database "app": the default image of the end-to-end project, its schema and its
// seed.  The image is the default image of DefaultImages, so that it is named once.
public sealed class AppContainer : IAsyncLifetime
{
    private const string Database = "app";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(DefaultImages.Postgres)
        .WithDatabase("postgres")
        .Build();

    // Of the database "app".
    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Database = Database }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await _container.StartAsync(token);
        try
        {
            await using var server = NpgsqlDataSource.Create(_container.GetConnectionString());
            await EndToEndFiles.CreateAsync(
                server,
                EndToEndEngine.Postgres,
                Database,
                _ => NpgsqlDataSource.Create(ConnectionString),
                token
            );
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
