using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Capture;

// The exchange that replays: it answers from a capture, in the capture's order for a question asked twice, never
// asks the engine, and fails loudly on a question the capture does not hold.
public class ReplayingExchangeTests
{
    private const string Awkward = "SELECT '<é>' AS \"a\"\r\nFROM t";

    private static readonly DescribeRequest GetUser = new(
        "GetUser",
        "SELECT 1;",
        EquatableArray<SqlQueryParameter>.Empty
    );

    private int _live;

    private static FakeDescribeRequest Question(string sql = "SELECT 1;") => new("GetUser", sql, []);

    private static FakeOpenAnswer Answer(string version) => new(new FakeServer(version, null, null, []), null);

    private static DescribeCapture Capture(params (FakeDescribeRequest Request, FakeOpenAnswer Answer)[] calls)
    {
        var capture = new DescribeCapture("postgres");
        foreach (var (request, answer) in calls)
        {
            capture.Add(new CaptureCall("ask", CaptureJson.ToElement(request), CaptureJson.ToElement(answer)));
        }

        return capture;
    }

    // Asks, and gives the version of the answer's server, which tells one answer from another.  The engine counts
    // how often it is asked, which under a replay is never.
    private async Task<string> AskAsync(ReplayingExchange exchange, FakeDescribeRequest request, string method = "ask")
    {
        var answer = await exchange.AskAsync(
            method,
            request,
            (_, _) =>
            {
                _live++;
                return Task.FromResult(Answer("live"));
            },
            TestContext.Current.CancellationToken
        );
        return answer.Server.ShouldNotBeNull().Version;
    }

    private static async Task<(ServerInfo Server, Outcome<QueryDescription, DescribeFailure> Described)> DescribeAsync(
        FakeDescriber describer,
        IDescribeExchange exchange
    )
    {
        var token = TestContext.Current.CancellationToken;
        var opened = await describer.OpenAsync(
            new OpenRequest("billing", "replay", exchange, TimeProvider.System),
            NullRunLog.Instance,
            token
        );
        await using var session = opened.Value.ShouldNotBeNull();
        return (session.Server, await session.DescribeAsync(GetUser, token));
    }

    [Fact]
    public async Task AskAsync_DescriptionMadeThroughACapture_IsEqualWhenReplayed()
    {
        var told = new FakeDescriber { Server = FakeDescriber.ServerWith("17.1", "4.5.6") };
        told.Descriptions["GetUser"] = FakeDescriber.TypedRows;
        var capture = new DescribeCapture("postgres");
        var (_, recorded) = await DescribeAsync(told, new CapturingExchange(LiveExchange.Instance, capture));
        var untold = new FakeDescriber();
        var replay = new ReplayingExchange(capture);

        var (server, replayed) = await DescribeAsync(untold, replay);

        replayed.Value.ShouldBe(recorded.Value.ShouldNotBeNull());
        replayed.Value.ShouldBe(FakeDescriber.TypedRows);
        server.ShouldBe(told.Server);
        untold.Opened.ShouldBeEmpty();
        untold.Described.ShouldBeEmpty();
        replay.Unused.ShouldBeEmpty();
    }

    [Fact]
    public async Task AskAsync_FailureThatWasCaptured_IsTheFailureWhenReplayed()
    {
        var told = new FakeDescriber();
        told.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "42P01: relation \"users\" does not exist");
        var capture = new DescribeCapture("postgres");
        _ = await DescribeAsync(told, new CapturingExchange(LiveExchange.Instance, capture));

        var (_, replayed) = await DescribeAsync(new FakeDescriber(), new ReplayingExchange(capture));

