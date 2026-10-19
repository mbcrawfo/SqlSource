using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The sidecars committed beside the end-to-end queries are what the server answers today: "describe --check" on the
// project, with the real MSBuild and a connection to each database, finds every entry the same as its sidecar's.
[Collection(UsesPostgres.Name)]
public sealed class CheckTests(PostgresContainer postgres)
{
    [Fact]
    public async Task Check_Project_EveryEntryIsTheSame()
    {
        var result = await EndToEndTool.RunAsync(
            RepositoryPaths.Root,
            "describe",
            "--check",
            RepositoryPaths.EndToEndProject,
            "--connection",
            PostgresContainer.AppDatabase + "=" + postgres.ConnectionStringOf(PostgresContainer.AppDatabase),
            "--connection",
            PostgresContainer.BillingDatabase + "=" + postgres.ConnectionStringOf(PostgresContainer.BillingDatabase)
        );

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe(
            "app (postgres): 17 same, 0 differ, 0 failed\nbilling (postgres): 2 same, 0 differ, 0 failed\n"
        );
    }
}
