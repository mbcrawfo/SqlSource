using System.Collections.Generic;
using Npgsql;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.DatabaseTests.Postgres;

// The PostgreSQL with PostGIS of the collection UsesPostgis, on the PostGIS image built on EngineImage.Postgres.Image,
// the same PostgreSQL as PostgresDatabases runs: the database "gis" with its schema and seed, made once.  A container
// of its own, which only the NetTopologySuite tests and the project check start, so that the other tests never wait
// for its image.
public sealed class PostgisDatabases() : PostgisContainer(NpgsqlFactory.Instance, EngineImage.Postgres.Image)
{
    // The database of the queries of EndToEnd/NetTopologySuite/, built from EndToEnd/gis/.
    public const string GisDatabase = "gis";

    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
        [(GisDatabase, EndToEndFiles.SchemaAndSeed(EndToEndEngine.Postgres, GisDatabase))];
}
