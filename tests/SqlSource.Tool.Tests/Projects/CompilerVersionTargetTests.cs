using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Projects;

// The target SqlSourceCheckCompilerVersion of build/SqlSource.targets, run by name with the real "dotnet msbuild" on
// the fixture Single, with the compiler's version given as the global property the SDK sets it to.  Each test starts
// MSBuild once, which takes a second or two.
public sealed class CompilerVersionTargetTests : IDisposable
{
    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // The generator cannot load in a compiler before Roslyn 4.14, so the build stops, and the message names the version
    // it found and the first SDK and Visual Studio that have a compiler it loads in.
    [Theory]
    [InlineData("roslyn4.8")]
    [InlineData("roslyn4.9")]
    [InlineData("roslyn4.11")]
    [InlineData("roslyn4.13")]
    public async Task Run_CompilerOlderThan414_FailsWithSqlsrc021(string value)
    {
        var (exitCode, output) = await RunAsync(value);

        exitCode.ShouldNotBe(0, output);
        output.ShouldContain("error SQLSRC021");
        output.ShouldContain(value[6..]);
        output.ShouldContain("9.0.300");
        output.ShouldContain("17.14");
    }

    // A version that is 4.14 or later passes, and so does a value the target cannot read as a version: the build then
    // does not know the compiler, and the generator reports a compiler it cannot load in as it always did.
    [Theory]
    [InlineData("roslyn4.14")]
    [InlineData("roslyn5.0")]
    [InlineData("roslyn5.9")]
    [InlineData("roslyn10.0")]
    [InlineData("")]
    [InlineData("roslyn")]
    [InlineData("csc4.8")]
    [InlineData("roslyn4.x")]
    public async Task Run_CompilerAtLeast414OrUnknown_Passes(string value)
    {
        var (exitCode, output) = await RunAsync(value);

        exitCode.ShouldBe(0, output);
        output.ShouldNotContain("SQLSRC021");
    }

    private async Task<(int ExitCode, string Output)> RunAsync(string compilerApiVersion) =>
        await FixtureProjects.RunMSBuildAsync(
            _fixtures.Copy("Single"),
            ["-t:SqlSourceCheckCompilerVersion", $"-p:CompilerApiVersion={compilerApiVersion}"]
        );
}
