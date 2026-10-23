using System;
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

    // Set by InitializeAsync, which starts it through SqlServerStart (TD-0072).
    private MsSqlContainer? _container;

    // Of the database "app".  The container's own string trusts the server's certificate.
    public string ConnectionString =>
        new SqlConnectionStringBuilder(Container.GetConnectionString()) { InitialCatalog = Database }.ConnectionString;

    public async ValueTask InitializeAsync()
    {
        var token = TestContext.Current.CancellationToken;
        _container = await SqlServerStart.StartAsync(new MsSqlBuilder(DefaultImages.SqlServer).Build, token);
        try
        {
            await using var server = SqlClientFactory.Instance.CreateDataSource(Container.GetConnectionString());
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

    public ValueTask DisposeAsync() => _container?.DisposeAsync() ?? ValueTask.CompletedTask;

    private MsSqlContainer Container =>
        _container ?? throw new InvalidOperationException("The container has not started.");
}
