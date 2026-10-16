using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// The tool that was built beside the tests, in a process of its own, by its real entry: the command line, the real
// "dotnet msbuild" and the registry of the released tool, against the container.  The project is a copy of the
// fixture Single, which tests/SqlSource.Tool.Tests owns.
[Collection(UsesPostgres.Name)]
public sealed class ToolProcessTests(PostgresContainer postgres) : IDisposable
{
    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    [Fact]
    public async Task Describe_BuiltToolAgainstTheContainer_WritesTheSidecar()
    {
        var token = TestContext.Current.CancellationToken;
        var project = _fixtures.Copy("Single");
        // Single has no type that claims its queries.
        _ = _fixtures.WriteFile("Single/Queries/Queries.cs", Plans.Type);
        var temp = Directory.CreateDirectory(_fixtures.PathOf("tmp")).FullName;

        using var tool = Programs.StartTool(
            _fixtures.Root,
            temp,
            "describe",
            project,
            "--connection",
            "billing=" + postgres.ConnectionString
        );
        try
        {
            var error = tool.StandardError.ReadToEndAsync(token);
            var output = tool.StandardOutput.ReadToEndAsync(token);
            await tool.WaitForExitAsync(token);

            tool.ExitCode.ShouldBe(0, await error);
            (await error).ShouldBeEmpty();
            (await output).ShouldBe("billing (postgres): 1 described, 0 skipped, 0 failed" + Environment.NewLine);
            var sidecar = SidecarFormat.PathFor(_fixtures.PathOf("Single/Queries/Users.sql"));
            File.Exists(sidecar).ShouldBeTrue();
            var entry = SidecarReader
                .Read(await File.ReadAllTextAsync(sidecar, token))
                .Sidecar.ShouldNotBeNull()
                .Queries.ShouldHaveSingleItem();
            entry.Name.ShouldBe("GetUser");
            entry.Engine.ShouldBe("postgres");
        }
        finally
        {
            if (!tool.HasExited)
            {
                tool.Kill(entireProcessTree: true);
            }
        }
    }
}
