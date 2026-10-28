using System;
using System.Threading.Tasks;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// The tool that was built beside the tests, in a process of its own, by its real entry: the command line, the real
// "dotnet msbuild" and the registry of the released tool, against the container.  The project is a copy of the
// fixture Single, which tests/SqlSource.Tool.Tests owns.
[Collection(UsesPostgres.Name)]
public sealed class ToolProcessTests(PostgresDatabases postgres) : IDisposable
{
    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    [Fact]
    public async Task Describe_BuiltToolAgainstTheContainer_WritesTheSidecar()
    {
        var project = _fixtures.Copy("Single");
        // Single has no type that claims its queries.
        _ = _fixtures.WriteFile("Single/Queries/Queries.cs", Plans.Type);

        await ToolProcess.ShouldDescribeOneQueryAsync(
            _fixtures,
            project,
            "billing=" + postgres.ConnectionString,
            "billing (postgres): 1 described, 0 skipped, 0 failed",
            _fixtures.PathOf("Single/Queries/Users.sql"),
            "GetUser",
            "postgres"
        );
    }
}
