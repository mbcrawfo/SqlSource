using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading.Tasks;
using Testcontainers.MsSql;
using Xunit;

namespace SqlSource.DatabaseTests.TestHarness;

// One SQL Server for every test of the project's collection, which the project's subclass supplies with its driver's
// factory, its image and its databases, each made once.  Testcontainers removes the container when the tests end, and
// its reaper does when the process is killed.
public abstract class SqlServerContainer(DbProviderFactory factory, string image) : IAsyncLifetime
{
    // Set by InitializeAsync, which starts it through EngineImage, and that through SqlServerStart (TD-0072).
    private MsSqlContainer? _container;

    private string _database = "";

    // The databases to build on a server of that major, in order: each its name and the scripts to run on it, in
    // order.  The first is the container's default database, the one ConnectionString, ExecuteAsync and
    // ScalarAsync use.  Called once, when the container has started.
    protected abstract IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major);

    // The server's major version, read once when the container has started, before DatabasesOf is called: 14 for
    // SQL Server 2017, 17 for 2025.
    public int Major { get; private set; }

    // The build that SERVERPROPERTY('ProductVersion') reports on each image the tests run: the default, the floor and
    // the images of the nightly run, newest first, recorded when its tag was chosen.  Moving a tag changes its row; see
    // tests/DatabaseTests/SqlServer/README.md.
    public static IReadOnlyDictionary<string, string> ImageBuilds { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04"] = "17.0.5015.4",
            ["mcr.microsoft.com/mssql/server:2022-CU27-GDR1-ubuntu-22.04"] = "16.0.4305.5",
            ["mcr.microsoft.com/mssql/server:2019-CU32-GDR12-ubuntu-20.04"] = "15.0.4500.2",
            ["mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04"] = "14.0.3560.2",
        };

    // Of the default database.
    public string ConnectionString => ConnectionStringOf(_database);

    // Of the named database of the container.  The container's own string trusts the server's certificate.
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

    // Skips the test on a server before that major, which lacks what the reason names.
    public void SkipBelow(int major, string reason) => EngineImage.SkipBelow(Major, major, reason);

    // Skips the test on a server whose major is not recorded, where what it pins is not known.
    public void SkipUnlessRecordedMajor(string what) => EngineImage.SqlServer.SkipUnlessRecordedMajor(Major, what);

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;

        // The container's own database is "master", so that every database of the subclass is made by CREATE
        // DATABASE.
        _container = await EngineImage.SqlServer.StartAsync(new MsSqlBuilder(image).Build, token);
        try
        {
            await using var server = SourceOf("master");
            var version = await TestDatabases.ScalarAsync(
                server,
                "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)",
                token
            );
            Major = Convert.ToInt32(version, CultureInfo.InvariantCulture);
            var databases = DatabasesOf(Major);
            _database = TestDatabases.DefaultOf(databases, GetType());
            await TestDatabases.BuildAsync(server, EndToEndEngine.SqlServer, databases, SourceOf, token);
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

    private MsSqlContainer Container =>
        _container ?? throw new InvalidOperationException("The container has not started.");
}
