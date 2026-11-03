using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// The runs of dotnet that FixtureProjects starts write MSBuild's own lines in English, which some tests read, whatever
// the language of the machine's interface.  The test process is given another language for the purpose, so the tests
// are of the collection that runs alone for that.
[Collection(nameof(ChangesTheEnvironment))]
public sealed class FixtureProjectsTests : IDisposable
{
    private const string Language = "DOTNET_CLI_UI_LANGUAGE";

    private readonly FixtureProjects _fixtures = new();
    private readonly string? _language = Environment.GetEnvironmentVariable(Language);

    public FixtureProjectsTests()
    {
        Environment.SetEnvironmentVariable(Language, "de");
    }

    public void Dispose()
    {
        Environment.SetEnvironmentVariable(Language, _language);
        _fixtures.Dispose();
    }

    [Fact]
    public async Task BuildAsync_MachineInAnotherLanguage_BuildsInEnglish()
    {
        var project = _fixtures.Copy("Online");

        var (exitCode, output) = await FixtureProjects.BuildAsync(
            project,
            new Dictionary<string, string>(),
            "-p:SqlSourceOnline=false"
        );

        exitCode.ShouldBe(0, output);
        output.ShouldContain("0 Warning(s)");
    }

    [Fact]
    public async Task RunMSBuildAsync_MachineInAnotherLanguage_RunsInEnglish()
    {
        var project = _fixtures.Copy("Online");

        var (exitCode, output) = await FixtureProjects.RunMSBuildAsync(
            project,
            ["-t:SqlSourceWriteManifest", "-tl:off", "-v:n"]
        );

        exitCode.ShouldBe(0, output);
        output.ShouldContain("Build succeeded.");
    }
}
