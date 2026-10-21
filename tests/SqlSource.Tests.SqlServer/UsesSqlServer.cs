using Xunit;

namespace SqlSource.Tests.SqlServer;

// The collection of the tests that need the server.  Its classes share one SqlServerContainer and run one after
// another, as a session describes one query at a time.
[CollectionDefinition(Name)]
public sealed class UsesSqlServer : ICollectionFixture<SqlServerContainer>
{
    public const string Name = "sqlserver";
}
