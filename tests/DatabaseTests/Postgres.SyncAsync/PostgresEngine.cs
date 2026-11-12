using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.DatabaseTests.Postgres.EndToEnd.Types;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync;

// The runner's PostgreSQL: the data sources the end-to-end tests run their queries on, by name, a copy of a seeded
// database for a scenario that writes, SQL states, what became of a caller's transaction and the text of a NodaTime
// value or a geometry.  It disposes the shared sources it made.
internal sealed class PostgresEngine(PostgresDatabases databases) : IEngine, IAsyncDisposable
{
    // The SQL state of a statement in a transaction that an error aborted.
    private const string InFailedTransaction = "25P02";

    // The number of the last copy made in this process, so that each copy has a name of its own.
    private static int _copies;

    private readonly Dictionary<string, NpgsqlDataSource> _shared = [with(StringComparer.Ordinal)];

    public DbDataSource SourceOf(string source, string? database)
    {
        if (database is not null)
        {
            return Build(source, database);
        }

        if (!_shared.TryGetValue(source, out var shared))
        {
            shared = Build(source, DatabaseOf(source));
            _shared.Add(source, shared);
        }

        return shared;
    }

    public async Task<string> FreshDatabaseAsync(string source)
    {
        var name = string.Create(CultureInfo.InvariantCulture, $"run_{Interlocked.Increment(ref _copies)}");
        await ServerAsync($"CREATE DATABASE {name} TEMPLATE {PostgresDatabases.TemplateOf(DatabaseOf(source))}");
        return name;
    }

    // The runner has disposed the copy's source, and the pools of connections opened without one let go of theirs;
    // FORCE ends any connection that is still open.
    public async Task DropDatabaseAsync(string database)
    {
        NpgsqlConnection.ClearAllPools();
        await ServerAsync($"DROP DATABASE {database} WITH (FORCE)");
    }

    public string? CodeOf(DbException exception) => exception.SqlState;

    // "completed" when the call committed or rolled the transaction back, "aborted" when an error left it unusable,
    // else "active".
    public async Task<string> TransactionStateAsync(DbConnection connection, DbTransaction transaction)
    {
        if (transaction.Connection is null)
        {
            return "completed";
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT 1";
        command.Transaction = transaction;
        try
        {
            _ = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
            return "active";
        }
        catch (PostgresException exception) when (exception.SqlState == InFailedTransaction)
        {
            return "aborted";
        }
    }

    public string? Render(object value) => LibraryRendering.Of(value);

    public async ValueTask DisposeAsync()
    {
        foreach (var source in _shared.Values)
        {
            await source.DisposeAsync();
        }

        _shared.Clear();
    }

    // The database of a source's name: "gis" and "billing" their own, the others "app".
    private static string DatabaseOf(string source) =>
        source switch
        {
            "default" or "multiplexing" or "nodatime" => PostgresDatabases.AppDatabase,
            "gis" => PostgresDatabases.GisDatabase,
            "billing" => PostgresDatabases.BillingDatabase,
            _ => throw new ArgumentException($"No data source is named '{source}'.", nameof(source)),
        };

    // A data source set up as EndToEndSources of the end-to-end project sets up the one of that name.  Its connection
    // string keeps the password, so that a scenario can make a ConnectionStringRepository of the source's.
    private NpgsqlDataSource Build(string source, string database)
    {
        var builder = new NpgsqlDataSourceBuilder(databases.ConnectionStringOf(database));
        builder.ConnectionStringBuilder.PersistSecurityInfo = true;
        switch (source)
        {
            case "default":
                _ = builder.MapEnum<NoteKind>();
                break;
            case "multiplexing":
                builder.ConnectionStringBuilder.Multiplexing = true;
                _ = builder.MapEnum<NoteKind>();
                break;
            case "nodatime":
                _ = builder.MapEnum<NoteKind>();
                _ = builder.UseNodaTime();
                break;
            case "gis":
                _ = builder.UseNetTopologySuite();
                break;
            case "billing":
                break;
            default:
                throw new ArgumentException($"No data source is named '{source}'.", nameof(source));
        }

        return builder.Build();
    }

    private async Task ServerAsync(string sql)
    {
        await using var server = databases.SourceOf(PostgresDatabases.ServerDatabase);
        await TestDatabases.ExecuteAsync(server, sql, TestContext.Current.CancellationToken);
    }
}