        var failure = replayed.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe("SQLSRC999");
        failure.ServerLines.ToArray().ShouldBe(["42P01: relation \"users\" does not exist"]);
    }

    [Fact]
    public async Task AskAsync_CallTheCaptureLacks_ThrowsWithTheMethodAndTheRequestAndDoesNotAskTheEngine()
    {
        var exchange = new ReplayingExchange(Capture((Question(), Answer("1"))));

        var thrown = await Should.ThrowAsync<CaptureMissException>(async () =>
            await AskAsync(exchange, Question("SELECT 2;"))
        );

        thrown.Message.ShouldBe(
            "The capture has no further answer to 'ask' for the request "
                + "{\"name\":\"GetUser\",\"sql\":\"SELECT 2;\",\"parameters\":[]}."
        );
        _live.ShouldBe(0);
    }

    [Fact]
    public async Task AskAsync_SameRequestUnderAnotherMethod_IsACallTheCaptureLacks()
    {
        var exchange = new ReplayingExchange(Capture((Question(), Answer("1"))));

        var thrown = await Should.ThrowAsync<CaptureMissException>(async () =>
            await AskAsync(exchange, Question(), "other")
        );

        thrown.Message.ShouldStartWith("The capture has no further answer to 'other' for the request ");
    }

    [Fact]
    public async Task AskAsync_QuestionAskedTwice_GivesItsAnswersInOrderAndThenThrows()
    {
        var exchange = new ReplayingExchange(Capture((Question(), Answer("1")), (Question(), Answer("2"))));

        (await AskAsync(exchange, Question())).ShouldBe("1");
        (await AskAsync(exchange, Question())).ShouldBe("2");
        _ = await Should.ThrowAsync<CaptureMissException>(async () => await AskAsync(exchange, Question()));
        _live.ShouldBe(0);
    }

    // A record with a list is equal to no other instance of itself.  The key is the text, so a new one finds it.
    [Fact]
    public async Task AskAsync_SameRequestFromANewInstance_FindsTheCall()
    {
        var recorded = new FakeDescribeRequest(
            "GetUser",
            "SELECT @id;",
            [new FakeListedParameter("id", "int4", false, true)]
        );
        var asked = new FakeDescribeRequest(
            "GetUser",
            "SELECT @id;",
            [new FakeListedParameter("id", "int4", false, true)]
        );
        recorded.ShouldNotBe(asked);
        var exchange = new ReplayingExchange(Capture((recorded, Answer("1"))));

        (await AskAsync(exchange, asked)).ShouldBe("1");
    }

    // Order is kept among the answers of one question, and among nothing else.
    [Fact]
    public async Task AskAsync_QuestionsInAnotherOrderThanRecorded_AreEachAnswered()
    {
        var exchange = new ReplayingExchange(
            Capture((Question("SELECT 1;"), Answer("1")), (Question("SELECT 2;"), Answer("2")))
        );

        (await AskAsync(exchange, Question("SELECT 2;"))).ShouldBe("2");
        (await AskAsync(exchange, Question("SELECT 1;"))).ShouldBe("1");
    }

    [Fact]
    public async Task Unused_CallsNotAsked_AreListed()
    {
        var exchange = new ReplayingExchange(
            Capture((Question("SELECT 1;"), Answer("1")), (Question("SELECT 2;"), Answer("2")))
        );
        exchange.Unused.Count.ShouldBe(2);

        _ = await AskAsync(exchange, Question("SELECT 1;"));

        CaptureJson.KeyOf(exchange.Unused.ShouldHaveSingleItem().Request).ShouldContain("SELECT 2;");
    }

    // A fixture is a file: the request found in it must be the one that was asked when it was recorded.
    [Fact]
    public async Task AskAsync_CaptureReadFromItsFile_FindsARequestWithAwkwardText()
    {
        var text = CaptureJson.Write(Capture((Question(Awkward), Answer("1"))));
        var exchange = new ReplayingExchange(CaptureJson.Read(text).Value.ShouldNotBeNull());

        (await AskAsync(exchange, Question(Awkward))).ShouldBe("1");
    }

    [Theory]
    [InlineData("null", "it is null")]
    [InlineData("\"text\"", "")]
    public async Task AskAsync_AnswerThatIsNotOfTheAnswersShape_ThrowsNamingTheMethod(string answer, string reason)
    {
        using var document = JsonDocument.Parse(answer);
        var capture = new DescribeCapture("postgres");
        capture.Add(new CaptureCall("ask", CaptureJson.ToElement(Question()), document.RootElement.Clone()));
        var exchange = new ReplayingExchange(capture);

        var thrown = await Should.ThrowAsync<CaptureMissException>(async () => await AskAsync(exchange, Question()));

        thrown.Message.ShouldStartWith("The capture's answer to 'ask' cannot be read as FakeOpenAnswer: " + reason);
        _live.ShouldBe(0);
    }

    // The two shapes fit: what is replayed can be recorded again, and is the same file.
    [Fact]
    public async Task AskAsync_CapturedWhileReplaying_GivesTheCaptureAgain()
    {
        var told = new FakeDescriber();
        told.Descriptions["GetUser"] = FakeDescriber.TypedRows;
        var first = new DescribeCapture("postgres");
        _ = await DescribeAsync(told, new CapturingExchange(LiveExchange.Instance, first));
        var second = new DescribeCapture("postgres");

        _ = await DescribeAsync(new FakeDescriber(), new CapturingExchange(new ReplayingExchange(first), second));

        CaptureJson.Write(second).ShouldBe(CaptureJson.Write(first));
    }
}
