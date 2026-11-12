using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync;

// The runner's SQL Server: the connections the end-to-end tests run their queries on, by name, a copy of a seeded
// database for a scenario that writes, error numbers, what became of a caller's transaction and the text of a NodaTime
// value or a geometry.  It disposes the shared sources it made.
internal sealed class SqlServerEngine(SqlServerDatabases databases) : IEngine, IAsyncDisposable
{
    // The number of the last copy made in this process, so that each copy has a name of its own.
    private static int _copies;

    private readonly Dictionary<string, DbDataSource> _shared = [with(StringComparer.Ordinal)];

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

    // Restores the backup of the source's database under a name of its own, each of its files moved to a file of the
    // copy's.
    public async Task<string> FreshDatabaseAsync(string source)
    {
        var database = DatabaseOf(source);
        var name = string.Create(CultureInfo.InvariantCulture, $"run_{Interlocked.Increment(ref _copies)}");
        var moves = databases
            .LogicalFilesOf(database)
            .Select(logical => $"MOVE N'{logical}' TO N'{SqlServerDatabases.DataFolder}{name}_{logical}'");
        await ServerAsync(
            $"RESTORE DATABASE {name} FROM DISK = N'{SqlServerDatabases.BackupOf(database)}' "
                + $"WITH {string.Join(", ", moves)}"
        );
        return name;
    }

    // The runner has disposed the copy's source, and the pools let go of their connections; SINGLE_USER ends any
    // session that is still in it.
    public async Task DropDatabaseAsync(string database)
    {
        SqlConnection.ClearAllPools();
        await ServerAsync(
            $"ALTER DATABASE {database} SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE {database};"
        );
    }

    // The number of the SqlException in the exception or under it: a CollectionItemException is a DbException that
    // holds the driver's inside, and passes on what DbException has, which a SQL Server error number is not.
    public string? CodeOf(DbException exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql)
            {
                return sql.Number.ToString(CultureInfo.InvariantCulture);
            }
        }

        return null;
    }

    // "completed" when the call committed the transaction or the server rolled it back, else the transaction's state as
    // the server sees it in it: XACT_STATE() is 1 for one that can commit and -1 for one that an error doomed, and
    // @@TRANCOUNT how many are open.
    public async Task<string> TransactionStateAsync(DbConnection connection, DbTransaction transaction)
    {
        if (transaction.Connection is null)
        {
            return "completed";
        }

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT CONCAT('xact_state=', XACT_STATE(), ' trancount=', @@TRANCOUNT)";
        command.Transaction = transaction;
        return (string)(await command.ExecuteScalarAsync(TestContext.Current.CancellationToken))!;
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

    // The database of a source's name: "billing" its own, the others "app".
    private static string DatabaseOf(string source) =>
        source switch
        {
            "default" or "unpooled" => SqlServerDatabases.AppDatabase,
            "billing" => SqlServerDatabases.BillingDatabase,
            _ => throw new ArgumentException($"No data source is named '{source}'.", nameof(source)),
        };

    // A source of the database's connection string as the end-to-end project's EndToEndSources makes it: "unpooled"
    // without pooling, for a scenario whose Prepare changes a setting of the session, such as SET XACT_ABORT ON, which
    // the pool would hand to the next scenario.
    private DbDataSource Build(string source, string database)
    {
        var builder = new SqlConnectionStringBuilder(databases.ConnectionStringOf(database));
        switch (source)
        {
            case "default":
            case "billing":
                break;
            case "unpooled":
                builder.Pooling = false;
                break;
            default:
                throw new ArgumentException($"No data source is named '{source}'.", nameof(source));
        }

        return SqlClientFactory.Instance.CreateDataSource(builder.ConnectionString);
    }

    private async Task ServerAsync(string sql)
    {
        await using var server = databases.SourceOf(SqlServerDatabases.ServerDatabase);
        await TestDatabases.ExecuteAsync(server, sql, TestContext.Current.CancellationToken);
    }
}
