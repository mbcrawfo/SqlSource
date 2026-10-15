using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Npgsql;
using Shouldly;
using SqlSource.Diagnostics;
using SqlSource.Snapshot;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Tests;
using Xunit;

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
        var before = await CountAsync(connection, "SELECT count(*) FROM reservations");
        var accounts = await CountAsync(connection, "SELECT count(*) FROM accounts");

        var first = await RealisticOracle.RunAsync(connection, Insert);
        var second = await RealisticOracle.RunAsync(connection, Insert);

        (await CountAsync(connection, "SELECT count(*) FROM reservations")).ShouldBe(before);
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
        var found = RealisticCases.Read(System.IO.Path.Combine(RepositoryPaths.RealisticCases, FirstCase));

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

    private static async Task<long> CountAsync(string connection, string sql)
    {
        var token = TestContext.Current.CancellationToken;
        await using var source = NpgsqlDataSource.Create(connection);
        await using var command = source.CreateCommand(sql);
        return (long)(await command.ExecuteScalarAsync(token))!;
    }
}
