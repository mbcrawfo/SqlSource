using Xunit;

namespace SqlSource.DatabaseTests.SqlServer;

// The collection of the tests that need the server.  Its classes share one SqlServerDatabases and run one after
// another, as a session describes one query at a time.
[CollectionDefinition(Name)]
public sealed class UsesSqlServer : ICollectionFixture<SqlServerDatabases>
{
    public const string Name = "sqlserver";
}
