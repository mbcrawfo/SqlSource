using System;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Describers;

// The base of the describers' sessions: how a session is opened from the answer of its "open", what becomes of the
// connection when none is, and the rules of a server that both engines share.  Each connection is disposed by its
// test too, after the test has counted what the base did with it.
public sealed class DescriberSessionTests
{
    private const int Floor = 14;

    [Fact]
    public async Task OpenAsync_AnswerWithAnError_IsTheFailureAndDisposesTheConnection()
    {
        await using var connection = new RecordingConnection();

        var opened = await TestSession.OpenAsync(connection, () => new TestOpenAnswer(null, "no such database"));

        opened.Failure.ShouldBe(FailureOf("no such database"));
        connection.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task OpenAsync_ServerBelowTheFloor_IsVersionNotSupportedAndDisposesTheConnection()
    {
        await using var connection = new RecordingConnection();

        var opened = await TestSession.OpenAsync(connection, () => new TestOpenAnswer("13.12", null));

        opened.Failure.ShouldBe(FailureOf("version 13.12"));
        connection.Disposed.ShouldBe(1);
    }

    [Fact]
    public async Task OpenAsync_ServerAtTheFloor_IsTheSessionAndKeepsTheConnection()
    {
        await using var connection = new RecordingConnection();

        var opened = await TestSession.OpenAsync(connection, () => new TestOpenAnswer("14.0", null));

        await using var session = opened.Value.ShouldNotBeNull();
        session.ShouldBeOfType<TestSession>().Connection.ShouldBeSameAs(connection);
        session.Server.Version.ShouldBe("14.0");
        session.Server.Driver.ShouldBe("FakeDriver");
        connection.Disposed.ShouldBe(0);
    }

    [Fact]
    public async Task OpenAsync_ExchangeThatThrows_ThrowsAndDisposesTheConnection()
    {
        await using var connection = new RecordingConnection();

        _ = await Should.ThrowAsync<InvalidOperationException>(() =>
            TestSession.OpenAsync(connection, () => throw new InvalidOperationException("lost"))
        );

        connection.Disposed.ShouldBe(1);
    }

    // The opening's ServerOf throws for an answer with no version and no error, inside the base's "try".
    [Fact]
    public async Task OpenAsync_AnswerWithNoVersion_ThrowsAndDisposesTheConnection()
    {
        await using var connection = new RecordingConnection();

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            TestSession.OpenAsync(connection, () => new TestOpenAnswer(null, null))
        );

        thrown.Message.ShouldBe("The answer to 'open' holds no version and no error.");
        connection.Disposed.ShouldBe(1);
    }

    [Fact]
    public void ServerOf_NoVersion_Throws() =>
        Should
            .Throw<InvalidOperationException>(() => TestSession.ServerOfVersion(null))
            .Message.ShouldBe("The answer to 'open' holds no version and no error.");

    [Theory]
    [InlineData("13.12", 14, true)]
    [InlineData("14.0", 14, false)]
    [InlineData("devel", 14, false)]
    public void IsBelow_Version_ComparesItsLeadingMajor(string version, int minimum, bool expected) =>
        TestSession.IsBelowOf(version, minimum).ShouldBe(expected);

    private static DescribeFailure FailureOf(string reason) => FakeDescriber.Failure(reason, step: null, help: null);

    // The request and the answer of the session's "open": a version, or an error.
    private sealed record TestOpenRequest
    {
        public static TestOpenRequest Instance { get; } = new();
    }

    private sealed record TestOpenAnswer(string? Version, string? Error);

    // A connection that counts how often it was disposed.
    private sealed class RecordingConnection : IAsyncDisposable
    {
        public int Disposed { get; private set; }

        public ValueTask DisposeAsync()
        {
            Disposed++;
            return ValueTask.CompletedTask;
        }
    }

    // A session of the base, opened through the exchange of a real run, whose "live" function answers from a
    // lambda, with a floor of 14.
    private sealed class TestSession(IDescribeExchange exchange, TimeProvider time, ServerInfo server)
        : DescriberSession(exchange, time, server)
    {
        public RecordingConnection? Connection { get; private init; }

        public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
            RecordingConnection connection,
            Func<TestOpenAnswer> answer
        )
        {
            var request = new OpenRequest("billing", "Host=db", LiveExchange.Instance, new TestClock());
            return OpenAsync(
                request,
                connection,
                new SessionOpening<TestOpenRequest, TestOpenAnswer>(
                    "open",
                    TestOpenRequest.Instance,
                    (_, _) => Task.FromResult(answer()),
                    static answered => answered.Error is { } error ? FailureOf(error) : null,
                    static answered => ServerOfVersion(answered.Version),
                    static (_, server) => IsBelow(server.Version, Floor),
                    static server => FailureOf("version " + server.Version),
                    (_, server) => new TestSession(request.Exchange, request.Time, server) { Connection = connection }
                ),
                TestContext.Current.CancellationToken
            );
        }

        public static ServerInfo ServerOfVersion(string? version) => ServerOf(version, "FakeDriver", null, []);

        public static bool IsBelowOf(string version, int minimum) => IsBelow(version, minimum);

        public override Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
            DescribeRequest request,
            CancellationToken cancellationToken
        ) => Task.FromResult(Failed(FailureOf("not described")));

        public override ValueTask DisposeAsync() => Connection?.DisposeAsync() ?? ValueTask.CompletedTask;
    }
}
