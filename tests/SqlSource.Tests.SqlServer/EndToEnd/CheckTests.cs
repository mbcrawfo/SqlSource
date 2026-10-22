using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.SqlServer.EndToEnd;

// The sidecars committed beside the end-to-end queries are what the server answers today: "describe --check" on the
// project, with the real MSBuild and a connection to each database, finds every entry the same as its sidecar's.  The
// counts it prints are those of the entries the committed sidecars hold for each database.
[Collection(UsesSqlServer.Name)]
public sealed class CheckTests(SqlServerContainer sqlServer)
{
    // The sidecars under EndToEnd/.
    private static IEnumerable<string> Sidecars() =>
        Directory.EnumerateFiles(RepositoryPaths.EndToEnd, "*.sql.json", SearchOption.AllDirectories);

    [Fact]
    public async Task Check_Project_EveryEntryIsTheSame()
    {
        var sidecars = Sidecars().ToList();
        var app = CommittedEntries.Count(sidecars, SqlServerContainer.AppDatabase);
        var billing = CommittedEntries.Count(sidecars, SqlServerContainer.BillingDatabase);
        app.ShouldBeGreaterThan(0);
        billing.ShouldBeGreaterThan(0);
        string[] connections =
        [
            .. Connection(SqlServerContainer.AppDatabase),
            .. Connection(SqlServerContainer.BillingDatabase),
        ];

        if (sqlServer.Major >= 17)
        {
            var result = await CheckAsync(connections);

            result.ExitCode.ShouldBe(0, result.Error);
            result.Out.ShouldBe(
                $"app (mssql): {app} same, 0 differ, 0 failed\nbilling (mssql): {billing} same, 0 differ, 0 failed\n"
            );
            return;
        }

        // A server before major 17 cannot describe NewTypes.sql, whose table is not made there, and a filtered check
        // compares only what it names, so the run names every other query file of EndToEnd/Queries/ by its path and
        // none of the 2025-only file.  A "--database" filter would take the file in with the rest of "app".
        var newTypes = CommittedEntries.Count(
            [RepositoryPaths.NewTypesQueries + ".json"],
            SqlServerContainer.AppDatabase
        );
        newTypes.ShouldBeGreaterThan(0);
        // The named files include Constants.sql, whose only query has no sidecar and counts nothing; NewTypes.sql is
        // the one file that only 2025 describes.
        var named = Directory
            .EnumerateFiles(RepositoryPaths.EndToEndQueries, "*.sql", SearchOption.AllDirectories)
            .Where(static path => path != RepositoryPaths.NewTypesQueries)
            .Order()
            .ToList();
        var namedSidecars = named.Select(static path => path + ".json").Where(File.Exists).ToList();
        var checkedApp = CommittedEntries.Count(namedSidecars, SqlServerContainer.AppDatabase);
        var checkedBilling = CommittedEntries.Count(namedSidecars, SqlServerContainer.BillingDatabase);
        checkedApp.ShouldBe(app - newTypes);
        checkedBilling.ShouldBe(billing);

        var floor = await CheckAsync([.. connections, .. named]);

        floor.ExitCode.ShouldBe(0, floor.Error);
        floor.Out.ShouldBe(
            $"app (mssql): {checkedApp} same, 0 differ, 0 failed\n"
                + $"billing (mssql): {checkedBilling} same, 0 differ, 0 failed\n"
        );
    }

    // "describe --check" over the real project, on the real MSBuild and the describers of the released tool.
    private static async Task<CliResult> CheckAsync(IEnumerable<string> args)
    {
        using var run = CliRun.Real(RepositoryPaths.Root);
        run.ReleasedDescribers = true;
        return await run.RunAsync(["describe", "--check", RepositoryPaths.EndToEndProject, .. args]);
    }

    private string[] Connection(string database) =>
        ["--connection", database + "=" + sqlServer.ConnectionStringOf(database)];
}
