using Xunit;

namespace SqlSource.DatabaseTests.Postgres;

// The collection of the tests that need PostGIS.  Its classes share one PostgisDatabases, which no other test starts,
// and one PostgresDatabases of their own, for EndToEnd/CheckTests, which checks the project on both containers at once.
[CollectionDefinition(Name)]
public sealed class UsesPostgis : ICollectionFixture<PostgisDatabases>, ICollectionFixture<PostgresDatabases>
{
    public const string Name = "postgis";
}
