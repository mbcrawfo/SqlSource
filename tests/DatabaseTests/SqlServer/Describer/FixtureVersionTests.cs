using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.DatabaseTests.SqlServer.Describer;

// The fixture cases of the SQL Server describer were recorded against the default image: the capture.json and the
// expected.json of each carry the build its server reports.  FixtureCasesTests compares a live entry with its
// expected.json whatever the version, because CI describes the cases on the oldest supported server too; this is
// what fails when the default image moves and the cases are not recorded again.  It needs no server.
public sealed partial class FixtureVersionTests
{
    private static readonly IReadOnlyList<FixtureCase> Found = FixtureCases.InFolder(
        DescriberFixtures.SqlServer.Folder
    );

    public static TheoryData<string> Cases => [with(Found.Select(static found => found.Name))];

    [Theory]
    [MemberData(nameof(Cases))]
    public void Recorded_Case_IsOfTheBuildOfTheDefaultImage(string name)
    {
        var fixture = Found.Single(found => found.Name == name);
        var build = SqlServerContainer.ImageBuilds[DefaultImages.SqlServer];

        VersionsIn(ProductVersion().Matches(fixture.Capture)).ShouldBe([build], "the capture.json");
        VersionsIn(ServerVersion().Matches(fixture.Expected)).ShouldBe([build], "the expected.json");
    }

    private static string[] VersionsIn(MatchCollection matches) =>
        [.. matches.Select(static match => match.Groups[1].Value).Distinct()];

    // As CaptureJson writes the key of the answer to the open call.
    [GeneratedRegex("\"productVersion\": \"([^\"]*)\"")]
    private static partial Regex ProductVersion();

    // As SidecarWriter writes the key.
    [GeneratedRegex("\"serverVersion\": \"([^\"]*)\"")]
    private static partial Regex ServerVersion();
}
