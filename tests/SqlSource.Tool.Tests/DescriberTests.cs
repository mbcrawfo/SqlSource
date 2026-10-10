using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests;

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

    // The released tool has no describer in this sub-phase: phase 3 registers the first.
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
        var exchange = new RecordingExchange();
        var token = TestContext.Current.CancellationToken;

        var opened = await describer.OpenAsync(new OpenRequest("billing", "Host=db", exchange), token);
        await using var session = opened.Session.ShouldNotBeNull();
        var described = await session.DescribeAsync(
            new DescribeRequest("GetUser", "SELECT 1;", EquatableArray<SqlQueryParameter>.Empty),
            token
        );

        exchange.Methods.ShouldBe(["open", "describe"]);
        described.Description.ShouldBe(FakeDescriber.NoRows);
        describer.Opened.ShouldHaveSingleItem().Database.ShouldBe("billing");
        describer.Described.ShouldHaveSingleItem().Name.ShouldBe("GetUser");
    }

    [Fact]
    public async Task DescribeAsync_QueryGivenAFailure_ReturnsItAndDoesNotThrow()
    {
        var describer = new FakeDescriber();
        describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "42P01: relation \"users\" does not exist");
        var token = TestContext.Current.CancellationToken;

        var opened = await describer.OpenAsync(new OpenRequest("billing", "Host=db", new RecordingExchange()), token);
        var described = await opened
            .Session.ShouldNotBeNull()
            .DescribeAsync(new DescribeRequest("GetUser", "SELECT 1;", EquatableArray<SqlQueryParameter>.Empty), token);

        described.Description.ShouldBeNull();
        described.Failure.ShouldNotBeNull().Step.ShouldBe(DescribeStep.DescribeColumns);
    }

    // A record prints its members, and one of these is a secret.
    [Fact]
    public void ToString_OpenRequest_LeavesTheConnectionOut()
    {
        var request = new OpenRequest("billing", "Host=db;Password=s3cret", LiveExchange.Instance);

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
