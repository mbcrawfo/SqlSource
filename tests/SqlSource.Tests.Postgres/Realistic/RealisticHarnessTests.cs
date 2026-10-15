using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Tests;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.Tests.Postgres.Realistic;

// The harness of the realistic cases: the reading of a case's folder, the substitution of its sample values, the run
// that leaves the seed as it was, and the text a description is recorded as.
[Collection(UsesPostgres.Name)]
public sealed class RealisticHarnessTests(PostgresContainer postgres)
{
    private const string FirstCase = "aggregate-cte-left-joined";

    private const string Query = "-- name: Q\nSELECT 1;\n";

    [Fact]
    public void Substitute_ParameterInAStringACommentOrAQuotedName_IsLeftAlone() =>
        RealisticOracle
            .Substitute("SELECT '@x', \"@x\", @x -- @x", Values(("x", "1")), Values(("x", "integer")))
            .ShouldBe("SELECT '@x', \"@x\", (1)::integer -- @x");

    [Fact]
    public void Substitute_TwoParameters_ReplacesEachWithItsCastLiteral() =>
        RealisticOracle
            .Substitute(
                "WHERE a = @a AND b = @B",
                Values(("a", "'x'"), ("b", "NULL")),
                Values(("a", "text"), ("b", "bigint"))
            )
            .ShouldBe("WHERE a = ('x')::text AND b = (NULL)::bigint");

    [Fact]
    public void Substitute_ParameterNotInTheSample_ThrowsNamingIt() =>
        Should
            .Throw<InvalidOperationException>(() =>
                RealisticOracle.Substitute(
                    "WHERE a = @a AND c = @c",
                    Values(("a", "1")),
                    Values(("a", "integer"), ("c", "integer"))
                )
            )
            .Message.ShouldContain("@c");

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
    public void CheckSampleKeys_KeyThatNamesNoParameter_ThrowsNamingTheCaseAndTheKey()
    {
        var found = Case(Values(("from", "'2024-01-01'"), ("tto", "'2025-01-01'")));

        var thrown = Should.Throw<InvalidOperationException>(() =>
            RealisticOracle.CheckSampleKeys(found, ["from", "to"])
        );

        thrown.Message.ShouldContain("realistic/keyed");
        thrown.Message.ShouldContain("@tto");
    }

    [Fact]
    public void CheckSampleKeys_KeyThatDiffersInCaseAlone_DoesNotThrow() =>
        Should.NotThrow(() => RealisticOracle.CheckSampleKeys(Case(Values(("From", "'2024-01-01'"))), ["from", "to"]));

    [Theory]
    [InlineData(true, 14, Expectation.Record)]
    [InlineData(true, 18, Expectation.Record)]
    [InlineData(true, 16, Expectation.Refuse)]
    [InlineData(false, 14, Expectation.Compare)]
    [InlineData(false, 18, Expectation.Compare)]
    [InlineData(false, 17, Expectation.Skip)]
    public void ExpectationOn_RecordingAndMajor_IsWhatTheTheoryDoes(bool recording, int major, Expectation expected) =>
        RealisticRun.ExpectationOn(recording, major).ShouldBe(expected);

    [Theory]
    [InlineData(null, 14, true)]
    [InlineData(17, 14, false)]
    [InlineData(17, 17, true)]
    [InlineData(17, 18, true)]
    public void RunsOn_MinimumMajorAndServer_IsWhetherTheCaseRuns(int? minimum, int major, bool runs) =>
        RealisticRun.RunsOn(Case(Values()) with { MinimumMajor = minimum }, major).ShouldBe(runs);

