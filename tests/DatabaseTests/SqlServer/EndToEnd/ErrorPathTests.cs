using System;
using System.Threading.Tasks;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// The tool on a scratch project, with the real MSBuild: a sidecar that no longer matches the query or the server is
// out of date under "--check", and a query that the server refuses is reported with no sidecar written for it.  Each
// test makes the table of the query on the database "fixtures" and drops it again.  ErrorPaths of
// tests/SqlSource.Tool.Testing holds the steps, and this class the statements of SQL Server.
[Collection(UsesSqlServer.Name)]
public sealed class ErrorPathTests(SqlServerContainer sqlServer) : IDisposable
{
    private const string Table = "dbo.error_path_widgets";

    private readonly ErrorPaths _paths = new(
        new ErrorPathEngine(
            Table,
            $"CREATE TABLE {Table} (id int PRIMARY KEY, name nvarchar(50) NOT NULL)",
            $"ALTER TABLE {Table} ADD note nvarchar(50) NULL",
            "dbo.error_path_no_such_table",
            sqlServer.ExecuteAsync,
            // The describers of the released tool.
            static run =>
                run.ReleasedDescribers = true
        ),
        sqlServer.ConnectionString
    );

    public void Dispose() => _paths.Dispose();

    [Fact]
    public async Task Check_QueryEditedAfterDescribe_IsOutOfDate() =>
        await _paths.ShouldFindTheEditedQueryOutOfDateAsync();

    [Fact]
    public async Task Check_ColumnAddedAfterDescribe_IsOutOfDate() =>
        await _paths.ShouldFindTheAddedColumnOutOfDateAsync();

    [Fact]
    public async Task Describe_QueryTheServerRefuses_ReportsItAndWritesNoSidecar() =>
        await _paths.ShouldReportTheRefusedQueryAsync();
}
