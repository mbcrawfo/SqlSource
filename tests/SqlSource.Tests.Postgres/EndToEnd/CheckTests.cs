using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing.EndToEnd;
using SqlSource.Tests.Postgres.Realistic;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The sidecars committed beside the end-to-end queries and the realistic cases are what the server answers today:
// "describe --check" on the project, with the real MSBuild and a connection to each database, finds every entry the
// same as its sidecar's.  The counts it prints are those of the entries the committed sidecars hold for each database.
[Collection(UsesPostgres.Name)]
public sealed class CheckTests(PostgresContainer postgres)
{
    // The number of entries of the sidecars under EndToEnd/ and Realistic/Cases/, by the database each was described
    // against.
    private static int ProjectEntries(string database) =>
        CommittedEntries.Count(
            new[] { RepositoryPaths.EndToEnd, RepositoryPaths.RealisticCases }.SelectMany(static folder =>
                Directory.EnumerateFiles(folder, "*.sql.json", SearchOption.AllDirectories)
            ),
            database
        );

    [Fact]
    public async Task Check_Project_EveryEntryIsTheSame()
    {
        var app = ProjectEntries(PostgresContainer.AppDatabase);
        var billing = ProjectEntries(PostgresContainer.BillingDatabase);
        var realistic = ProjectEntries(PostgresContainer.RealisticDatabase);
        app.ShouldBeGreaterThan(0);
        billing.ShouldBeGreaterThan(0);
        realistic.ShouldBeGreaterThan(0);
        var others =
            $"app (postgres): {app} same, 0 differ, 0 failed\n"
            + $"billing (postgres): {billing} same, 0 differ, 0 failed\n";

        var withModels = RealisticCases.All.Where(static found => found.HasModels).ToList();
        var checkedHere = withModels.Where(found => found.IsCheckedOn(postgres.Major)).ToList();
        if (checkedHere.Count == withModels.Count)
        {
            var result = await CheckAsync([
                .. Connection(PostgresContainer.AppDatabase),
                .. Connection(PostgresContainer.BillingDatabase),
                .. Connection(PostgresContainer.RealisticDatabase),
            ]);

            result.ExitCode.ShouldBe(0, result.Error);
            result.Out.ShouldBe(others + $"realistic (postgres): {realistic} same, 0 differ, 0 failed\n");
            return;
        }

        // A server that leaves cases out, by their minimumMajor or their checkedFrom, gets two runs, since a filtered
        // check compares only what it names: the realistic cases that it checks, each named by its query.sql, and
        // the other databases whole, named by --database alone, so that none of their queries goes unchecked.  Only
        // such a server runs this path, PostgreSQL 14 today, which CI runs: a local run on the default image does not
        // exercise it.
        checkedHere.ShouldNotBeEmpty();
        var checkedEntries = CommittedEntries.Count(
            checkedHere.Select(static found => found.SidecarPath),
            PostgresContainer.RealisticDatabase
        );
        checkedEntries.ShouldBeLessThan(realistic);
        var realisticResult = await CheckAsync([
            "--database",
            PostgresContainer.RealisticDatabase,
            .. Connection(PostgresContainer.RealisticDatabase),
            .. checkedHere.Select(static found => Path.Combine(found.Folder, RealisticCase.QueryFile)),
        ]);
        var othersResult = await CheckAsync([
            "--database",
            PostgresContainer.AppDatabase,
            "--database",
            PostgresContainer.BillingDatabase,
            .. Connection(PostgresContainer.AppDatabase),
            .. Connection(PostgresContainer.BillingDatabase),
        ]);

        realisticResult.ExitCode.ShouldBe(0, realisticResult.Error);
        realisticResult.Out.ShouldBe($"realistic (postgres): {checkedEntries} same, 0 differ, 0 failed\n");
        othersResult.ExitCode.ShouldBe(0, othersResult.Error);
        othersResult.Out.ShouldBe(others);
    }

    // "describe --check" over the real project, on the real MSBuild and the PostgreSQL describer.
    private static async Task<CliResult> CheckAsync(IEnumerable<string> args)
    {
        using var run = CliRun.Real(RepositoryPaths.Root);
        run.Describers.Add(new PostgresDescriber());
        return await run.RunAsync(["describe", "--check", RepositoryPaths.EndToEndProject, .. args]);
    }

    private string[] Connection(string database) =>
        ["--connection", database + "=" + postgres.ConnectionStringOf(database)];
}
