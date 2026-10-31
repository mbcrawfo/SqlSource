using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.Postgres.Realistic;
using SqlSource.DatabaseTests.TestHarness.EndToEnd;
using SqlSource.Testing;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.EndToEnd;

// The sidecars committed beside the end-to-end queries and the realistic cases are what the server answers today:
// "describe --check" on the project, with the real MSBuild and a connection to each database, finds every entry the
// same as its sidecar's.  The counts it prints are those of the entries the committed sidecars hold for each database.
// It is in the collection UsesPostgis, which has both containers, since "gis" is on the PostGIS container alone and the
// project's other databases on the PostgreSQL one.
[Collection(UsesPostgis.Name)]
public sealed class CheckTests(PostgresDatabases postgres, PostgisDatabases postgis)
{
    // The number of entries of the sidecars under EndToEnd/ and Realistic/Cases/, by the database each was described
    // against.
    private static int ProjectEntries(string database) =>
        CommittedEntries.Count(
            new[] { RepositoryPaths.EndToEnd, RealisticCases.Corpus.Cases }.SelectMany(static folder =>
                Directory.EnumerateFiles(folder, "*.sql.json", SearchOption.AllDirectories)
            ),
            database
        );

    [Fact]
    public async Task Check_Project_EveryEntryIsTheSame()
    {
        var app = ProjectEntries(PostgresDatabases.AppDatabase);
        var billing = ProjectEntries(PostgresDatabases.BillingDatabase);
        var realistic = ProjectEntries(PostgresDatabases.RealisticDatabase);
        var gis = ProjectEntries(PostgisDatabases.GisDatabase);
        app.ShouldBeGreaterThan(0);
        billing.ShouldBeGreaterThan(0);
        realistic.ShouldBeGreaterThan(0);
        gis.ShouldBeGreaterThan(0);
        // In the tool's order, which puts "app" first, by the queries of EndToEnd/Json/, and then "gis".
        var appGisAndBilling =
            $"app (postgres): {app} same, 0 differ, 0 failed\n"
            + $"gis (postgres): {gis} same, 0 differ, 0 failed\n"
            + $"billing (postgres): {billing} same, 0 differ, 0 failed\n";
        string[] others =
        [
            .. Connection(PostgresDatabases.AppDatabase),
            .. Connection(PostgresDatabases.BillingDatabase),
            .. GisConnection(),
        ];

        var withModels = RealisticCases.Corpus.All.Where(static found => found.HasModels).ToList();
        var checkedHere = withModels.Where(found => found.IsCheckedOn(postgres.Major)).ToList();
        if (checkedHere.Count == withModels.Count)
        {
            // A run on the whole, with no --database and no file, so that it also reports a sidecar beside a file
            // none of whose queries needs an entry, which a filtered run never deletes and so never reports.
            var result = await CheckAsync([.. others, .. Connection(PostgresDatabases.RealisticDatabase)]);

            result.ExitCode.ShouldBe(0, result.Error);
            result.Out.ShouldBe(appGisAndBilling + $"realistic (postgres): {realistic} same, 0 differ, 0 failed\n");
            return;
        }

        // A server that leaves cases out, by their minimumMajor or their checkedFrom, gets two runs, since a filtered
        // check compares only what it names: the realistic cases that it checks, each named by its query.sql, and
        // the other databases whole, named by --database alone, so that none of their queries goes unchecked.  Only
        // such a server runs this path, every major below 18 today, of which CI runs 14 on every push and the nightly
        // run 15, 16 and 17: a local run on the default image does not exercise it, and neither run reports a sidecar
        // beside a file that needs none, which the run on the whole of the default image does.
        checkedHere.ShouldNotBeEmpty();
        var checkedEntries = CommittedEntries.Count(
            checkedHere.Select(static found => found.SidecarPath),
            PostgresDatabases.RealisticDatabase
        );
        checkedEntries.ShouldBeLessThan(realistic);
        var realisticResult = await CheckAsync([
            "--database",
            PostgresDatabases.RealisticDatabase,
            .. Connection(PostgresDatabases.RealisticDatabase),
            .. checkedHere.Select(static found => Path.Combine(found.Folder, RealisticCase.QueryFile)),
        ]);
        var othersResult = await CheckAsync([
            "--database",
            PostgresDatabases.AppDatabase,
            "--database",
            PostgresDatabases.BillingDatabase,
            "--database",
            PostgisDatabases.GisDatabase,
            .. others,
        ]);

        realisticResult.ExitCode.ShouldBe(0, realisticResult.Error);
        realisticResult.Out.ShouldBe($"realistic (postgres): {checkedEntries} same, 0 differ, 0 failed\n");
        othersResult.ExitCode.ShouldBe(0, othersResult.Error);
        othersResult.Out.ShouldBe(appGisAndBilling);
    }

    // "describe --check" over the real project, on the real MSBuild and the PostgreSQL describer.
    private static async Task<CliResult> CheckAsync(IEnumerable<string> args)
    {
        using var run = CliRun.Real(RepositoryRoot.WorkingFolder);
        run.Describers.Add(new PostgresDescriber());
        return await run.RunAsync(["describe", "--check", RepositoryPaths.EndToEndProject, .. args]);
    }

    private string[] Connection(string database) =>
        ["--connection", database + "=" + postgres.ConnectionStringOf(database)];

    // The connection of "gis", on the PostGIS container, whose parameters of PostGIS are bound through the tool's
    // Npgsql.NetTopologySuite.
    private string[] GisConnection() =>
        ["--connection", PostgisDatabases.GisDatabase + "=" + postgis.ConnectionStringOf(PostgisDatabases.GisDatabase)];
}
