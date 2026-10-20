using Xunit;

namespace SqlSource.Tests.Postgres;

// The collection of the tests that need the server.  Its classes share one PostgresContainer and run one after
// another, as a session describes one query at a time.
[CollectionDefinition(Name)]
public sealed class UsesPostgres : ICollectionFixture<PostgresContainer>
{
    public const string Name = "postgres";
}
