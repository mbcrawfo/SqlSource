using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

// The reading of the cases' folders: every folder of Cases is a case, a case.json that is wrong fails naming the
// case and the fault, a folder holds its query, its truth, the expectation of each major it is recorded on and its
// sidecar when it has models, and a case runs on the majors from its minimum on.
public sealed class RealisticCasesTests
{
    private const string KnownCase = "aggregate-cte-left-joined";

    private const string Query = "-- name: Q\nSELECT 1;\n";

    // macOS writes it into a folder that Finder shows, and .gitignore keeps it out of the repository.
    private const string FinderFile = ".DS_Store";

    // The cases of the corpus, by the names of their folders alone, so that a case.json that is wrong fails the rows
    // of its own case.  Every theory over the corpus takes them.
    public static TheoryData<string> Cases => [with(RealisticCases.Names)];

    [Fact]
    public void All_Cases_HoldAKnownCase() => RealisticCases.All.ShouldContain(found => found.Name == KnownCase);

    [Fact]
    public void NamesIn_FolderWithoutQuerySql_IsNamedAndNotSkipped()
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile("b/query.sql", Query);
        _ = temp.CreateFolder("a");

        RealisticCases.NamesIn(temp.Path).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void AllIn_FolderWithoutQuerySql_ThrowsNamingTheCase()
    {
        using var temp = new TempFolder();
        _ = temp.CreateFolder("queryless");

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCases.AllIn(temp.Path));

