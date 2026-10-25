using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing.Realistic;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.Tool.Tests.Realistic;

// What a case says of itself: the majors it runs on, the files of its folder, and whether --check, the run through
// Dapper and the run through its method take it on a major.
public sealed class RealisticCaseTests
{
    [Theory]
    [InlineData(null, 14, true)]
    [InlineData(17, 14, false)]
    [InlineData(17, 17, true)]
    [InlineData(17, 18, true)]
    public void RunsOn_MinimumMajorAndServer_IsWhetherTheCaseRuns(int? minimum, int major, bool runs) =>
        (Case() with { MinimumMajor = minimum }).RunsOn(major).ShouldBe(runs);

    [Fact]
    public void Files_Case_AreTheQueryTheTruthAndTheSidecar() =>
        Case().Files.ShouldBe(["query.sql", "case.json", "query.sql.json"]);

    // A case the describer refuses has no entry to write, and one the generator refuses is claimed by no class.
    [Theory]
    [InlineData("SQLSRC229", null)]
    [InlineData(null, "SQLSRC255")]
    public void Files_RefusedOrModelRefusedCase_HasNoSidecar(string? refusal, string? models)
    {
        var found = Case() with { Refusal = refusal, Models = models is null ? null : new ModelRefusal(models, "w") };

        found.HasModels.ShouldBeFalse();
        found.Files.ShouldNotContain(RealisticCase.SidecarFile);
    }

    // A case with a parameter that Dapper cannot send is left out of the run through Dapper, as is one that the
    // server does not run.
    [Theory]
    [InlineData(null, null, 14, true)]
    [InlineData(null, "w", 14, false)]
    [InlineData(17, null, 14, false)]
    [InlineData(17, null, 18, true)]
    public void IsReadThroughDapperOn_CaseAndServer_IsWhetherTheRunReadsIt(
        int? minimum,
        string? dapper,
        int major,
        bool expected
    ) => (Case() with { MinimumMajor = minimum, Dapper = dapper }).IsReadThroughDapperOn(major).ShouldBe(expected);

    // A refused case has no models to read rows into.
    [Theory]
    [InlineData("SQLSRC229", null)]
    [InlineData(null, "SQLSRC255")]
    public void IsReadThroughDapperOn_RefusedOrModelRefusedCase_IsNever(string? refusal, string? models)
    {
        var found = Case() with { Refusal = refusal, Models = models is null ? null : new ModelRefusal(models, "w") };

        RealisticTestCases.Postgres.RecordedMajors.ShouldAllBe(major => !found.IsReadThroughDapperOn(major));
    }

    // A case runs through its method whether or not Dapper can send its parameters, and is left out only where the
    // server does not run it.
    [Theory]
    [InlineData(null, null, 14, true)]
    [InlineData(null, "w", 14, true)]
    [InlineData(17, null, 14, false)]
    [InlineData(17, "w", 18, true)]
    public void IsReadThroughMethodOn_CaseAndServer_IsWhetherTheRunReadsIt(
        int? minimum,
        string? dapper,
        int major,
        bool expected
    ) => (Case() with { MinimumMajor = minimum, Dapper = dapper }).IsReadThroughMethodOn(major).ShouldBe(expected);

    // A refused case has no method to call.
    [Theory]
    [InlineData("SQLSRC229", null)]
    [InlineData(null, "SQLSRC255")]
    public void IsReadThroughMethodOn_RefusedOrModelRefusedCase_IsNever(string? refusal, string? models)
    {
        var found = Case() with { Refusal = refusal, Models = models is null ? null : new ModelRefusal(models, "w") };

        RealisticTestCases.Postgres.RecordedMajors.ShouldAllBe(major => !found.IsReadThroughMethodOn(major));
    }

    // A case whose sidecar an earlier major cannot describe as recorded is left out of --check there, and out of the
    // comparison with its committed entry, as is one that the server does not run.
    [Theory]
    [InlineData(null, null, 14, true)]
    [InlineData(17, null, 14, false)]
    [InlineData(17, null, 18, true)]
    [InlineData(null, 18, 14, false)]
    [InlineData(null, 18, 18, true)]
    public void IsCheckedOn_CaseAndServer_IsWhetherCheckComparesIt(
        int? minimum,
        int? checkedFrom,
        int major,
        bool expected
    ) =>
        (
            Case() with
            {
                MinimumMajor = minimum,
                CheckedFrom = checkedFrom is { } from ? new CheckedFrom(from, "w") : null,
            }
        )
            .IsCheckedOn(major)
            .ShouldBe(expected);

    // A refused case has no sidecar, so --check has nothing of it to compare on any major.
    [Theory]
    [InlineData("SQLSRC229", null)]
    [InlineData(null, "SQLSRC255")]
    public void IsCheckedOn_RefusedOrModelRefusedCase_IsNever(string? refusal, string? models)
    {
        var found = Case() with { Refusal = refusal, Models = models is null ? null : new ModelRefusal(models, "w") };

        RealisticTestCases.Postgres.RecordedMajors.ShouldAllBe(major => !found.IsCheckedOn(major));
    }

    [Fact]
    public void SkipUnlessRunsOn_ServerBeforeTheMinimum_SkipsNamingTheEnginesProduct()
    {
        var found = Case() with { Engine = RealisticTestCases.SqlServer, MinimumMajor = 16 };

        Should
            .Throw<SkipException>(() => found.SkipUnlessRunsOn(14))
            .Message.ShouldEndWith("realistic/keyed needs SQL Server 16 or later, and the server is 14.");
    }

    [Fact]
    public void SkipUnlessRunsOn_ServerOfTheMinimum_DoesNotSkip() =>
        Should.NotThrow(() => (Case() with { MinimumMajor = 16 }).SkipUnlessRunsOn(16));

    [Fact]
    public void AsFixture_Case_IsOfTheEnginesName() =>
        (Case() with { Engine = RealisticTestCases.SqlServer }).AsFixture.Engine.ShouldBe(
            SidecarValues.Engine.SqlServer
        );

    private static RealisticCase Case() => RealisticTestCases.Keyed();
}
