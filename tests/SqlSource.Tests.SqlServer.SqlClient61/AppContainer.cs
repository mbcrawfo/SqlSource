using System.IO;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlSource.Testing;
using Testcontainers.MsSql;
using Xunit;

namespace SqlSource.Tests.SqlServer.SqlClient61;

// A SQL Server of DefaultImages.SqlServer, the end-to-end project's default image, with the end-to-end database "app":
// its schema, its seed and the table of the types of SQL Server 2025.
public sealed class AppContainer : IAsyncLifetime
{
    private const string Database = "app";

    private readonly MsSqlContainer _container = new MsSqlBuilder(DefaultImages.SqlServer).Build();

    // Of the database "app".  The container's own string trusts the server's certificate.
    public string ConnectionString =>
        new SqlConnectionStringBuilder(_container.GetConnectionString()) { InitialCatalog = Database }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        await _container.StartAsync(token);
        try
        {
            await using var server = SqlClientFactory.Instance.CreateDataSource(_container.GetConnectionString());
            await EndToEndFiles.CreateAsync(
                server,
                EndToEndEngine.SqlServer,
                Database,
                _ => SqlClientFactory.Instance.CreateDataSource(ConnectionString),
                [
                    EndToEndFiles.Schema(EndToEndEngine.SqlServer, Database),
                    EndToEndFiles.Seed(EndToEndEngine.SqlServer, Database),
                    // The default image is SQL Server 2025, which has json and vector.
                    Path.Combine(EndToEndEngine.SqlServer.Folder, Database, "new-types.sql"),
                ],
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
