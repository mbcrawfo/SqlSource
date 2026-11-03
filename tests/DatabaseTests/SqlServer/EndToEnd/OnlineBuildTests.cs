using System;
using System.Threading.Tasks;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.EndToEnd;

// "dotnet build" of a copy of ErrorPaths that goes online against the container: the build runs the tool beside the
// tests on the database "fixtures" and compiles the sidecar that the run created, and a query that the server refuses
// fails the build at its file.  Each test makes the table of the query and drops it again.  OnlineBuilds of
// tests/SqlSource.Tool.Testing holds the steps, and ErrorPathTests the statements of SQL Server.
[Collection(UsesSqlServer.Name)]
public sealed class OnlineBuildTests(SqlServerDatabases sqlServer) : IDisposable
{
    private readonly OnlineBuilds _builds = new(ErrorPathTests.Engine(sqlServer), sqlServer.ConnectionString);

    public void Dispose() => _builds.Dispose();

    [Fact]
    public async Task Build_OnlineWithNoSidecar_CompilesTheSidecarItsRunCreated() =>
        await _builds.ShouldCompileTheSidecarItCreatedAsync();

    [Fact]
    public async Task Build_OnlineWithAQueryTheServerRefuses_FailsAtTheQuery() =>
        await _builds.ShouldFailAtTheRefusedQueryAsync();
}
