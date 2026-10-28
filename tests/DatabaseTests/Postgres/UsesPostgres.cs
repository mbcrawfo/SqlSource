using Xunit;

namespace SqlSource.DatabaseTests.Postgres;

// The collection of the tests that need the server.  Its classes share one PostgresDatabases and run one after
// another, as a session describes one query at a time.
[CollectionDefinition(Name)]
public sealed class UsesPostgres : ICollectionFixture<PostgresDatabases>
{
    public const string Name = "postgres";
}
