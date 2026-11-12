using System.Data.Common;
using System.Threading.Tasks;

namespace SqlSource.DatabaseTests.TestHarness.SyncAsync;

// What an engine's project gives the runner, so that the runner names no driver.
internal interface IEngine
{
    // The engine's data source of that name, on the shared seeded database when database is null.  The runner disposes
    // a source it asked for with a database, never the shared one.
    DbDataSource SourceOf(string source, string? database);

    // A copy of the seed; returns its name.
    Task<string> FreshDatabaseAsync(string source);

    Task DropDatabaseAsync(string database);

    // SqlState on PostgreSQL, Number on SQL Server.
    string? CodeOf(DbException exception);

    // The engine's text for what became of the transaction, read after the call.
    Task<string> TransactionStateAsync(DbConnection connection, DbTransaction transaction);
}