    [Fact]
    public void ColumnsOf_ColumnNamedOtherwiseThanItsTruth_FailsNamingTheCaseAndThePosition()
    {
        var found = Case(Values()) with
        {
            Columns = [new TruthColumn("id", false, null, null), new TruthColumn("id", false, null, null)],
        };
        var described = Outcome<QueryDescription, DescribeFailure>.Of(
            new QueryDescription(
                SidecarResultKind.Rows,
                new EquatableArray<DescribedParameter>([]),
                new EquatableArray<SidecarColumn>([Column(0, "id"), Column(1, "account_id")]),
                null,
                null,
                null
            )
        );

        var thrown = Should.Throw<FailException>(() => RealisticRun.ColumnsOf(found, described));

        thrown.Message.ShouldStartWith("realistic/keyed column 1 is described as account_id");
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

    [Fact]
    public async Task RunAsync_InsertReturning_LeavesTheTableAsItWas()
    {
        const string Insert =
            "INSERT INTO reservations (account_id, during) SELECT id, '[2024-01-01,2024-01-02)' FROM accounts "
            + "RETURNING id";
        var connection = postgres.RealisticConnectionString;
        var before = (long)(await RealisticRun.ScalarAsync(connection, "SELECT count(*) FROM reservations"))!;
        var accounts = (long)(await RealisticRun.ScalarAsync(connection, "SELECT count(*) FROM accounts"))!;

        var first = await RealisticOracle.RunAsync(connection, Insert);
        var second = await RealisticOracle.RunAsync(connection, Insert);

        ((long)(await RealisticRun.ScalarAsync(connection, "SELECT count(*) FROM reservations"))!).ShouldBe(before);
        first.Rows.ShouldBe((int)accounts);
        second.Rows.ShouldBe((int)accounts);
        first.Names.ShouldBe(["id"]);
        first.SawNull.ShouldBe([false]);
    }

    [Fact]
    public async Task RunAsync_NullableColumn_SawNullWhereARowHasOne()
    {
        var run = await RealisticOracle.RunAsync(
            postgres.RealisticConnectionString,
            "SELECT id, email FROM accounts ORDER BY id"
        );

        run.Names.ShouldBe(["id", "email"]);
        run.SawNull.ShouldBe([false, true]);
    }

    [Fact]
    public async Task DescribeAsync_FirstCase_IsDescribedAndRendered()
    {
        var found = RealisticCases.Read(Path.Combine(RepositoryPaths.RealisticCases, FirstCase));

        var described = await RealisticRun.DescribeAsync(found, postgres.RealisticConnectionString);

        described.Failure.ShouldBeNull();
        RealisticRun.Render(described).ShouldStartWith("kind: rows\n");
    }

    [Fact]
    public void All_Cases_HoldTheFirstCase() => RealisticCases.All.ShouldContain(found => found.Name == FirstCase);

    [Fact]
    public void Render_Description_WritesALineForEachFact()
    {
        var integer = Type("integer", "int4");
        var description = new QueryDescription(
            SidecarResultKind.Rows,
            new EquatableArray<DescribedParameter>([
                new DescribedParameter("limit", Type("bigint", "int8"), SidecarValues.TypeSource.Inferred),
            ]),
            new EquatableArray<SidecarColumn>([
                new SidecarColumn(
                    0,
                    "id",
                    integer,
                    false,
                    SidecarValues.NullableSource.Catalog,
                    new SidecarOrigin("public", "accounts", "id"),
                    true,
                    false
                ),
                new SidecarColumn(1, "spent", Type("numeric", "numeric"), null, null, null, null, null),
            ]),
            null,
            SidecarValues.Plan.Walked,
            null
        );

        RealisticRun
            .Render(Outcome<QueryDescription, DescribeFailure>.Of(description))
            .ShouldBe(
                "kind: rows\n"
                    + "plan: walked\n"
                    + "table-match: -\n"
                    + "parameter @limit: bigint (inferred)\n"
                    + "column 0 id: integer | nullable false | catalog | public.accounts.id\n"
                    + "column 1 spent: numeric | nullable null | - | -\n"
            );
    }

    [Fact]
    public void Render_RefusalWithHelp_WritesTheHelpBetweenItsArgumentsAndTheServer() =>
        RealisticRun
            .Render(
                Outcome<QueryDescription, DescribeFailure>.Failed(
                    new DescribeFailure(
                        ToolDiagnostics.ServerRejectedQuery,
                        new EquatableArray<string>(["Q", "operator does not exist"]),
                        null,
                        new EquatableArray<string>(["error: operator does not exist", "hint: add casts"]),
                        "cast the parameter"
                    )
                )
            )
            .ShouldBe(
                "failure: SQLSRC225\n"
                    + "argument: Q\n"
                    + "argument: operator does not exist\n"
                    + "help: cast the parameter\n"
                    + "server: error: operator does not exist\n"
                    + "server: hint: add casts\n"
            );

    private static Dictionary<string, string> Values(params (string Name, string Value)[] values)
    {
        var found = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (name, value) in values)
        {
            found.Add(name, value);
        }

        return found;
    }

    private static PostgresType Type(string name, string internalName) =>
        new(name, SidecarValues.Kind.Base, "pg_catalog", internalName, null, null, null, null, null, null, null);

    private static RealisticCase Case(Dictionary<string, string> sample) =>
        new(
            "keyed",
            "keyed",
            null,
            null,
            [],
            [new Dictionary<string, string>(sample, StringComparer.OrdinalIgnoreCase)]
        );

    private static SidecarColumn Column(int ordinal, string name) =>
        new(ordinal, name, Type("integer", "int4"), null, null, null, null, null);
}
