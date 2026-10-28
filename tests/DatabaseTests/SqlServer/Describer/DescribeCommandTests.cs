using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// The whole "describe" command in process, as Cli.Run runs it, on the real MSBuild and with the describers of the
// released tool, against the container: a copy of the fixture project SqlServer, whose sqlsource.json gives its one
// file the dialect mssql and the database "fixtures", and a connection for it on the command line.
[Collection(UsesSqlServer.Name)]
public sealed class DescribeCommandTests(SqlServerDatabases sqlServer) : IDisposable
{
    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // "describe", with the arguments given, over the copy of the project.
    private async Task<CliResult> DescribeAsync(string project, params string[] args)
    {
        using var run = CliRun.Real(Path.GetDirectoryName(project)!);
        run.ReleasedDescribers = true;
        return await run.RunAsync([
            "describe",
            project,
            "--connection",
            SqlServerDatabases.Database + "=" + sqlServer.ConnectionString,
            .. args,
        ]);
    }

    [Fact]
    public async Task Describe_ProjectWithAnMssqlFile_WritesItsSidecar()
    {
        var project = _fixtures.Copy("SqlServer");

        var result = await DescribeAsync(project);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe("fixtures (mssql): 1 described, 0 skipped, 0 failed\n");
        var entry = SidecarFiles.Read(_fixtures.PathOf("SqlServer/Queries/Orders.sql")).Queries.ShouldHaveSingleItem();
        entry.Name.ShouldBe("GetOrders");
        entry.Engine.ShouldBe(SidecarValues.Engine.SqlServer);
        // dbo.orders has id and total NOT NULL and note NULL.
        var columns = entry.Columns.ShouldNotBeNull();
        columns
            .Select(static column => (column.Name, column.Nullable))
            .ShouldBe([("id", false), ("total", false), ("note", true)]);
    }

    [Fact]
    public async Task Check_AfterDescribe_IsTheSame()
    {
        var project = _fixtures.Copy("SqlServer");
        var described = await DescribeAsync(project);
        described.ExitCode.ShouldBe(0, described.Error);

        var result = await DescribeAsync(project, "--check");

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe("fixtures (mssql): 1 same, 0 differ, 0 failed\n");
    }
}
