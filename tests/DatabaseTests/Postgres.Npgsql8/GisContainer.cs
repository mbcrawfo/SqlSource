using System.Collections.Generic;
using Npgsql;
using SqlSource.DatabaseTests.TestHarness;

namespace SqlSource.DatabaseTests.Postgres.Npgsql8;

// A PostgreSQL with PostGIS, of the PostGIS image built on EngineImage.Postgres.Default, the image AppContainer runs,
// with the end-to-end database "gis": its schema and its seed.
public sealed class GisContainer() : PostgisContainer(NpgsqlFactory.Instance, EngineImage.Postgres.Default)
{
    protected override IReadOnlyList<(string Name, IReadOnlyList<string> Scripts)> DatabasesOf(int major) =>
        [("gis", EndToEndFiles.SchemaAndSeed(EndToEndEngine.Postgres, "gis"))];
}
