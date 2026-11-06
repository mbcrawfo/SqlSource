using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// Step 3 of the run with a capture, as diagnose runs it: every call of the one session is recorded, a refusal is
// kept on the query and not printed, and the capture is written when the session closes, or refused when it holds
// the connection's value.
public sealed class DatabaseRunsCaptureTests : IDisposable
{
    private readonly DatabaseRunsScene _scene = new();

    public void Dispose() => _scene.Dispose();

    // A project of one database, whose queries one session describes.
    private TestProject OneDatabase()
    {
        var project = Plans.Postgres(_scene.Folder, "Diagnosed");
        _ = project.AddSql("Q.sql", "-- name: GetUser\nSELECT 1 WHERE @id = 1;\n\n-- name: GetName\nSELECT 2;\n");
        return project;
    }

    private Task<ImmutableArray<FileWork>> RunAsync(RunCapture? capture) =>
        _scene.RunAsync(Plans.Of(OneDatabase()), given: ["postgres=" + Secrets.ConnectionWithWord], capture: capture);

    [Fact]
    public async Task RunAsync_WithACapture_RecordsEveryCallAndWritesItOnClose()
    {
        var capture = new RunCapture(SidecarValues.Engine.Postgres);

        var files = await RunAsync(capture);

        _scene.Error.ToString().ShouldBeEmpty();
        DatabaseRunsScene.States(files).ShouldBe([QueryState.Described, QueryState.Described]);
        // The run's exchange records in front of the host's, which recorded the same calls.
        _scene.Describer.Opened.ShouldHaveSingleItem().Exchange.ShouldNotBe(_scene.Exchange);
        capture.Server.ShouldBe(_scene.Describer.Server);
        var text = capture.Text.ShouldNotBeNull();
        text.ShouldBe(CaptureJson.Write(_scene.Capture));
        var read = CaptureJson.Read(text).Value.ShouldNotBeNull();
        read.Engine.ShouldBe(SidecarValues.Engine.Postgres);
        read.Calls.Select(call => call.Method).ShouldBe(["open", "describe", "describe"]);
    }

    [Fact]
    public async Task RunAsync_WithACapture_ARefusalIsKeptAndNotWritten()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "42P01: relation does not exist");
        var capture = new RunCapture(SidecarValues.Engine.Postgres);

        var files = await RunAsync(capture);

        var refused = files[0].Queries[0];
        refused.State.ShouldBe(QueryState.Failed);
        var failure = refused.Failure.ShouldNotBeNull();
        failure.Descriptor.Id.ShouldBe(FakeDescriber.Rejected.Id);
        failure.ServerLines.ToArray().ShouldBe(["42P01: relation does not exist"]);
        files[0].Queries[1].Failure.ShouldBeNull();
        _scene.Error.ToString().ShouldBeEmpty();
        var reporter = _scene.Reporter.ShouldNotBeNull();
        reporter.Count.ShouldBe(1);
        reporter.UnwrittenCount.ShouldBe(1);
        // A refused query makes a bundle with its failure, so its capture is written too.
        _ = capture.Text.ShouldNotBeNull();
    }

    // A capture goes into a bundle that is sent: a value in it is a bug of the describer, as in an entry.
    [Fact]
    public async Task RunAsync_ACaptureThatHoldsTheSecret_IsSQLSRC200()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure(
            "GetUser",
            $"could not connect to {Secrets.ConnectionWithWord}"
        );
        var capture = new RunCapture(SidecarValues.Engine.Postgres);

        var fault = await Should.ThrowAsync<DescriberFaultException>(() => RunAsync(capture));

        fault.InnerException.ShouldBeNull();
        fault.Diagnostic.Descriptor.Id.ShouldBe("SQLSRC200");
        fault
            .Diagnostic.Arguments.ToArray()
            .ShouldBe([
                "System.InvalidOperationException",
                "The capture holds the connection's value or a password of it.",
            ]);
        fault.Message.ShouldNotContain(Secrets.Word);
        capture.Text.ShouldBeNull();
        _scene.Describer.Closed.ShouldBe(1);
        _scene.Error.ToString().ShouldBeEmpty();
        // The failure kept on the query is the redacted one.
        _scene
            .Files[0]
            .Queries[0]
            .Failure.ShouldNotBeNull()
            .ServerLines.ShouldAllBe(line => !line.Contains(Secrets.Word, StringComparison.Ordinal));
    }

    [Fact]
    public async Task RunAsync_WithoutACapture_IsUnchanged()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var files = await RunAsync(capture: null);

        _scene.Describer.Opened.ShouldHaveSingleItem().Exchange.ShouldBe(_scene.Exchange);
        _scene.Error.ToString().ShouldStartWith($"{files[0].File.Path}(1,10): error SQLSRC999: ");
        var reporter = _scene.Reporter.ShouldNotBeNull();
        reporter.Count.ShouldBe(1);
        reporter.UnwrittenCount.ShouldBe(0);
    }

    // A capture is of one session: the calls of two would replay as neither.
    [Fact]
    public async Task RunAsync_ACaptureAndTwoDatabases_IsAFaultOfTheTool()
    {
        var capture = new RunCapture(SidecarValues.Engine.Postgres);

        var exception = await Should.ThrowAsync<InvalidOperationException>(() => _scene.RunAsync(capture: capture));

        exception.ShouldNotBeOfType<DescriberFaultException>();
        _scene.Describer.Opened.ShouldHaveSingleItem().Database.ShouldBe("postgres");
    }
}
