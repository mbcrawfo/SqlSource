using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd.NetTopologySuite;

// The sidecar of the queries of PostGIS is what the PostGIS container answers today: "describe --check" on the
// project, named to "gis" by --database, finds every entry the same as its sidecar's, with every parameter of PostGIS
// bound through the tool's Npgsql.NetTopologySuite.  CheckTests checks the project's other databases.
[Collection(UsesPostgis.Name)]
public sealed class GisCheckTests(PostgisDatabases postgis)
{
    [Fact]
    public async Task Check_Gis_EveryEntryIsTheSame()
    {
        var gis = CheckTests.ProjectEntries(PostgisDatabases.GisDatabase);
        gis.ShouldBeGreaterThan(0);

        var result = await CheckTests.CheckAsync([
            "--database",
            PostgisDatabases.GisDatabase,
            "--connection",
            PostgisDatabases.GisDatabase + "=" + postgis.ConnectionStringOf(PostgisDatabases.GisDatabase),
        ]);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe($"gis (postgres): {gis} same, 0 differ, 0 failed\n");
    }
}
