using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tests.Postgres.Realistic;

// The reading of the cases' folders: every folder of Cases is a case, a case.json that is wrong fails naming the
// case and the fault, a folder holds its query, its truth and the expectation of each major it is recorded on, and a
// case runs on the majors from its minimum on.
public sealed class RealisticCasesTests
{
    private const string FirstCase = "aggregate-cte-left-joined";

    private const string Query = "-- name: Q\nSELECT 1;\n";

    // macOS writes it into a folder that Finder shows, and .gitignore keeps it out of the repository.
    private const string FinderFile = ".DS_Store";

    // The cases of the corpus, by the names of their folders alone, so that a case.json that is wrong fails the rows
    // of its own case.  Every theory over the corpus takes them.
    public static TheoryData<string> Cases => [with(RealisticCases.Names)];

    [Fact]
    public void All_Cases_HoldTheFirstCase() => RealisticCases.All.ShouldContain(found => found.Name == FirstCase);

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
    public void Folder_Case_HoldsItsQueryItsTruthAndTheExpectationsOfItsRecordedMajorsAlone(string name)
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
    [InlineData(null, new[] { "query.sql", "case.json", "expected-pg14.txt", "expected-pg18.txt" })]
    [InlineData(17, new[] { "query.sql", "case.json", "expected-pg18.txt" })]
    public void Files_MinimumMajor_AreTheQueryTheTruthAndAnExpectationOfEachRecordedMajorItRunsOn(
        int? minimum,
        string[] files
    ) => (Case() with { MinimumMajor = minimum }).Files.ShouldBe(files);

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
