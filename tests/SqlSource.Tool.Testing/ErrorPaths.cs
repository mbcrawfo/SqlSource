using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Testing;

// What an engine gives the error paths: the table the query reads, its statements, and how the run describes.
// Table is the name the DROP uses.  CreateTable makes the table (an integer key "id" and a text column "name" that is
// not null), and AddColumn adds a column "note" to it, each a whole statement.  MissingTable names a table the
// database does not have.  ExecuteAsync runs a statement on the database "fixtures", and UseDescribers sets the
// describers of the real run.
internal sealed record ErrorPathEngine(
    string Table,
    string CreateTable,
    string AddColumn,
    string MissingTable,
    Func<string, Task> ExecuteAsync,
    Action<CliRun> UseDescribers
);

// The tool on a scratch project, a copy of the fixture ErrorPaths of the engine's test project, with the real MSBuild:
// a sidecar that no longer matches the query or the server is out of date under "--check", and a query that the
// server refuses is reported with no sidecar written for it.  Each test makes the table of the query and drops it
// again.
internal sealed class ErrorPaths(ErrorPathEngine engine, string connection) : IDisposable
{
    private const string Logical = "errors";

    private readonly FixtureProjects _projects = new();

    public void Dispose() => _projects.Dispose();

    // Describes, edits the query, and finds the sidecar out of date.
    public async Task ShouldFindTheEditedQueryOutOfDateAsync()
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

    // Describes, adds a column to the table, and finds the sidecar out of date.
    public async Task ShouldFindTheAddedColumnOutOfDateAsync()
    {
        var project = _projects.Copy("ErrorPaths");
        await WithWidgetsTableAsync(async () =>
        {
            var described = await DescribeAsync(project);
            described.ExitCode.ShouldBe(0, described.Error);
            File.Exists(Path.Combine(Path.GetDirectoryName(project)!, "Widgets.sql.json")).ShouldBeTrue();
            await engine.ExecuteAsync(engine.AddColumn);

            var result = await DescribeAsync(project, check: true);

            result.ExitCode.ShouldBe(2, result.Out);
            result.Error.ShouldContain("SQLSRC219");
        });
    }

    // Describes a query on a table that is not there, and finds it reported with no sidecar.
    public async Task ShouldReportTheRefusedQueryAsync()
    {
        var project = _projects.Copy("ErrorPaths");
        var folder = Path.GetDirectoryName(project)!;
        var refused = Path.Combine(folder, "Refused.sql");
        await File.WriteAllTextAsync(
            refused,
            $"-- name: GetMissing\nSELECT id FROM {engine.MissingTable};\n",
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

    // "describe", with "--check" when asked, on the copy of the project and the connection of the logical database.
    // Runs on the real MSBuild and the describers the engine sets.
    private async Task<CliResult> DescribeAsync(string project, bool check = false)
    {
        using var run = CliRun.Real(Path.GetDirectoryName(project)!);
        engine.UseDescribers(run);
        var connectionArgument = Logical + "=" + connection;
        return await run.RunAsync(
            check
                ? ["describe", "--check", project, "--connection", connectionArgument]
                : ["describe", project, "--connection", connectionArgument]
        );
    }

    // Runs a test's body with the table of the query made, and drops it again however the body ends.
    private async Task WithWidgetsTableAsync(Func<Task> body)
    {
        await engine.ExecuteAsync(engine.CreateTable);
        try
        {
            await body();
        }
        finally
        {
            await engine.ExecuteAsync($"DROP TABLE IF EXISTS {engine.Table}");
        }
    }
}
