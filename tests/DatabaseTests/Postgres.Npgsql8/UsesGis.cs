using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// The tests that read and write the database "gis" of GisContainer, which the collection starts once for the run, so
// that the NetTopologySuite tests share one PostgreSQL with PostGIS and no other test waits for its image.
[CollectionDefinition(Name)]
public sealed class UsesGis : ICollectionFixture<GisContainer>
{
    public const string Name = "gis";
}
