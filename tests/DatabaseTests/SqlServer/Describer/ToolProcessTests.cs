using System;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// The tool that was built beside the tests, in a process of its own, by its real entry: the command line, the real
// "dotnet msbuild" and the registry of the released tool, against the container.  SqlClient is the tests' copy, which
// the tests' deps file names (Programs.ToolStart); tools/check-tool-install.sh shows that the packed tool loads its
// own.  The project is a copy of the fixture SqlServer, which tests/SqlSource.Tool.Tests owns.
[Collection(UsesSqlServer.Name)]
public sealed class ToolProcessTests(SqlServerContainer sqlServer) : IDisposable
{
    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    [Fact]
    public async Task Describe_BuiltToolAgainstTheContainer_WritesTheSidecar() =>
        await ToolProcess.ShouldDescribeOneQueryAsync(
            _fixtures,
            _fixtures.Copy("SqlServer"),
            SqlServerContainer.Database + "=" + sqlServer.ConnectionString,
            "fixtures (mssql): 1 described, 0 skipped, 0 failed",
            _fixtures.PathOf("SqlServer/Queries/Orders.sql"),
            "GetOrders",
            SidecarValues.Engine.SqlServer
        );
}
