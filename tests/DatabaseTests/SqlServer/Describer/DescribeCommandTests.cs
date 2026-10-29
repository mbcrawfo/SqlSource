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

    // A synonym's column has the synonym's table as its origin.  The query, its hash and its column's type stay the
    // same, but a column type that names the first table would no longer apply, so the origin is what differs.  The
    // query reads a column of two, so that it matches no table, whose name would differ first.
    [Fact]
    public async Task Check_SynonymPointedAtAnotherTable_DiffersInTheOrigin()
    {
        await sqlServer.ExecuteAsync(
            "CREATE TABLE dbo.command_first (id int NOT NULL PRIMARY KEY, note nvarchar(10) NULL); "
                + "CREATE TABLE dbo.command_second (id int NOT NULL PRIMARY KEY, note nvarchar(10) NULL); "
                + "CREATE SYNONYM dbo.command_alias FOR dbo.command_first;"
        );
        try
        {
            var project = _fixtures.Copy("SqlServer");
            _ = _fixtures.WriteFile(
                "SqlServer/Queries/Aliased.sql",
                "-- name: GetAliased\nSELECT id FROM dbo.command_alias;\n"
            );
            var described = await DescribeAsync(project);
            described.ExitCode.ShouldBe(0, described.Error);
            await sqlServer.ExecuteAsync(
                "DROP SYNONYM dbo.command_alias; CREATE SYNONYM dbo.command_alias FOR dbo.command_second;"
            );

            var check = await DescribeAsync(project, "--check");

            check.ExitCode.ShouldBe(2, check.Error);
            check.Error.ShouldContain(
                "'GetAliased': columns[0].origin.table is \"command_first\", and the database says \"command_second\""
            );
            check.Out.ShouldBe("fixtures (mssql): 1 same, 1 differ, 0 failed\n");
        }
        finally
        {
            await sqlServer.ExecuteAsync(
                "DROP SYNONYM IF EXISTS dbo.command_alias; DROP TABLE IF EXISTS dbo.command_first; "
                    + "DROP TABLE IF EXISTS dbo.command_second;"
            );
        }
    }
}
