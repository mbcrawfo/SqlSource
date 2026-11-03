using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using SqlSource.Tool.Watching;
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

    // The connection is in the .env file beside the project alone, in double quotes with the backslash and the quote
    // escaped, which a connection string may hold.
    [Fact]
    public async Task Describe_ConnectionInADotEnvFileAlone_WritesItsSidecar()
    {
        var project = _fixtures.Copy("SqlServer");
        var connection = sqlServer
            .ConnectionString.Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("\"", "\\\"", StringComparison.Ordinal);
        _ = _fixtures.WriteFile("SqlServer/.env", $"SQLSOURCE_CONNECTION_FIXTURES=\"{connection}\"\n");
        using var run = CliRun.Real(Path.GetDirectoryName(project)!);
        run.ReleasedDescribers = true;

        var result = await run.RunAsync("describe", project);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Out.ShouldBe("fixtures (mssql): 1 described, 0 skipped, 0 failed\n");
        var entry = SidecarFiles.Read(_fixtures.PathOf("SqlServer/Queries/Orders.sql")).Queries.ShouldHaveSingleItem();
        entry.Name.ShouldBe("GetOrders");
        entry.Engine.ShouldBe(SidecarValues.Engine.SqlServer);
    }

    // The real watcher and the real clock over a copy of the project, on the real MSBuild: Orders.sql is saved with
    // another column, and its sidecar says so.  The copy is under the temporary folder, where a test's host watches.
    [Fact]
    public async Task Watch_SqlFileChanged_RewritesItsSidecar()
    {
        var project = _fixtures.Copy("SqlServer");
        var orders = _fixtures.PathOf("SqlServer/Queries/Orders.sql");
        using var run = CliRun.Real(Path.GetDirectoryName(project)!);
        run.ReleasedDescribers = true;
        run.Time = TimeProvider.System;
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var running = run.Start(
            stop.Token,
            "describe",
            project,
            "--watch",
            "--connection",
            SqlServerDatabases.Database + "=" + sqlServer.ConnectionString
        );
        try
        {
            await Eventually.TrueAsync(
                () => running.Output.Contains(WatchSession.WatchingMarker, StringComparison.Ordinal),
                "The watch did not finish its first pass.  It wrote: " + running.Output + running.Errors
            );
            SidecarColumns(orders).ShouldBe(["id", "total", "note"]);

            await File.WriteAllTextAsync(
                orders,
                "-- name: GetOrders\nSELECT id, total, note, user_id FROM dbo.orders WHERE user_id = @userId;\n",
                TestContext.Current.CancellationToken
            );

            await Eventually.TrueAsync(
                () => SidecarColumns(orders).SequenceEqual(["id", "total", "note", "user_id"]),
                "The watch did not rewrite the sidecar.  It wrote: " + running.Output + running.Errors
            );
        }
        finally
        {
            await stop.CancelAsync();
        }

        var result = await Eventually.WithinAsync(running.Ended, "The watch did not end when it was stopped.");
        result.ExitCode.ShouldBe(0, result.Error);
    }

    // The names of the columns of the sidecar's one entry, or none while the sidecar is being replaced.
    private static string[] SidecarColumns(string sql)
    {
        try
        {
            return [.. SidecarFiles.Read(sql).Queries.Single().Columns?.Select(static column => column.Name) ?? []];
        }
        catch (IOException)
        {
            return [];
        }
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
