using System;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.DatabaseTests.TestHarness;
using Testcontainers.PostgreSql;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// A PostgreSQL of EngineImage.Postgres.Default, the end-to-end project's default image, with the end-to-end database
// "app": its schema and its seed.
public sealed class AppContainer : IAsyncLifetime
{
    private const string Database = "app";

    // Set by InitializeAsync, which starts it through EngineImage.
    private PostgreSqlContainer? _container;

    // Of the database "app".
    public string ConnectionString =>
        new NpgsqlConnectionStringBuilder(Container.GetConnectionString()) { Database = Database }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        _container = await EngineImage.Postgres.StartAsync(
            new PostgreSqlBuilder(EngineImage.Postgres.Default).WithDatabase("postgres").Build,
            token
        );
        try
        {
            await using var server = NpgsqlDataSource.Create(Container.GetConnectionString());
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

    public ValueTask DisposeAsync() => _container?.DisposeAsync() ?? ValueTask.CompletedTask;

    private PostgreSqlContainer Container =>
        _container ?? throw new InvalidOperationException("The container has not started.");
}
