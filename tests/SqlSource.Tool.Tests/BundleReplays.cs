using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests;

// A bundle replayed as replay runs it, for the tests of Bundles/: its capture, its engine, its plan, its describer and
// its run, each step required to succeed.
internal static class BundleReplays
{
    // The SQLSTATE and the message of the PostgreSQL that cannot tell the type of a parameter.
    public const string NotDeterminedState = "42P18";

    public const string NotDeterminedMessage = "could not determine data type of parameter $1";

    // The bundle of a recorded case of the describers' fixtures.
    public static Bundle OfCase(string engine, string name) =>
        WrittenBundles.Read(FixtureCases.Named(engine, name).Folder);

    // The bundle with its capture.json replaced by the text of another capture.
    public static Bundle WithCapture(Bundle bundle, DescribeCapture capture) =>
        bundle with
        {
            Capture = CaptureJson.Write(capture),
        };

    // The capture of a bundle, which must read.
    public static DescribeCapture CaptureOf(Bundle bundle) => CaptureJson.Read(bundle.Capture).Value.ShouldNotBeNull();

    // Replays the bundle at the path with the real host's describers, or with those given.
    public static async Task<ReplayOutcome> ReplayAsync(
        Bundle bundle,
        string path,
        DescriberRegistry? registry = null,
        IRunLog? log = null
    )
    {
        var token = TestContext.Current.CancellationToken;
        var capture = BundleReplay.CaptureOf(bundle, path);
        capture.Failure.ShouldBeNull();
        var engine = BundleReplay.EngineOf(bundle);
        var planned = BundleReplay.Plan(bundle, engine, path, token);
        planned.Failure.ShouldBeNull();
        var (query, dialect) = planned.Value;
        var describer = BundleDescribers.For(
            dialect,
            registry ?? ToolHost.Create().Describers,
            bundle.ExplainSettings,
            engine,
            path
        );
        describer.Failure.ShouldBeNull();

        return await BundleReplay.RunAsync(
            new PreparedReplay(
                query,
                dialect,
                capture.Value.ShouldNotBeNull(),
                describer.Value.ShouldNotBeNull(),
                Expected: null
            ),
            log ?? NullRunLog.Instance,
            new TestClock(),
            token
        );
    }

    // A PostgreSQL bundle whose prepare the server refused, as it does a parameter it cannot tell the type of: the
    // open and prepare calls of the recorded case update-inferred, with prepare's answer the server's error 42P18.
    // Every member of PostgresPrepareAnswer and of PostgresServerError is written, as a recording writes it.
    public static Bundle PrepareTypeNotDetermined()
    {
        var recorded = OfCase("postgres", "update-inferred");
        var calls = CaptureOf(recorded).Calls;
        var capture = new DescribeCapture("postgres");
        capture.Add(calls.Single(static call => call.Method == "open"));
        using var answer = JsonDocument.Parse(
            $$"""
            {
                "parameterTypes": null,
                "error": {
                    "sqlState": "{{NotDeterminedState}}",
                    "message": "{{NotDeterminedMessage}}",
                    "detail": null,
                    "hint": null,
                    "position": null
                }
            }
            """
        );
        var prepare = calls.Single(static call => call.Method == "prepare");
        capture.Add(new CaptureCall(prepare.Method, prepare.Request, answer.RootElement.Clone()));

        return new Bundle(Info: null, recorded.Query, CaptureJson.Write(capture), Expected: null, []);
    }
}
