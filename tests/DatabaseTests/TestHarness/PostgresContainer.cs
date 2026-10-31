using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Testcontainers.PostgreSql;
using Xunit;

namespace SqlSource.DatabaseTests.TestHarness;

// One PostgreSQL for every test of the project's collection, which the project's subclass supplies with its driver's
// factory, its image and its databases, each made once.  Testcontainers removes the container when the tests end, and
// its reaper does when the process is killed.
public abstract class PostgresContainer(DbProviderFactory factory, string image) : IAsyncLifetime
{
    // Set by InitializeAsync, which starts it through EngineImage.
    private PostgreSqlContainer? _container;

    private string _database = "";

    // The databases to build on a server of that major, in order: each its name and the scripts to run on it, in
    // order.  The first is the container's default database, the one ConnectionString, ExecuteAsync and
    // ScalarAsync use.  Called once, when the container has started.
    protected abstract IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major);

    // The server's major version, read once when the container has started, before DatabasesOf is called.
    public int Major { get; private set; }

    // Of the default database.
    public string ConnectionString => ConnectionStringOf(_database);

    // Of the named database of the container.
    public string ConnectionStringOf(string database) =>
        TestDatabases.ConnectionStringOf(Container.GetConnectionString(), database);

    // A data source of the named database of the container, which the caller disposes.
    public DbDataSource SourceOf(string database) => factory.CreateDataSource(ConnectionStringOf(database));

    // Runs one statement of the test's own on the default database.
    public async Task ExecuteAsync(string sql)
    {
        await using var source = SourceOf(_database);
        await TestDatabases.ExecuteAsync(source, sql, TestContext.Current.CancellationToken);
    }

    // One value from the server, on the default database.
    public async Task<object?> ScalarAsync(string sql)
    {
        await using var source = SourceOf(_database);
        return await TestDatabases.ScalarAsync(source, sql, TestContext.Current.CancellationToken);
    }

    // Makes the image ready before the container is built: nothing for an image that is pulled, and PostgisContainer
    // builds its own.  A container whose image this throws for is never started.
    protected virtual Task PrepareImageAsync(CancellationToken token) => Task.CompletedTask;

    // Skips the test on a server before that major, which lacks what the reason names.
    public void SkipBelow(int major, string reason) => EngineImage.SkipBelow(Major, major, reason);

    // Skips the test on a server whose major is not recorded, where what it pins is not known.
    public void SkipUnlessRecordedMajor(string what) => EngineImage.Postgres.SkipUnlessRecordedMajor(Major, what);

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await PrepareImageAsync(token);

        // The container's own database is "postgres", so that every database of the subclass is made by CREATE
        // DATABASE.
        _container = await EngineImage.Postgres.StartAsync(new PostgreSqlBuilder(image).Build, token);
        try
        {
            await using var server = factory.CreateDataSource(_container.GetConnectionString());
            var version = await TestDatabases.ScalarAsync(
                server,
                "SELECT current_setting('server_version_num')::int",
                token
            );
            Major = Convert.ToInt32(version, CultureInfo.InvariantCulture) / 10000;
            var databases = DatabasesOf(Major);
            _database = TestDatabases.DefaultOf(databases, GetType());
            await TestDatabases.BuildAsync(server, EndToEndEngine.Postgres, databases, SourceOf, token);
        }
        catch
        {
            // The container has started, so it is removed here and not left to xunit or to the reaper.
            await _container.DisposeAsync();
            throw;
        }
    }

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }

    private PostgreSqlContainer Container =>
        _container ?? throw new InvalidOperationException("The container has not started.");
}
