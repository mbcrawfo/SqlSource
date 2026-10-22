using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlSource.Testing;
using Testcontainers.MsSql;
using Xunit;

namespace SqlSource.Tests.SqlServer;

// One SQL Server for every test of the collection UsesSqlServer: the image, the database "fixtures" with the fixture
// schema, and the database "realistic" with its schema and seed, each made once.  Testcontainers removes the container
// when the tests end, and its reaper does when the process is killed.
public sealed class SqlServerContainer : IAsyncLifetime
{
    // Names another image: CI runs the tests again on the oldest SQL Server that is supported.
    public const string ImageVariable = "SQLSOURCE_TESTS_SQLSERVER_IMAGE";

    public const string Database = "fixtures";

    // The second database, for the corpus of realistic queries: its own schema and seed, so that the fixture schema
    // and the rows that tests count in it stay as they are.
    public const string RealisticDatabase = "realistic";

    private readonly MsSqlContainer _container = new MsSqlBuilder(Image).Build();

    // The server's major version, read once when the container has started: 14 for SQL Server 2017, 17 for 2025.
    public int Major { get; private set; }

    // The majors that CI runs and that the tests pin answers for.
    public static IReadOnlyList<int> RecordedMajors { get; } = [14, 17];

    public static string Image =>
        Environment.GetEnvironmentVariable(ImageVariable) is { Length: > 0 } image ? image : DefaultImages.SqlServer;

    // The build that SERVERPROPERTY('ProductVersion') reports on each image the tests run, the default and the floor,
    // recorded when its tag was chosen.  Moving a tag changes its row; see tests/SqlSource.Tests.SqlServer/README.md.
    public static IReadOnlyDictionary<string, string> ImageBuilds { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["mcr.microsoft.com/mssql/server:2025-CU9-GDR1-ubuntu-24.04"] = "17.0.5015.4",
            ["mcr.microsoft.com/mssql/server:2017-CU31-GDR19-ubuntu-18.04"] = "14.0.3560.2",
        };

    // The tag of an image, which for a pinned image names its update: "...:2025-CU9-GDR1-ubuntu-24.04" is
    // "2025-CU9-GDR1-ubuntu-24.04".
    public static string TagOf(string image) => image[(image.LastIndexOf(':') + 1)..];

    // Of the database "fixtures".
    public string ConnectionString => ConnectionStringOf(Database);

    // Of the database "realistic".
    public string RealisticConnectionString => ConnectionStringOf(RealisticDatabase);

    // Of the named database of the container.  The container's own string trusts the server's certificate.
    public string ConnectionStringOf(string database) =>
        new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = database }.ConnectionString;

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
    public void SkipBelow(int major, string reason) => SkipBelow(Major, major, reason);

    public static void SkipBelow(int server, int major, string reason) => Assert.SkipWhen(server < major, reason);

    // Skips the test on a server whose major is not recorded, where what it pins is not known.
    public void SkipUnlessRecordedMajor(string what) => SkipUnlessRecordedMajor(Major, what);

    public static void SkipUnlessRecordedMajor(int server, string what) =>
        Assert.SkipUnless(
            RecordedMajors.Contains(server),
            string.Create(
                CultureInfo.InvariantCulture,
                $"{what} are pinned for SQL Server {string.Join(" and ", RecordedMajors)} alone."
            )
        );

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await _container.StartAsync(token);
        try
        {
            await using (var master = new SqlConnection(ConnectionStringOf("master")))
            {
                await master.OpenAsync(token);
                await using var create = new SqlCommand(
                    $"CREATE DATABASE {Database}; CREATE DATABASE {RealisticDatabase};",
                    master
                );
                _ = await create.ExecuteNonQueryAsync(token);
            }

            await using var fixtures = new SqlConnection(ConnectionString);
            await fixtures.OpenAsync(token);
            await SqlServerScripts.RunAsync(fixtures, RepositoryPaths.SqlServerSchema, token);

            // The database of the corpus, built after "fixtures" from its schema, then its seed.
            await using (var realistic = new SqlConnection(RealisticConnectionString))
            {
                await realistic.OpenAsync(token);
                await SqlServerScripts.RunAsync(realistic, RepositoryPaths.RealisticSchema, token);
                await SqlServerScripts.RunAsync(realistic, RepositoryPaths.RealisticSeed, token);
            }

            await using var major = new SqlCommand(
                "SELECT CAST(SERVERPROPERTY('ProductMajorVersion') AS int)",
                fixtures
            );
            Major = Convert.ToInt32(await major.ExecuteScalarAsync(token), CultureInfo.InvariantCulture);
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
