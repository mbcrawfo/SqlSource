using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlSource.Testing;
using Testcontainers.MsSql;
using Xunit;

namespace SqlSource.Tests.SqlServer;

// One SQL Server for every test of the collection UsesSqlServer: the image, the database "fixtures" with the fixture
// schema, the database "realistic" with its schema and seed, and the end-to-end databases "app" and "billing", each
// made once.  Testcontainers removes the container when the tests end, and its reaper does when the process is
// killed.
public sealed class SqlServerContainer : IAsyncLifetime
{
    public const string Database = "fixtures";

    // The second database, for the corpus of realistic queries: its own schema and seed, so that the fixture schema
    // and the rows that tests count in it stay as they are.
    public const string RealisticDatabase = "realistic";

    // The logical databases of the end-to-end tests, built from EndToEnd/app/ and EndToEnd/billing/.
    public const string AppDatabase = "app";

    public const string BillingDatabase = "billing";

    // Set by InitializeAsync, which starts it through EngineImage, and that through SqlServerStart (TD-0072).
    private MsSqlContainer? _container;

    // The server's major version, read once when the container has started: 14 for SQL Server 2017, 17 for 2025.
    public int Major { get; private set; }

    // The databases of the end-to-end tests, in the order they are built.
    public static IReadOnlyList<string> EndToEndDatabases { get; } = [AppDatabase, BillingDatabase];

    // The build that SERVERPROPERTY('ProductVersion') reports on each image the tests run, the default and the floor,
    // recorded when its tag was chosen.  Moving a tag changes its row; see tests/SqlSource.Tests.SqlServer/README.md.
    public static IReadOnlyDictionary<string, string> ImageBuilds { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04"] = "17.0.5015.4",
            ["mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04"] = "14.0.3560.2",
        };

    // Of the database "fixtures".
    public string ConnectionString => ConnectionStringOf(Database);

    // Of the database "realistic".
    public string RealisticConnectionString => ConnectionStringOf(RealisticDatabase);

    // Of the named database of the container.  The container's own string trusts the server's certificate.
    public string ConnectionStringOf(string database) =>
        new SqlConnectionStringBuilder(Container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

    // A source of the named database of the container, which the caller disposes.
    public DbDataSource SourceOf(string database) =>
        SqlClientFactory.Instance.CreateDataSource(ConnectionStringOf(database));

    // Runs one statement of the test's own on the database "fixtures".
    public async Task ExecuteAsync(string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection);
        _ = await command.ExecuteNonQueryAsync(token);
    }

    // One value from the server, on the database "fixtures".
    public async Task<object?> ScalarAsync(string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var connection = new SqlConnection(ConnectionString);
        await connection.OpenAsync(token);
        await using var command = new SqlCommand(sql, connection);
        return await command.ExecuteScalarAsync(token);
    }

    // Skips the test on a server before that major, which lacks what the reason names.
    public void SkipBelow(int major, string reason) => EngineImage.SkipBelow(Major, major, reason);

    // Skips the test on a server whose major is not recorded, where what it pins is not known.
    public void SkipUnlessRecordedMajor(string what) => EngineImage.SqlServer.SkipUnlessRecordedMajor(Major, what);

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        _container = await EngineImage.SqlServer.StartAsync(new MsSqlBuilder(EngineImage.SqlServer.Image).Build, token);
        try
        {
            await using var server = SourceOf("master");
            await EndToEndFiles.CreateAsync(
                server,
                EndToEndEngine.SqlServer,
                Database,
                SourceOf,
                [RepositoryPaths.SqlServerSchema],
                token
            );

            // The database of the corpus, built after "fixtures" from its schema, then its seed.
            await EndToEndFiles.CreateAsync(
                server,
                EndToEndEngine.SqlServer,
                RealisticDatabase,
                SourceOf,
                [RepositoryPaths.RealisticSchema, RepositoryPaths.RealisticSeed],
                token
            );

            // Read before the end-to-end databases, whose files it chooses.
            await using (var major = server.CreateCommand("SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)"))
            {
                Major = Convert.ToInt32(await major.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
            }

            // The databases of the end-to-end tests: each is created, then built from its schema and its seed, and
            // "app" then gets the table of the types of SQL Server 2025 on a major of 17 or later, in the order that
            // tools/describe-end-to-end.sh applies them.
            foreach (var database in EndToEndDatabases)
            {
                await EndToEndFiles.CreateAsync(
                    server,
                    EndToEndEngine.SqlServer,
                    database,
                    SourceOf,
                    EndToEndFilesOf(database, Major),
                    token
                );
            }
        }
        catch
        {
            // The container has started, so it is removed here and not left to xunit or to the reaper.
            await _container.DisposeAsync();
            throw;
        }
    }

    // The files that build the end-to-end database of that name on a server of that major, in order.
    private static string[] EndToEndFilesOf(string database, int major)
    {
        string[] built =
        [
            EndToEndFiles.Schema(EndToEndEngine.SqlServer, database),
            EndToEndFiles.Seed(EndToEndEngine.SqlServer, database),
        ];
        return database == AppDatabase && major >= 17 ? [.. built, RepositoryPaths.NewTypesSchema] : built;
    }

    public ValueTask DisposeAsync() => _container?.DisposeAsync() ?? ValueTask.CompletedTask;

    private MsSqlContainer Container =>
        _container ?? throw new InvalidOperationException("The container has not started.");
}
