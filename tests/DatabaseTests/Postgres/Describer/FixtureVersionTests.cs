using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres.Describer;

// The fixture cases of the PostgreSQL describer were recorded against the default image: the capture.json and the
// expected.json of each carry its version.  FixtureCasesTests compares a live entry with its expected.json whatever
// the version, because CI describes the cases on the oldest supported server too; this is what fails when the
// default image moves and the cases are not recorded again.  It needs no server.
public sealed partial class FixtureVersionTests
{
    private static readonly IReadOnlyList<FixtureCase> Found = FixtureCases.InFolder(RepositoryPaths.DescriberFixtures);

    public static TheoryData<string> Cases => [with(Found.Select(static found => found.Name))];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Recorded_Case_IsOfTheVersionOfTheDefaultImage(string name)
    {
        var fixture = Found.Single(found => found.Name == name);
        var version = EngineImage.TagOf(DefaultImages.Postgres);

        VersionsIn(fixture.Capture).ShouldBe([version], "the capture.json");
        VersionsIn(fixture.Expected).ShouldBe([version], "the expected.json");
    }

    private static string[] VersionsIn(string json) =>
        [.. ServerVersion().Matches(json).Select(static match => match.Groups[1].Value).Distinct()];

    // As CaptureJson and SidecarWriter write the key.
    [GeneratedRegex("\"serverVersion\": \"([^\"]*)\"")]
    private static partial Regex ServerVersion();
}
