using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tests.Postgres.EndToEnd;

// The tool on a scratch project, with the real MSBuild: a sidecar that no longer matches the query or the server is
// out of date under "--check", and a query that the server refuses is reported with no sidecar written for it.  Each
// test makes the table of the query on the database "fixtures" and drops it again.
[Collection(UsesPostgres.Name)]
public sealed class ErrorPathTests(PostgresContainer postgres) : IDisposable
{
    private const string Logical = "errors";
    private const string Table = "error_path_widgets";

    private readonly FixtureProjects _projects = new();

    public void Dispose() => _projects.Dispose();

    private string ConnectionArgument => Logical + "=" + postgres.ConnectionString;

    // "describe", with "--check" when asked, on the copy of the project and the connection of the logical database.
    // Runs on the real MSBuild and the PostgreSQL describer.
    private async Task<CliResult> DescribeAsync(string project, bool check = false)
    {
        using var run = CliRun.Real(Path.GetDirectoryName(project)!);
        run.Describers.Add(new PostgresDescriber());
        return await run.RunAsync(
            check
                ? ["describe", "--check", project, "--connection", ConnectionArgument]
                : ["describe", project, "--connection", ConnectionArgument]
        );
    }

    // Runs a test's body with the table of the query made, and drops it again however the body ends.
    private async Task WithWidgetsTableAsync(Func<Task> body)
    {
        await postgres.ExecuteAsync($"CREATE TABLE {Table} (id integer PRIMARY KEY, name text NOT NULL)");
        try
        {
            await body();
        }
        finally
        {
            await postgres.ExecuteAsync($"DROP TABLE IF EXISTS {Table}");
        }
    }

    [Fact]
    public async Task Check_QueryEditedAfterDescribe_IsOutOfDate()
    {
        var project = _projects.Copy("ErrorPaths");
        var queries = Path.Combine(Path.GetDirectoryName(project)!, "Widgets.sql");
        await WithWidgetsTableAsync(async () =>
        {
            var described = await DescribeAsync(project);
            described.ExitCode.ShouldBe(0, described.Error);
            File.Exists(queries + ".json").ShouldBeTrue();
            var text = await File.ReadAllTextAsync(queries, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(
                queries,
                text.Replace("SELECT id, name", "SELECT id, name, name AS label", StringComparison.Ordinal),
                TestContext.Current.CancellationToken
            );

            var result = await DescribeAsync(project, check: true);

            result.ExitCode.ShouldBe(2, result.Out);
            result.Error.ShouldContain("SQLSRC219");
        });
    }

    [Fact]
    public async Task Check_ColumnAddedAfterDescribe_IsOutOfDate()
    {
        var project = _projects.Copy("ErrorPaths");
        await WithWidgetsTableAsync(async () =>
        {
            var described = await DescribeAsync(project);
            described.ExitCode.ShouldBe(0, described.Error);
            File.Exists(Path.Combine(Path.GetDirectoryName(project)!, "Widgets.sql.json")).ShouldBeTrue();
            await postgres.ExecuteAsync($"ALTER TABLE {Table} ADD COLUMN note text");

            var result = await DescribeAsync(project, check: true);

            result.ExitCode.ShouldBe(2, result.Out);
            result.Error.ShouldContain("SQLSRC219");
        });
    }

    [Fact]
    public async Task Describe_QueryTheServerRefuses_ReportsItAndWritesNoSidecar()
    {
        var project = _projects.Copy("ErrorPaths");
        var folder = Path.GetDirectoryName(project)!;
        var refused = Path.Combine(folder, "Refused.sql");
        await File.WriteAllTextAsync(
            refused,
            "-- name: GetMissing\nSELECT id FROM error_path_no_such_table;\n",
            TestContext.Current.CancellationToken
        );
        await WithWidgetsTableAsync(async () =>
        {
            var result = await DescribeAsync(project);

            result.ExitCode.ShouldBe(1, result.Out);
            result.Error.ShouldContain("SQLSRC225");
            File.Exists(refused + ".json").ShouldBeFalse();
        });
    }
}
