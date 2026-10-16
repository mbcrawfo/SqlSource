using System;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// What a case reads of its folder beside its three texts.
public sealed class FixtureCaseTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private FixtureCase Case() =>
        new(SidecarValues.Engine.Postgres, "one", _folder.CreateFolder($"{SidecarValues.Engine.Postgres}/one"));

    [Fact]
    public void ExplainSettings_CaseWithoutTheFile_HasNone() => Case().ExplainSettings.ShouldBeEmpty();

    // One name=value on a line; an empty line and the spaces around a line are not settings.
    [Fact]
    public void ExplainSettings_CaseWithTheFile_HasEachLine()
    {
        _ = _folder.WriteFile(
            $"postgres/one/{FixtureCase.ExplainSettingsFile}",
            "enable_mergejoin=off\n  enable_nestloop=off  \n\n"
        );

        Case().ExplainSettings.ShouldBe(["enable_mergejoin=off", "enable_nestloop=off"]);
    }

    [Fact]
    public void Case_ItsThreeFiles_AreReadFromItsFolder()
    {
        var fixture = FixtureCaseFiles.Create(_folder.Path, SidecarValues.Engine.Postgres, "typed", "SELECT 1;\n");
        _ = _folder.WriteFile($"postgres/typed/{FixtureCase.CaptureFile}", "a capture");
        _ = _folder.WriteFile($"postgres/typed/{FixtureCase.ExpectedFile}", "a sidecar");

        fixture.Query.ShouldBe("SELECT 1;\n");
        fixture.Capture.ShouldBe("a capture");
        fixture.Expected.ShouldBe("a sidecar");
        fixture.ToString().ShouldBe("postgres/typed");
    }
}
