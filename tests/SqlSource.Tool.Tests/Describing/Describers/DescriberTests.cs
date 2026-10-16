using System;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Testing.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Describers;

// The seam a describer is written against: the registry, the exchange, and the describer of the tests itself.
public class DescriberTests
{
    [Fact]
    public void Find_DialectWithADescriber_GivesIt()
    {
        var postgres = new FakeDescriber();
        var mssql = new FakeDescriber(SqlDialect.SqlServer);

        var registry = new DescriberRegistry([postgres, mssql]);

        registry.Find(SqlDialect.PostgreSql).ShouldBeSameAs(postgres);
        registry.Find(SqlDialect.SqlServer).ShouldBeSameAs(mssql);
    }

    [Fact]
    public void Find_DialectWithoutADescriber_GivesNull() =>
        new DescriberRegistry([new FakeDescriber()]).Find(SqlDialect.SqlServer).ShouldBeNull();

    [Fact]
    public void Constructor_TwoDescribersOfOneDialect_Throws() =>
        Should.Throw<ArgumentException>(() => new DescriberRegistry([new FakeDescriber(), new FakeDescriber()]));

    // The registry of the tests' hosts, which has no describer: the released tool's has PostgreSQL's.
    [Fact]
    public void Empty_EveryDescribableDialect_HasNoDescriber()
    {
        foreach (var dialect in SqlDescribableDialects.All)
        {
            DescriberRegistry.Empty.Find(dialect).ShouldBeNull();
        }
    }

    [Fact]
    public async Task AskAsync_LiveExchange_CallsLiveWithTheRequest()
    {
        var answer = await LiveExchange.Instance.AskAsync(
            "double",
            21,
            static (request, _) => Task.FromResult(request * 2),
            TestContext.Current.CancellationToken
        );

        answer.ShouldBe(42);
    }

    [Fact]
    public async Task OpenAsync_DescriberOfTheTests_MakesEveryCallThroughTheExchange()
    {
        var describer = new FakeDescriber();
        var capture = new DescribeCapture("postgres");
        var exchange = new CapturingExchange(LiveExchange.Instance, capture);
        var token = TestContext.Current.CancellationToken;

        var opened = await describer.OpenAsync(
            new OpenRequest("billing", "Host=db", exchange, TimeProvider.System),
            NullRunLog.Instance,
            token
        );
        await using var session = opened.Value.ShouldNotBeNull();
        var described = await session.DescribeAsync(
            new DescribeRequest("GetUser", "SELECT 1;", EquatableArray<SqlQueryParameter>.Empty),
            token
        );

        capture.Calls.Select(call => call.Method).ShouldBe(["open", "describe"]);
        described.Value.ShouldBe(FakeDescriber.NoRows);
        describer.Opened.ShouldHaveSingleItem().Database.ShouldBe("billing");
        describer.Described.ShouldHaveSingleItem().Name.ShouldBe("GetUser");
    }

    [Fact]
    public async Task DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow()
    {
        var describer = new FakeDescriber();
        describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "42P01: relation \"users\" does not exist");
        var token = TestContext.Current.CancellationToken;

        var opened = await describer.OpenAsync(
            new OpenRequest("billing", "Host=db", LiveExchange.Instance, TimeProvider.System),
            NullRunLog.Instance,
            token
        );
        var described = await opened
            .Value.ShouldNotBeNull()
            .DescribeAsync(new DescribeRequest("GetUser", "SELECT 1;", EquatableArray<SqlQueryParameter>.Empty), token);

