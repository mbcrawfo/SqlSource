using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot.Reading;
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
    private static int CommittedEntries(string database) =>
        new[] { RepositoryPaths.EndToEnd, RepositoryPaths.RealisticCases }
            .SelectMany(static folder => Directory.EnumerateFiles(folder, "*.sql.json", SearchOption.AllDirectories))
            .SelectMany(static path => SidecarReader.Read(File.ReadAllText(path)).Sidecar.ShouldNotBeNull(path).Queries)
            .Count(entry => entry.Database == database);

    [Fact]
    public async Task Check_Project_EveryEntryIsTheSame()
    {
        var app = CommittedEntries(PostgresContainer.AppDatabase);
        var billing = CommittedEntries(PostgresContainer.BillingDatabase);
        var realistic = CommittedEntries(PostgresContainer.RealisticDatabase);
        app.ShouldBeGreaterThan(0);
        billing.ShouldBeGreaterThan(0);
        realistic.ShouldBeGreaterThan(0);

        var result = await EndToEndTool.RunAsync(
            RepositoryPaths.Root,
            "describe",
            "--check",
            RepositoryPaths.EndToEndProject,
            "--connection",
            PostgresContainer.AppDatabase + "=" + postgres.ConnectionStringOf(PostgresContainer.AppDatabase),
            "--connection",
            PostgresContainer.BillingDatabase + "=" + postgres.ConnectionStringOf(PostgresContainer.BillingDatabase),
            "--connection",
            PostgresContainer.RealisticDatabase + "=" + postgres.RealisticConnectionString
        );

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe(
            $"app (postgres): {app} same, 0 differ, 0 failed\n"
                + $"billing (postgres): {billing} same, 0 differ, 0 failed\n"
                + $"realistic (postgres): {realistic} same, 0 differ, 0 failed\n"
        );
    }
}