        thrown.Message.ShouldContain("realistic/queryless");
        thrown.Message.ShouldContain(RealisticCase.QueryFile);
    }

    // Each case is checked apart, so that a broken one fails its own row.
    [Theory]
    [MemberData(nameof(Cases))]
    public void Folder_Case_HoldsItsFilesAlone(string name)
    {
        var found = RealisticCases.Read(Path.Combine(RepositoryPaths.RealisticCases, name));

        Directory
            .GetFiles(found.Folder)
            .Select(Path.GetFileName)
            .Where(static file => file != FinderFile)
            .Order(StringComparer.Ordinal)
            .ShouldBe(found.Files.Order(StringComparer.Ordinal), $"{found} holds other files");
    }

    [Theory]
    [InlineData(null, 14, true)]
    [InlineData(17, 14, false)]
    [InlineData(17, 17, true)]
    [InlineData(17, 18, true)]
    public void RunsOn_MinimumMajorAndServer_IsWhetherTheCaseRuns(int? minimum, int major, bool runs) =>
        (Case() with { MinimumMajor = minimum }).RunsOn(major).ShouldBe(runs);

    [Theory]
    [InlineData(null, new[] { "query.sql", "case.json", "query.sql.json", "expected-pg14.txt", "expected-pg18.txt" })]
    [InlineData(17, new[] { "query.sql", "case.json", "query.sql.json", "expected-pg18.txt" })]
    public void Files_Case_AreTheQueryTheTruthTheSidecarAndAnExpectationOfEachRecordedMajorItRunsOn(
        int? minimum,
        string[] files
    ) => (Case() with { MinimumMajor = minimum }).Files.ShouldBe(files);

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

    [Fact]
    public void Read_FolderWithoutCaseJson_ThrowsNamingTheCase()
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile("lonely/query.sql", Query);

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCases.Read(temp.PathOf("lonely")));

        thrown.Message.ShouldContain("realistic/lonely");
        thrown.Message.ShouldContain(RealisticCase.CaseFile);
    }

    [Fact]
    public void Read_FolderWithoutQuerySql_ThrowsNamingTheCase()
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile(
            "queryless/case.json", /*lang=json,strict*/
            "{ \"columns\": [] }"
        );

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCases.Read(temp.PathOf("queryless")));

        thrown.Message.ShouldContain("realistic/queryless");
        thrown.Message.ShouldContain(RealisticCase.QueryFile);
    }

    [Fact]
    public void Read_SampleKeys_AreComparedIgnoringCase()
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile("cased/query.sql", Query);
        _ = temp.WriteFile(
            "cased/case.json", /*lang=json,strict*/
            "{ \"columns\": [], \"samples\": [ { \"Limit\": \"10\" } ] }"
        );

        var found = RealisticCases.Read(temp.PathOf("cased"));

        found.Samples[0]["limit"].ShouldBe("10");
        found.Samples[0]["LIMIT"].ShouldBe("10");
    }

    [Fact]
    public void Read_SampleThatNamesAParameterTwiceIgnoringCase_ThrowsNamingIt()
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile("twice/query.sql", Query);
        _ = temp.WriteFile(
            "twice/case.json", /*lang=json,strict*/
            "{ \"columns\": [], \"samples\": [ { \"limit\": \"10\", \"LIMIT\": \"20\" } ] }"
        );

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCases.Read(temp.PathOf("twice")));

        thrown.Message.ShouldContain("realistic/twice");
        thrown.Message.ShouldContain("@LIMIT twice");
    }

    [Fact]
    public void Read_CaseJsonThatDoesNotParse_ThrowsNamingTheCase()
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile("broken/query.sql", Query);
        _ = temp.WriteFile("broken/case.json", "{ \"columns\": [ { \"name\": ");

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCases.Read(temp.PathOf("broken")));

        thrown.Message.ShouldContain("realistic/broken");
        thrown.Message.ShouldContain(RealisticCase.CaseFile);
    }

    [Theory]
    [InlineData( /*lang=json,strict*/
        "{ \"refusal\": \"SQLSRC229\", \"columns\": [] }",
        "both"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"minimumMajor\": 17 }",
        "neither"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [ { \"name\": \"id\" } ] }",
        "nullable"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [ { \"name\": \"email\", \"nullable\": true } ] }",
        "why"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [ { \"name\": \"id\", \"nullabel\": false } ] }",
        "nullabel"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [ { \"name\": \"email\", \"nullable\": true, \"why\": \"w\", \"exempt\": \" \" } ] }",
        "exempt gives no reason"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [ { \"name\": \"id\", \"nullable\": false, \"exempt\": \"no row\" } ] }",
        "not nullable and has an exempt"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"refusal\": \"SQLSRC229\", \"models\": { \"refusal\": \"SQLSRC255\", \"why\": \"w\" } }",
        "both a refusal and a refusal of its models"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"models\": { \"refusal\": \" \", \"why\": \"w\" } }",
        "models gives no refusal"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"models\": { \"refusal\": \"SQLSRC255\", \"why\": \"\" } }",
        "models gives no why"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"checkedFrom\": { \"why\": \"w\" } }",
        "checkedFrom names no major"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"checkedFrom\": { \"major\": 15, \"why\": \"w\" } }",
        "checkedFrom names 15, which is not a recorded major"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"minimumMajor\": 18, \"columns\": [], \"checkedFrom\": { \"major\": 18, \"why\": \"w\" } }",
        "checkedFrom is not above its minimumMajor"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"checkedFrom\": { \"major\": 18, \"why\": \" \" } }",
        "checkedFrom gives no why"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"refusal\": \"SQLSRC229\", \"checkedFrom\": { \"major\": 18, \"why\": \"w\" } }",
        "checkedFrom on a case with no models"
    )]
    [InlineData(
        "{ \"columns\": [], \"models\": { \"refusal\": \"SQLSRC255\", \"why\": \"w\" },"
            + " \"checkedFrom\": { \"major\": 18, \"why\": \"w\" } }",
        "checkedFrom on a case with no models"
    )]
    public void Read_CaseJsonThatIsWrong_ThrowsNamingTheCaseAndTheFault(string json, string fault)
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile("wrong/query.sql", Query);
        _ = temp.WriteFile("wrong/case.json", json);

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCases.Read(temp.PathOf("wrong")));

        thrown.Message.ShouldContain("realistic/wrong");
        thrown.Message.ShouldContain(fault);
    }

    [Fact]
    public void Read_CaseWithModelRefusal_ReadsIt()
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile("refused/query.sql", Query);
        _ = temp.WriteFile(
            "refused/case.json", /*lang=json,strict*/
            "{ \"columns\": [], \"models\": { \"refusal\": \"SQLSRC255\", \"why\": \"two ids\" } }"
        );

        var found = RealisticCases.Read(temp.PathOf("refused"));

        found.Models.ShouldBe(new ModelRefusal("SQLSRC255", "two ids"));
        found.HasModels.ShouldBeFalse();
    }

    [Fact]
    public void Read_CaseWithCheckedFrom_ReadsIt()
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile("later/query.sql", Query);
        _ = temp.WriteFile(
            "later/case.json", /*lang=json,strict*/
            "{ \"columns\": [], \"checkedFrom\": { \"major\": 18, \"why\": \"a later plan\" } }"
        );

        RealisticCases.Read(temp.PathOf("later")).CheckedFrom.ShouldBe(new CheckedFrom(18, "a later plan"));
    }

    // A case whose sidecar an earlier major cannot describe as recorded is left out of --check there, as is one that
    // the server does not run.
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

        PostgresContainer.RecordedMajors.ShouldAllBe(major => !found.IsCheckedOn(major));
    }

    [Fact]
    public void Read_CaseWithoutSamples_HasOneEmptySet()
    {
        using var temp = new TempFolder();
        _ = temp.WriteFile("plain/query.sql", Query);
        _ = temp.WriteFile(
            "plain/case.json", /*lang=json,strict*/
            "{ \"columns\": [ { \"name\": \"one\", \"nullable\": false } ] }"
        );

        var found = RealisticCases.Read(temp.PathOf("plain"));

        found.Name.ShouldBe("plain");
        found.ToString().ShouldBe("realistic/plain");
        found.Columns.ShouldBe([new TruthColumn("one", false, null, null)]);
        found.Samples.Count.ShouldBe(1);
        found.Samples[0].ShouldBeEmpty();
    }

    private static RealisticCase Case() =>
        new("keyed", "keyed", null, null, [], [new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)]);
}
