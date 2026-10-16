using System;
using Shouldly;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// What a case reads of its folder beside its three texts.
public sealed class FixtureCaseTests : IDisposable
{
    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    private FixtureCase Case() => new("postgres", "one", _folder.CreateFolder("postgres/one"));

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
}
