using Xunit;

namespace SqlSource.DatabaseTests.Postgres.SyncAsync;

// The collection of the comparisons, which share one PostgresDatabases.  Every test of the project is in it, since the
// one PostGIS container holds the three databases, gis among them.  It runs one scenario at a time, since a scenario
// that writes makes and drops a database of its own and reads what the server holds.
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class UsesPostgis : ICollectionFixture<PostgresDatabases>
{
    public const string Name = "postgis";
}
