using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Testing.EndToEnd;
using SqlSource.Tests.SqlServer.Realistic;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The sidecars committed beside the end-to-end queries and the realistic cases are what the server answers today:
// "describe --check" on the project, with the real MSBuild and a connection to each database, finds every entry the
// same as its sidecar's.  The counts it prints are those of the entries the committed sidecars hold for each database.
[Collection(UsesSqlServer.Name)]
public sealed class CheckTests(SqlServerContainer sqlServer)
{
    // The sidecars under EndToEnd/ and Realistic/Cases/.
    private static IEnumerable<string> Sidecars() =>
        new[] { RepositoryPaths.EndToEnd, RealisticCases.Corpus.Cases }.SelectMany(static folder =>
            Directory.EnumerateFiles(folder, "*.sql.json", SearchOption.AllDirectories)
        );

    [Fact]
    public async Task Check_Project_EveryEntryIsTheSame()
    {
        var sidecars = Sidecars().ToList();
        var app = CommittedEntries.Count(sidecars, SqlServerContainer.AppDatabase);
        var billing = CommittedEntries.Count(sidecars, SqlServerContainer.BillingDatabase);
        var realistic = CommittedEntries.Count(sidecars, SqlServerContainer.RealisticDatabase);
        app.ShouldBeGreaterThan(0);
        billing.ShouldBeGreaterThan(0);
        realistic.ShouldBeGreaterThan(0);
        string[] connections =
        [
            .. Connection(SqlServerContainer.AppDatabase),
            .. Connection(SqlServerContainer.BillingDatabase),
            .. Connection(SqlServerContainer.RealisticDatabase),
        ];

        var withModels = RealisticCases.Corpus.All.Where(static found => found.HasModels).ToList();
        var checkedHere = withModels.Where(found => found.IsCheckedOn(sqlServer.Major)).ToList();
        if (sqlServer.Major >= 17 && checkedHere.Count == withModels.Count)
        {
            var result = await CheckAsync(connections);

            result.ExitCode.ShouldBe(0, result.Error);
            result.Out.ShouldBe(
                $"app (mssql): {app} same, 0 differ, 0 failed\n"
                    + $"billing (mssql): {billing} same, 0 differ, 0 failed\n"
                    + $"realistic (mssql): {realistic} same, 0 differ, 0 failed\n"
            );
            return;
        }

        // A server before major 17 cannot describe NewTypes.sql, whose table is not made there, and no server
        // describes as their sidecars hold the realistic cases that a minimumMajor or a checkedFrom leaves out on it.
        // A filtered check compares only what it names, so the run names every other query file of EndToEnd/Queries/
        // and the query.sql of each realistic case that it checks, by their paths.  A "--database" filter would take
        // the files it leaves out in with the rest.  SQL Server 2017, which CI runs, and 2019 and 2022, which the
        // nightly run runs, take this path: a local run on the default image does not exercise it.
        var leavesOutNewTypes = sqlServer.Major < 17;
        var newTypes = CommittedEntries.Count(
            [RepositoryPaths.NewTypesQueries + ".json"],
            SqlServerContainer.AppDatabase
        );
        newTypes.ShouldBeGreaterThan(0);
        checkedHere.ShouldNotBeEmpty();
        // The named files include Constants.sql, whose only query has no sidecar and counts nothing; NewTypes.sql is
        // the one file of EndToEnd/Queries/ that only 2025 describes.
        var named = Directory
            .EnumerateFiles(RepositoryPaths.EndToEndQueries, "*.sql", SearchOption.AllDirectories)
            .Where(path => !leavesOutNewTypes || path != RepositoryPaths.NewTypesQueries)
            .Order()
            .Concat(checkedHere.Select(static found => Path.Combine(found.Folder, RealisticCase.QueryFile)))
            .ToList();
        var namedSidecars = named.Select(static path => path + ".json").Where(File.Exists).ToList();
        var checkedApp = CommittedEntries.Count(namedSidecars, SqlServerContainer.AppDatabase);
        var checkedBilling = CommittedEntries.Count(namedSidecars, SqlServerContainer.BillingDatabase);
        var checkedRealistic = CommittedEntries.Count(namedSidecars, SqlServerContainer.RealisticDatabase);
        checkedApp.ShouldBe(leavesOutNewTypes ? app - newTypes : app);
        checkedBilling.ShouldBe(billing);
        checkedRealistic.ShouldBe(checkedHere.Count);
        checkedRealistic.ShouldBeLessThan(realistic);

        var floor = await CheckAsync([.. connections, .. named]);

        floor.ExitCode.ShouldBe(0, floor.Error);
        floor.Out.ShouldBe(
            $"app (mssql): {checkedApp} same, 0 differ, 0 failed\n"
                + $"billing (mssql): {checkedBilling} same, 0 differ, 0 failed\n"
                + $"realistic (mssql): {checkedRealistic} same, 0 differ, 0 failed\n"
        );
    }

    // "describe --check" over the real project, on the real MSBuild and the describers of the released tool.
    private static async Task<CliResult> CheckAsync(IEnumerable<string> args)
    {
        using var run = CliRun.Real(RepositoryRoot.WorkingFolder);
        run.ReleasedDescribers = true;
        return await run.RunAsync(["describe", "--check", RepositoryPaths.EndToEndProject, .. args]);
    }

    private string[] Connection(string database) =>
        ["--connection", database + "=" + sqlServer.ConnectionStringOf(database)];
}
