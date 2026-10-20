using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Capture;

// The exchange that records: every call of a describer is in the capture, in order, as it was asked and answered.
public sealed class CapturingExchangeTests
{
    private readonly DescribeCapture _capture = new(SidecarValues.Engine.Postgres);
    private readonly FakeDescriber _describer = new();

    private Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync() =>
        _describer.OpenAsync(
            "billing",
            Secrets.ConnectionWithMarker,
            new CapturingExchange(LiveExchange.Instance, _capture)
        );

    [Fact]
    public async Task AskAsync_DescriberOverARun_KeepsEachCallInOrderWithItsRequestAndItsAnswer()
    {
        var opened = await OpenAsync();
        await using var session = opened.Value.ShouldNotBeNull();
        _ = await session.DescribeAsync(CaptureInputs.GetUser, TestContext.Current.CancellationToken);

        _capture
            .Calls.Select(call => $"{call.Method} {CaptureJson.TextOf(call.Request)} {CaptureJson.TextOf(call.Answer)}")
            .ShouldBe([
                "open {} {\"server\":{\"version\":\"16.4\",\"driver\":\"FakeDriver\",\"driverVersion\":\"1.2.3\","
                    + "\"settings\":[]},\"error\":null}",
                "describe {\"name\":\"GetUser\",\"sql\":\"SELECT 1;\",\"parameters\":[]} "
                    + "{\"description\":{\"resultKind\":\"None\",\"parameters\":[],\"columns\":null,"
                    + "\"matchesTable\":null,\"plan\":null,\"tableMatch\":null},\"error\":null}",
            ]);
    }

    // The request of "open" holds nothing, so nothing of the connection is in the file.
    [Fact]
    public async Task AskAsync_SessionOpenedWithAConnection_KeepsNothingOfIt()
    {
        var opened = await OpenAsync();
        await using var session = opened.Value.ShouldNotBeNull();

        _describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.ConnectionWithMarker);
        CaptureJson.Write(_capture).ShouldNotContain(Secrets.Marker);
    }

    [Fact]
    public async Task AskAsync_LiveThatThrows_AddsNoCallAndLetsTheExceptionPass()
    {
        _describer.OpenException = new IOException("lost");

        var thrown = await Should.ThrowAsync<IOException>(OpenAsync);

        thrown.Message.ShouldBe("lost");
        _capture.Calls.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_FailureOfTheServer_IsKeptAsTheAnswer()
    {
        _describer.OpenFailure = FakeDescriber.Failure("billing", step: null, help: null, "connection refused");

        var opened = await OpenAsync();

        _ = opened.Failure.ShouldNotBeNull();
        var answer = CaptureJson.TextOf(_capture.Calls.ShouldHaveSingleItem().Answer);
        answer.ShouldStartWith("{\"server\":null,\"error\":{\"id\":\"SQLSRC999\",");
        answer.ShouldContain("\"serverLines\":[\"connection refused\"]");
    }

    [Fact]
    public async Task AskAsync_TwoSessions_KeepsTwoOpenCalls()
    {
        await using var first = (await OpenAsync()).Value.ShouldNotBeNull();
        await using var second = (await OpenAsync()).Value.ShouldNotBeNull();

        _capture.Calls.Select(call => call.Method).ShouldBe(["open", "open"]);
    }
}