        described.Value.ShouldBeNull();
        described.Failure.ShouldNotBeNull().Step.ShouldBe(DescribeStep.DescribeColumns);
    }

    private static readonly DescribeRequest GetUser = new(
        "GetUser",
        "SELECT 1;",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private static async Task<IDescribeSession> OpenAsync(FakeDescriber describer)
    {
        var opened = await describer.OpenAsync(
            new OpenRequest("billing", "Host=db", LiveExchange.Instance, TimeProvider.System),
            NullRunLog.Instance,
            TestContext.Current.CancellationToken
        );
        return opened.Value.ShouldNotBeNull();
    }

    // What the describer of the tests is given goes through its plain records and comes back as it was.
    [Fact]
    public async Task DescribeAsync_DescriptionWithEveryPart_ComesBackEqual()
    {
        var describer = new FakeDescriber { Server = FakeDescriber.ServerWith("17.1", "4.5.6") };
        describer.Descriptions["GetUser"] = FakeDescriber.TypedRows;

        await using var session = await OpenAsync(describer);
        var described = await session.DescribeAsync(GetUser, TestContext.Current.CancellationToken);

        described.Value.ShouldBe(FakeDescriber.TypedRows);
        session.Server.ShouldBe(describer.Server);
    }

    [Fact]
    public async Task DescribeAsync_FailureGivenToTheDescriber_ComesBackWithEveryPart()
    {
        var describer = new FakeDescriber();
        describer.Failures["GetUser"] = FakeDescriber.Failure(
            "GetUser",
            DescribeStep.Explain,
            "check the SQL",
            "line one",
            "line two"
        );

        await using var session = await OpenAsync(describer);
        var described = await session.DescribeAsync(GetUser, TestContext.Current.CancellationToken);

        described.Value.ShouldBeNull();
        var failure = described.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC999");
        failure
            .Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture)
            .ShouldBe("The server rejected the query '{0}'");
        failure.Descriptor.HelpLinkUri.ShouldBe("https://example.test/sqlsrc999");
        failure.Arguments.ToArray().ShouldBe(["GetUser"]);
        failure.Step.ShouldBe(DescribeStep.Explain);
        failure.ServerLines.ToArray().ShouldBe(["line one", "line two"]);
        failure.Help.ShouldBe("check the SQL");
    }

    // The describer of the tests carries the types its tests give it.  Phase 6 adds SQL Server's.
    [Fact]
    public async Task DescribeAsync_DescriptionWithASqlServerType_IsNotCarriedThroughTheExchange()
    {
        var describer = new FakeDescriber();
        describer.Descriptions["GetUser"] = FakeDescriber.NoRows with
        {
            Parameters = TestSidecars.Of(new DescribedParameter("id", TestSidecars.SqlServer(), null)),
        };

        await using var session = await OpenAsync(describer);

        _ = await Should.ThrowAsync<NotSupportedException>(async () =>
            await session.DescribeAsync(GetUser, TestContext.Current.CancellationToken)
        );
    }

    // A record prints its members, and one of these is a secret.
    [Fact]
    public void ToString_OpenRequest_LeavesTheConnectionOut()
    {
        var request = new OpenRequest(
            "billing",
            Secrets.ConnectionWithWord,
            LiveExchange.Instance,
            TimeProvider.System
        );

        request.ToString().ShouldBe("OpenRequest { Database = billing }");
    }

    [Theory]
    [InlineData(nameof(DescribeStep.DescribeParameters), "describe parameters")]
    [InlineData(nameof(DescribeStep.CheckParameters), "check parameters")]
    [InlineData(nameof(DescribeStep.DescribeColumns), "describe columns")]
    [InlineData(nameof(DescribeStep.Catalog), "catalog")]
    [InlineData(nameof(DescribeStep.Explain), "explain")]
    [InlineData(nameof(DescribeStep.Walk), "walk")]
    [InlineData(nameof(DescribeStep.TableMatch), "table match")]
    [InlineData(nameof(DescribeStep.ResolveType), "resolve type")]
    [InlineData(nameof(DescribeStep.WriteSidecar), "write sidecar")]
    public void Of_Step_IsTheNameTheEpicGivesIt(string step, string name) =>
        DescribeStepName.Of(Enum.Parse<DescribeStep>(step)).ShouldBe(name);
}
