using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Fixtures;

namespace SqlSource.Tool.Testing.Bundles;

// The raw bundle of a capture, as diagnose writes one before the renaming: its files, with a bundle.json that says it
// is not renamed and takes the engine from the capture and the server's and driver's versions from its open.  What
// made it is "fixture", so that no test reads the host it runs on.
internal static class RawBundles
{
    // What made the bundle: its tool, runtime and system.
    public const string Maker = FixtureReplay.ToolVersion;

    // The members of the answer of "open" that hold the server's version, PostgreSQL's and SQL Server's, and the
    // driver's version, the same for every engine.
    private const string ServerVersion = "serverVersion";
    private const string ProductVersion = "productVersion";
    private const string DriverVersion = "driverVersion";

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

    // The case described as FixtureReplay.DescribeOrRefuseThroughAsync describes it, through a capturing exchange over
    // the live one, as diagnose captures one, as a raw bundle: an entry, with its expected.json, or the failure's id
    // and step for a query the describer refuses, with none.  A session that does not open throws.
    public static async Task<Bundle> CaptureAsync(
        FixtureCase fixture,
        Func<FixtureCase, IQueryDescriber> describer,
        string connection,
        IDescribeExchange live
    )
    {
        var capture = new DescribeCapture(fixture.Engine);
        var described = await FixtureReplay.DescribeOrRefuseThroughAsync(
            fixture,
            describer,
            connection,
            new CapturingExchange(live, capture)
        );
        var outcome = described.Succeeded ? BundleOutcome.Entry : OutcomeOf(described.Failure);
        return Of(fixture.Query, capture, described.Value, outcome, fixture.ExplainSettings);
    }

    // A raw bundle of a query and its capture, with the expected.json and the planner settings given.  The server's
    // version is the open's serverVersion, as PostgreSQL's answers it, or its productVersion, as SQL Server's does.
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
            StringOf(open.Answer, ServerVersion)
                ?? StringOf(open.Answer, ProductVersion)
                ?? throw new InvalidOperationException("The capture's open answers no server's version."),
            StringOf(open.Answer, DriverVersion),
            Maker,
            Maker,
            outcome
        );
        return new Bundle(info, query, CaptureJson.Write(capture), expected, explainSettings);
    }

    private static BundleOutcome OutcomeOf(DescribeFailure failure) =>
        BundleOutcome.Failure(failure.Descriptor.Id, failure.Step is { } step ? DescribeStepName.Of(step) : null);

    private static string? StringOf(JsonElement answer, string member) =>
        answer.ValueKind == JsonValueKind.Object
        && answer.TryGetProperty(member, out var value)
        && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
