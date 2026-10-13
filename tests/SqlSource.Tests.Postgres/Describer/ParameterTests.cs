using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tests.Postgres.Describer;

// A query's parameters against a real server.  Every describe here runs in a session of its own and ends with no
// prepared statement left in it.
[Collection(UsesPostgres.Name)]
public sealed class ParameterTests(PostgresContainer postgres)
{
    private async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        IDescribeExchange? exchange = null
    )
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString, exchange);
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();

        var described = await session.DescribeAsync(request, TestContext.Current.CancellationToken);

        (await Sessions.PreparedStatementsAsync(session)).ShouldBe(0);
        return described;
    }

    private static string[] MethodsOf(DescribeCapture capture) => [.. capture.Calls.Select(call => call.Method)];

    [Fact]
    public async Task Describe_TwoStatements_IsSqlsrc230AndSendsNothing()
    {
        var capture = new DescribeCapture("postgres");

        var described = await DescribeAsync(
            Requests.Of("DELETE FROM orders WHERE user_id = @id; DELETE FROM users WHERE id = @id"),
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        described.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC230");
        MethodsOf(capture).ShouldBe(["open"]);
    }

    [Fact]
    public async Task Describe_DeclaredTypeThatIsNoTypeName_IsSqlsrc228AndSendsNothing()
    {
        var capture = new DescribeCapture("postgres");

        var described = await DescribeAsync(
            Requests.Of("SELECT 1 FROM users WHERE id = @id", ("id", "int) AS SELECT 1; DROP TABLE users; --")),
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC228");
        failure.Arguments.ShouldBe([
            "int) AS SELECT 1; DROP TABLE users; --",
            "@id",
            Requests.Query,
            PostgresErrors.NotATypeName,
        ]);
        MethodsOf(capture).ShouldBe(["open"]);
    }

    private async Task<DescribeFailure> ShouldFailAsync(DescribeRequest request, string id)
    {
        var described = await DescribeAsync(request);

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe(id);
        failure.Step.ShouldBe(DescribeStep.DescribeParameters);
        return failure;
    }

    // The server checks a declared type against every use of its parameter.  The hint's words differ by version.
    [Fact]
    public async Task Describe_DeclaredTypeThatAUseRejects_IsSqlsrc225WithTheServersLines()
    {
        var failure = await ShouldFailAsync(
            Requests.Of("SELECT 1 FROM users WHERE name = @p", ("p", "int")),
            "SQLSRC225"
        );

        failure.Arguments.ShouldBe([Requests.Query, "operator does not exist: character varying = integer"]);
        failure.ServerLines.Count.ShouldBe(3);
        failure.ServerLines[0].ShouldBe("42883: operator does not exist: character varying = integer");
        failure.ServerLines[1].ShouldStartWith("hint: ");
        failure.ServerLines[2].ShouldBe("at character 32");
        failure.Help.ShouldBeNull();
    }

    [Fact]
    public async Task Describe_ParameterThatNoUseTypes_IsSqlsrc226()
    {
        var failure = await ShouldFailAsync(Requests.Of("SELECT @p IS NULL"), "SQLSRC226");

        failure.Arguments.ShouldBe(["@p", Requests.Query]);
        failure.ServerLines.ShouldBe(["42P18: could not determine data type of parameter $1"]);
    }

    [Fact]
    public async Task Describe_FirstUseThatDoesNotFixTheType_IsSqlsrc227AtTheUse()
    {
        var failure = await ShouldFailAsync(
            Requests.Of("SELECT 1 FROM users WHERE (@name IS NULL OR name = @name)"),
            "SQLSRC227"
        );

        failure.Arguments.ShouldBe(["@name", Requests.Query]);
        failure.ServerLines.ShouldBe(["42P08: could not determine data type of parameter $1", "at character 28"]);
    }

    [Theory]
    [InlineData("SELECT 1 FROM users WHERE (name = @name OR @name IS NULL)", null)]
    [InlineData("SELECT 1 FROM users WHERE (@name IS NULL OR name = @name)", "varchar")]
    public async Task Describe_OptionalFilterWithTheTypingUseFirstOrADeclaration_IsDescribed(string sql, string? type)
    {
        var request = type is null ? Requests.Of(sql) : Requests.Of(sql, ("name", type));

        _ = (await DescribeAsync(request)).Value.ShouldNotBeNull();
    }

    [Fact]
    public async Task Describe_DeclaredTypeTheDatabaseLacks_IsSqlsrc228NamingItAndItsParameter()
    {
        var failure = await ShouldFailAsync(
            Requests.Of("SELECT 1 FROM users WHERE id = @id", ("p", "nope_t")),
            "SQLSRC228"
        );

        failure.Arguments.ShouldBe(["nope_t", "@p", Requests.Query, PostgresErrors.NoSuchType]);
        failure.ServerLines.ShouldBe(["42704: type \"nope_t\" does not exist"]);
    }

    // The position is in the user's SQL: the PREPARE around it is not counted.
    [Theory]
    [InlineData("SELEC 1 WHERE @p = 1", "42601: syntax error at or near \"SELEC\"", "at character 1")]
    [InlineData("SELECT 1 FROM nope WHERE id = @id", "42P01: relation \"nope\" does not exist", "at character 15")]
    [InlineData(
        "SELECT 'hé\U0001F600' FROM nope WHERE id = @id",
        "42P01: relation \"nope\" does not exist",
        "at character 19"
    )]
    public async Task Describe_QueryTheServerRejects_IsSqlsrc225AtTheCharacterOfTheUsersSql(
        string sql,
        string message,
        string position
    )
    {
        var failure = await ShouldFailAsync(Requests.Of(sql), "SQLSRC225");

        failure.ServerLines.ShouldBe([message, position]);
    }

    // A rejected PREPARE leaves no statement, and the session goes on with the next name.
    [Fact]
    public async Task Describe_AfterAQueryTheServerRejected_DescribesTheNextOne()
    {
        var capture = new DescribeCapture("postgres");
        var opened = await Sessions.OpenAsync(
            postgres.ConnectionString,
            new CapturingExchange(LiveExchange.Instance, capture)
        );
        await using var session = opened.Value.ShouldBeOfType<PostgresSession>();
        var token = TestContext.Current.CancellationToken;

        var first = await session.DescribeAsync(Requests.Of("DELETE FROM nope WHERE id = @id"), token);
        var second = await session.DescribeAsync(Requests.Of("DELETE FROM users WHERE id = @id"), token);

        first.Failure.ShouldNotBeNull().Descriptor.Id.ShouldBe("SQLSRC225");
        _ = second.Value.ShouldNotBeNull();
        MethodsOf(capture)
            .Where(method => method is "prepare" or "deallocate")
            .ShouldBe(["prepare", "prepare", "deallocate"]);
        CaptureJson.Write(capture).ShouldContain("\"statement\": \"sqlsource_2\"");
        (await Sessions.PreparedStatementsAsync(session)).ShouldBe(0);
    }

    // A run that is cancelled ends as cancelled, and never as a failure that quotes the driver.
    [Fact]
    public async Task Describe_CancelledToken_ThrowsAndIsNoFailure()
    {
        var opened = await Sessions.OpenAsync(postgres.ConnectionString);
        await using var session = opened.Value.ShouldNotBeNull();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            session.DescribeAsync(Requests.Of("DELETE FROM users WHERE id = @id"), cancelled.Token)
        );
    }

    [Fact]
    public async Task Describe_StatementWithATrailingSemicolonAndNoParameter_IsDescribed() =>
        (await DescribeAsync(Requests.Of("DELETE FROM samples WHERE flag; -- all of them"))).Value.ShouldNotBeNull();
}
