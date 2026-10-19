using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace SqlSource.Tests.Postgres.Npgsql8;

// A PostgreSQL with the end-to-end database "app": the default image of the end-to-end project, its schema and its
// seed.  The image is read from the line of PostgresContainer.cs that tools/record-fixtures.sh reads, so that it is
// named once.
public sealed partial class AppContainer : IAsyncLifetime
{
    private const string Database = "app";

    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder(DefaultImage())
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
            await using (var source = NpgsqlDataSource.Create(_container.GetConnectionString()))
            await using (var create = source.CreateCommand($"CREATE DATABASE {Database}"))
            {
                _ = await create.ExecuteNonQueryAsync(token);
            }

            await using var app = NpgsqlDataSource.Create(ConnectionString);
            foreach (var path in new[] { EndToEndFiles.Schema(Database), EndToEndFiles.Seed(Database) })
            {
                var sql = await File.ReadAllTextAsync(path, token);
                await using var command = app.CreateCommand(sql);
                _ = await command.ExecuteNonQueryAsync(token);
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

    // The image on the line that declares DefaultImage, which fails the run when the line has changed its shape.
    private static string DefaultImage()
    {
        var path = Path.Combine(RepositoryRoot.Folder, "tests", "SqlSource.Tests.Postgres", "PostgresContainer.cs");
        var match = DefaultImageLine().Match(File.ReadAllText(path));
        return match.Success
            ? match.Groups["image"].Value
            : throw new InvalidOperationException(
                $"{path} has no line that declares DefaultImage as a string, which these tests and tools/ read."
            );
    }

    [GeneratedRegex("""DefaultImage = "(?<image>[^"]*)";""")]
    private static partial Regex DefaultImageLine();
}
