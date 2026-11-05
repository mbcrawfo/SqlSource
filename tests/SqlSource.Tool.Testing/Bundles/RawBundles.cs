using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Testing.Fixtures;

namespace SqlSource.Tool.Testing.Bundles;

// The raw bundle of a capture, as diagnose writes one before the renaming: its files, with a bundle.json that says it
// is not renamed and takes the engine from the capture and the server's and driver's versions from its open.  What
// made it is "fixture", so that no test reads the host it runs on.
internal static class RawBundles
{
    // What made the bundle: its tool, runtime and system.
    public const string Maker = FixtureReplay.ToolVersion;

    // A case of the describers' fixtures as a raw bundle: its query, capture, expected.json and planner settings, with
    // an entry outcome, or the failure given.
    public static Bundle OfFixture(FixtureCase fixture, string? failureId = null, string? failureStep = null)
    {
        var read = CaptureJson.Read(fixture.Capture);
        var capture =
            read.Value
            ?? throw new InvalidOperationException(
                $"The capture of the case {fixture} cannot be read.  {read.Failure}"
            );
        var outcome = failureId is null ? BundleOutcome.Entry : BundleOutcome.Failure(failureId, failureStep);
        return Of(fixture.Query, capture, fixture.Expected, outcome, fixture.ExplainSettings);
    }

    // A raw bundle of a query and its capture, with the expected.json and the planner settings given.
    public static Bundle Of(
        string query,
        DescribeCapture capture,
        string? expected,
        BundleOutcome outcome,
        IReadOnlyList<string> explainSettings
    )
    {
        var open =
            capture.Calls.FirstOrDefault(static call => call.Method == "open")
            ?? throw new InvalidOperationException("The capture has no call 'open'.");
        var info = new BundleInfo(
            BundleJson.Version,
            Renamed: false,
            Maker,
            SidecarFormat.Version,
            capture.Engine,
            StringOf(open.Answer, "serverVersion")
                ?? throw new InvalidOperationException("The capture's open answers no serverVersion."),
            StringOf(open.Answer, "driverVersion"),
            Maker,
            Maker,
            outcome
        );
        return new Bundle(info, query, CaptureJson.Write(capture), expected, explainSettings);
    }

    private static string? StringOf(JsonElement answer, string member) =>
        answer.ValueKind == JsonValueKind.Object
        && answer.TryGetProperty(member, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
