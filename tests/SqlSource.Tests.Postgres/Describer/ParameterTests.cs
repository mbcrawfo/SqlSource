using System.Linq;
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
}
