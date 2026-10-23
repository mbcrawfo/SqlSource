using System;
using Shouldly;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Realistic;
using Xunit;

namespace SqlSource.Tool.Tests.Realistic;

// The reading of the cases' folders: every folder of a corpus is a case, and a case.json that is wrong fails naming
// the case and the fault.  A checkedFrom is read against the engine's recorded majors, PostgreSQL's 14 and 18 unless
// a test says SQL Server's.
public sealed class RealisticCaseReaderTests
{
    private const string Query = "-- name: Q\nSELECT 1;\n";

    private static RealisticEngine Postgres => RealisticTestCases.Postgres;

    private static RealisticEngine SqlServer => RealisticTestCases.SqlServer;

    // A folder of the case of the name, which holds the query and the case.json given.  A test that leaves one out
    // gives null for it, and one that gives neither has an empty folder.  Gives the path of the folder.
    private static string CaseFolder(TempFolder temp, string name, string? query = Query, string? json = null)
    {
        var folder = temp.CreateFolder(name);
        if (query is not null)
        {
            _ = temp.WriteFile(name + "/query.sql", query);
        }

        if (json is not null)
        {
            _ = temp.WriteFile(name + "/case.json", json);
        }

        return folder;
    }

    [Fact]
    public void NamesIn_FolderWithoutQuerySql_IsNamedAndNotSkipped()
    {
        using var temp = new TempFolder();
        _ = CaseFolder(temp, "b");
        _ = CaseFolder(temp, "a", query: null);

        RealisticCaseReader.NamesIn(temp.Path).ShouldBe(["a", "b"]);
    }

    [Fact]
    public void AllIn_FolderWithoutQuerySql_ThrowsNamingTheCase()
    {
        using var temp = new TempFolder();
        _ = CaseFolder(temp, "queryless", query: null);

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCaseReader.AllIn(temp.Path, Postgres));

