using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Fixtures;

// The harness of a describer's fixtures, proven with the describer of the tests: a case is three files in a
// folder, and replaying its capture gives the text of its expected sidecar.
public sealed class FixtureReplayTests : IDisposable
{
    private const string Query =
        "-- name: GetUser\n"
        + "-- param: @id int4 not null\n"
        + "SELECT id, tags, mood FROM users WHERE id = @id {{filter:AND active}};\n";

    private readonly TempFolder _folder = new();

    public void Dispose() => _folder.Dispose();

    // A case of postgres under the temporary folder, with its query.sql.
    private FixtureCase Case(string name = "typed", string query = Query)
    {
        _ = _folder.WriteFile($"postgres/{name}/{FixtureCase.QueryFile}", query);
        return new FixtureCase("postgres", name, _folder.PathOf($"postgres/{name}"));
    }

    private void Write(FixtureCase fixture, string file, string text) =>
        _ = _folder.WriteFile($"{fixture.Engine}/{fixture.Name}/{file}", text);

    // Describes the case through a describer that knows the query, and writes the two files a recording writes.
    private async Task<FakeDescriber> RecordAsync(FixtureCase fixture)
    {
        var describer = new FakeDescriber();
        describer.Descriptions["GetUser"] = FakeDescriber.TypedRows;
        var capture = new DescribeCapture("postgres");

        var expected = await FixtureReplay.DescribeThroughAsync(
            fixture,
            _ => describer,
            Secrets.ConnectionWithMarker,
            new CapturingExchange(LiveExchange.Instance, capture)
        );

        Write(fixture, FixtureCase.CaptureFile, CaptureJson.Write(capture));
        Write(fixture, FixtureCase.ExpectedFile, expected);
        return describer;
    }

    [Fact]
    public void Case_ItsThreeFiles_AreReadFromItsFolder()
    {
        var fixture = Case();
        Write(fixture, FixtureCase.CaptureFile, "a capture");
        Write(fixture, FixtureCase.ExpectedFile, "a sidecar");

        fixture.Query.ShouldBe(Query);
        fixture.Capture.ShouldBe("a capture");
        fixture.Expected.ShouldBe("a sidecar");
        fixture.ToString().ShouldBe("postgres/typed");
    }

    [Fact]
    public void Of_EngineWithNoFolder_IsEmpty() => FixtureCases.Of("no-such-engine").ShouldBeEmpty();

    [Fact]
    public void Of_EngineWithCases_GivesThemInNameOrder()
    {
        _ = Case("b");
        _ = Case("a");
        _ = _folder.WriteFile("postgres/schema.sql", "CREATE TABLE users ();\n");

        var cases = FixtureCases.Of("postgres", _folder.Path);

        cases.Select(found => found.Name).ShouldBe(["a", "b"]);
        cases.ShouldAllBe(found => found.Engine == "postgres");
        cases[0].Folder.ShouldBe(_folder.PathOf("postgres/a"));
    }

    [Fact]
    public async Task DescribeAsync_CaseRecordedFromADescriber_ReplaysToTheSameText()
    {
        var fixture = Case();
        var recorded = await RecordAsync(fixture);
        var untold = new FakeDescriber();

        var replayed = await FixtureReplay.DescribeAsync(fixture, _ => untold);

        replayed.ShouldBe(fixture.Expected);
        replayed.ShouldContain("\"fixture\"");
        replayed.ShouldContain("\"16.4\"");
        replayed.ShouldContain("_text");
        untold.Opened.ShouldBeEmpty();
        untold.Described.ShouldBeEmpty();
        var asked = recorded.Described.ShouldHaveSingleItem();
        asked.Sql.ShouldContain("WHERE id = @id AND active");
        asked.Parameters.ShouldHaveSingleItem().Type.ShouldBe("int4");
    }

