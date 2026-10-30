using Xunit;

namespace SqlSource.DatabaseTests.Postgres;

// The collection of the tests that need PostGIS.  Its classes share one PostgisDatabases, which no other test starts.
[CollectionDefinition(Name)]
public sealed class UsesPostgis : ICollectionFixture<PostgisDatabases>
{
    public const string Name = "postgis";
}