        thrown.Message.ShouldContain("realistic/queryless");
        thrown.Message.ShouldContain(RealisticCase.QueryFile);
    }

    [Fact]
    public void Read_FolderWithoutCaseJson_ThrowsNamingTheCase()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(temp, "lonely");

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCaseReader.Read(folder, Postgres));

        thrown.Message.ShouldContain("realistic/lonely");
        thrown.Message.ShouldContain(RealisticCase.CaseFile);
    }

    [Fact]
    public void Read_FolderWithoutQuerySql_ThrowsNamingTheCase()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(
            temp,
            "queryless",
            query: null,
            json: /*lang=json,strict*/
            "{ \"columns\": [] }"
        );

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCaseReader.Read(folder, Postgres));

        thrown.Message.ShouldContain("realistic/queryless");
        thrown.Message.ShouldContain(RealisticCase.QueryFile);
    }

    [Fact]
    public void Read_SampleKeys_AreComparedIgnoringCase()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(
            temp,
            "cased",
            json: /*lang=json,strict*/
            "{ \"columns\": [], \"samples\": [ { \"Limit\": \"10\" } ] }"
        );

        var found = RealisticCaseReader.Read(folder, Postgres);

        found.Samples[0]["limit"].ShouldBe("10");
        found.Samples[0]["LIMIT"].ShouldBe("10");
    }

    [Fact]
    public void Read_SampleThatNamesAParameterTwiceIgnoringCase_ThrowsNamingIt()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(
            temp,
            "twice",
            json: /*lang=json,strict*/
            "{ \"columns\": [], \"samples\": [ { \"limit\": \"10\", \"LIMIT\": \"20\" } ] }"
        );

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCaseReader.Read(folder, Postgres));

        thrown.Message.ShouldContain("realistic/twice");
        thrown.Message.ShouldContain("@LIMIT twice");
    }

    [Fact]
    public void Read_CaseJsonThatDoesNotParse_ThrowsNamingTheCase()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(temp, "broken", json: "{ \"columns\": [ { \"name\": ");

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCaseReader.Read(folder, Postgres));

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
        "checkedFrom is not above 18, the first recorded major the case runs on"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"minimumMajor\": 17, \"columns\": [], \"checkedFrom\": { \"major\": 18, \"why\": \"w\" } }",
        "checkedFrom is not above 18, the first recorded major the case runs on"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"checkedFrom\": { \"major\": 14, \"why\": \"w\" } }",
        "checkedFrom is not above 14, the first recorded major the case runs on"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"checkedFrom\": { \"major\": 18, \"why\": \"w\", \"reason\": \"x\" } }",
        "reason"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"models\": { \"refusal\": \"SQLSRC255\", \"why\": \"w\", \"reason\": \"x\" } }",
        "reason"
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
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"dapper\": { \"why\": \"\" } }",
        "dapper gives no why"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"columns\": [], \"dapper\": { \"why\": \"w\", \"reason\": \"x\" } }",
        "reason"
    )]
    [InlineData( /*lang=json,strict*/
        "{ \"refusal\": \"SQLSRC229\", \"dapper\": { \"why\": \"w\" } }",
        "dapper on a case with no models"
    )]
    [InlineData(
        "{ \"columns\": [], \"models\": { \"refusal\": \"SQLSRC255\", \"why\": \"w\" },"
            + " \"dapper\": { \"why\": \"w\" } }",
        "dapper on a case with no models"
    )]
    public void Read_CaseJsonThatIsWrong_ThrowsNamingTheCaseAndTheFault(string json, string fault)
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(temp, "wrong", json: json);

        var thrown = Should.Throw<InvalidOperationException>(() => RealisticCaseReader.Read(folder, Postgres));

        thrown.Message.ShouldContain("realistic/wrong");
        thrown.Message.ShouldContain(fault);
    }

    [Fact]
    public void Read_CaseWithModelRefusal_ReadsIt()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(
            temp,
            "refused",
            json: /*lang=json,strict*/
            "{ \"columns\": [], \"models\": { \"refusal\": \"SQLSRC255\", \"why\": \"two ids\" } }"
        );

        var found = RealisticCaseReader.Read(folder, Postgres);

        found.Models.ShouldBe(new ModelRefusal("SQLSRC255", "two ids"));
        found.HasModels.ShouldBeFalse();
    }

    [Fact]
    public void Read_CaseWithCheckedFrom_ReadsIt()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(
            temp,
            "later",
            json: /*lang=json,strict*/
            "{ \"columns\": [], \"checkedFrom\": { \"major\": 18, \"why\": \"a later plan\" } }"
        );

        RealisticCaseReader.Read(folder, Postgres).CheckedFrom.ShouldBe(new CheckedFrom(18, "a later plan"));
    }

    [Fact]
    public void Read_CaseWithDapper_ReadsIt()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(
            temp,
            "unbound",
            json: /*lang=json,strict*/
            "{ \"columns\": [], \"dapper\": { \"why\": \"a string sent as text\" } }"
        );

        RealisticCaseReader.Read(folder, Postgres).Dapper.ShouldBe("a string sent as text");
    }

    [Fact]
    public void Read_CaseWithoutSamples_HasOneEmptySet()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(
            temp,
            "plain",
            json: /*lang=json,strict*/
            "{ \"columns\": [ { \"name\": \"one\", \"nullable\": false } ] }"
        );

        var found = RealisticCaseReader.Read(folder, Postgres);

        found.Name.ShouldBe("plain");
        found.ToString().ShouldBe("realistic/plain");
        found.Columns.ShouldBe([new TruthColumn("one", false, null, null)]);
        found.Samples.Count.ShouldBe(1);
        found.Samples[0].ShouldBeEmpty();
        found.Engine.ShouldBe(Postgres);
    }

    // A checkedFrom names one of the recorded majors of the engine the corpus is read for, and no other's.
    [Fact]
    public void Read_CheckedFromOfTheEnginesRecordedMajor_ReadsIt()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(
            temp,
            "later",
            json: /*lang=json,strict*/
            "{ \"columns\": [], \"checkedFrom\": { \"major\": 17, \"why\": \"w\" } }"
        );

        RealisticCaseReader.Read(folder, SqlServer).CheckedFrom.ShouldBe(new CheckedFrom(17, "w"));
    }

    [Fact]
    public void Read_CheckedFromOfAnotherEnginesRecordedMajor_ThrowsNamingTheEnginesOnes()
    {
        using var temp = new TempFolder();
        var folder = CaseFolder(
            temp,
            "later",
            json: /*lang=json,strict*/
            "{ \"columns\": [], \"checkedFrom\": { \"major\": 18, \"why\": \"w\" } }"
        );

        Should
            .Throw<InvalidOperationException>(() => RealisticCaseReader.Read(folder, SqlServer))
            .Message.ShouldContain("checkedFrom names 18, which is not a recorded major: 14 or 17");
    }
}
