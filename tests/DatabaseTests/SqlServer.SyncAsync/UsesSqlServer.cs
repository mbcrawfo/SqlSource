using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.SyncAsync;

// The collection of the comparisons, which share one SqlServerDatabases.  It runs one scenario at a time, since a
// scenario that writes restores and drops a database of its own and reads what the server holds.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UsesSqlServer : ICollectionFixture<SqlServerDatabases>
{
    public const string Name = "sqlserver";
}
