using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The tests that read and write the database "app" of AppContainer, which the collection starts once for the run, so
// that the test classes share one PostgreSQL.
[CollectionDefinition(Name)]
public sealed class UsesApp : ICollectionFixture<AppContainer>
{
    public const string Name = "app";
}
