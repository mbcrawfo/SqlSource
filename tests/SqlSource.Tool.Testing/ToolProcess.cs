using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Testing;

// The tool that was built beside the tests, in a process of its own, by its real entry: the command line, the real
// "dotnet msbuild" and the registry of the released tool.
internal static class ToolProcess
{
    // Runs "describe" on the copy of a fixture project with the one --connection given ("name=connection string"),
    // through Programs.ToolStart, and checks the exit code is 0, nothing was written to the error stream, the output is
    // the one line given, and the sidecar of the .sql file holds one entry, of the query and the engine given.
    public static async Task DescribeOneQueryAsync(
        FixtureProjects fixtures,
        string project,
        string connection,
        string output,
        string sqlFile,
        string query,
        string engine
    )
    {
        var temp = Directory.CreateDirectory(fixtures.PathOf("tmp")).FullName;

        var result = await Programs.RunAsync(
            Programs.ToolStart(fixtures.Root, temp, "describe", project, "--connection", connection),
            Eventually.Patience
        );

        result.ExitCode.ShouldBe(0, result.Error);
        result.Error.ShouldBeEmpty();
        result.Output.ShouldBe(output + Environment.NewLine);
        var sidecar = SidecarFormat.PathFor(sqlFile);
        File.Exists(sidecar).ShouldBeTrue();
        var entry = SidecarReader
            .Read(await File.ReadAllTextAsync(sidecar, TestContext.Current.CancellationToken))
            .Sidecar.ShouldNotBeNull()
            .Queries.ShouldHaveSingleItem();
        entry.Name.ShouldBe(query);
        entry.Engine.ShouldBe(engine);
    }
}