    [Fact]
    public async Task DescribeAsync_CaseRecordedWithAConnection_HoldsNothingOfIt()
    {
        var fixture = Case();
        var recorded = await RecordAsync(fixture);

        recorded.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.ConnectionWithMarker);
        fixture.Capture.ShouldNotContain(Secrets.Marker);
        fixture.Expected.ShouldNotContain(Secrets.Marker);
    }

    // Nothing opens under a replay: the connection is one fixed word, and the exchange is the replaying one.
    [Fact]
    public async Task DescribeAsync_Replay_OpensWithTheConstantConnectionAndAReplayingExchange()
    {
        var fixture = Case();
        _ = await RecordAsync(fixture);
        FixtureCase? madeFor = null;
        OpenRequest? seen = null;

        _ = await FixtureReplay.DescribeAsync(
            fixture,
            given =>
            {
                madeFor = given;
                return new Probe(new FakeDescriber(), request => seen = request);
            }
        );

        madeFor.ShouldBe(fixture);
        var request = seen.ShouldNotBeNull();
        request.Connection.ShouldBe(FixtureReplay.Connection);
        FixtureReplay.Connection.ShouldBe("replay");
        request.Database.ShouldBe("postgres");
        _ = request.Exchange.ShouldBeOfType<ReplayingExchange>();
    }

    [Fact]
    public async Task DescribeAsync_CaptureWithAChangedAnswer_GivesAnotherText()
    {
        var fixture = Case();
        _ = await RecordAsync(fixture);
        Write(
            fixture,
            FixtureCase.CaptureFile,
            fixture.Capture.Replace(
                "\"internalName\": \"int4\"",
                "\"internalName\": \"int8\"",
                StringComparison.Ordinal
            )
        );

        var replayed = await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber());

        replayed.ShouldNotBe(fixture.Expected);
        replayed.ShouldContain("int8");
    }

    // A change to what a describer asks is a visible update of its fixtures and never a silent pass.
    [Fact]
    public async Task DescribeAsync_QueryThatChangedSinceItWasRecorded_IsACallTheCaptureLacks()
    {
        var fixture = Case();
        _ = await RecordAsync(fixture);
        Write(fixture, FixtureCase.QueryFile, Query.Replace("AND active", "AND NOT active", StringComparison.Ordinal));

        var thrown = await Should.ThrowAsync<CaptureMissException>(async () =>
            await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber())
        );

        thrown.Message.ShouldContain("'describe'");
        thrown.Message.ShouldContain("AND NOT active");
    }

    [Fact]
    public async Task DescribeAsync_CaptureOfAnotherEngine_FailsNamingBoth()
    {
        var fixture = Case();
        Write(fixture, FixtureCase.CaptureFile, CaptureJson.Write(new DescribeCapture("mssql")));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber())
        );

        thrown.Message.ShouldBe("The capture of the case postgres/typed is of the engine 'mssql'.");
    }

    [Fact]
    public async Task DescribeAsync_CaptureThatCannotBeRead_FailsNamingTheCase()
    {
        var fixture = Case();
        Write(fixture, FixtureCase.CaptureFile, "{");

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber())
        );

        thrown.Message.ShouldStartWith(
            "The capture of the case postgres/typed cannot be read.  The capture is not JSON"
        );
    }

    [Theory]
    [InlineData("")]
    [InlineData("-- name: One\nSELECT 1;\n\n-- name: Two\nSELECT 2;\n")]
    [InlineData("-- dialect: mssql\n-- name: One\nSELECT 1;\n")]
    public async Task DescribeAsync_QueryFileThatIsNotOneQuery_FailsNamingTheCase(string query)
    {
        var fixture = Case("odd", query);
        Write(fixture, FixtureCase.CaptureFile, CaptureJson.Write(new DescribeCapture("postgres")));

        var thrown = await Should.ThrowAsync<InvalidOperationException>(async () =>
            await FixtureReplay.DescribeAsync(fixture, _ => new FakeDescriber())
        );

        thrown.ShouldNotBeOfType<CaptureMissException>();
        thrown.Message.ShouldContain("query.sql of the case postgres/odd");
    }

    // A describer that shows the request it is opened with, and hands it on.  Under a replay FakeDescriber keeps
    // no request: its "live" never runs.
    private sealed class Probe(IQueryDescriber inner, Action<OpenRequest> opened) : IQueryDescriber
    {
        public SqlDialect Dialect => inner.Dialect;

        public Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
            OpenRequest request,
            IRunLog log,
            CancellationToken cancellationToken
        )
        {
            opened(request);
            return inner.OpenAsync(request, log, cancellationToken);
        }
    }
}
