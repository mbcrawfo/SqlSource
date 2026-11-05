using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Testing.Bundles;

// The raw bundles of the renaming's tests of an engine: a describer's fixture as a bundle, or a case captured through
// a capturing exchange as diagnose captures one.  Their bundle.json says that no user's run made them: the tool, the
// runtime and the system are "fixture".
internal static class RawBundles
{
    // What bundle.json names for the runtime and the system of a bundle that no user's run made.
    public const string NoRun = "fixture";

    // The member of the answer of "open" that holds the driver's version, the same for every engine.
    private const string Open = "open";
    private const string DriverVersion = "driverVersion";

    // The files of the fixture as a raw bundle whose outcome is its expected.json's entry.  Its bundle.json takes the
    // engine and the server's version from that entry, and the driver's version from the capture's first "open".
    public static Bundle OfFixture(FixtureCase fixture)
    {
        var expected = fixture.Expected;
        if (SidecarReader.Read(expected).Sidecar is not { Queries.Count: 1 } sidecar)
        {
            throw new InvalidOperationException($"The expected.json of the case {fixture} is no sidecar of one entry.");
        }

        var entry = sidecar.Queries.Single();
        var server =
            entry.ServerVersion
            ?? throw new InvalidOperationException($"The expected.json of the case {fixture} has no serverVersion.");
        var capture = fixture.Capture;
        var info = Info(entry.Engine, server, DriverVersionOf(capture, fixture), BundleOutcome.Entry);
        return new Bundle(info, fixture.Query, capture, expected, fixture.ExplainSettings);
    }

    // The case described as FixtureReplay.DescribeThroughAsync describes it, through a capturing exchange over the
    // live one, as a raw bundle: an entry, with its expected.json, or the failure's id and step, with none.  Its
    // bundle.json takes the server's and the driver's versions from the session, and none when no session opened.
    public static async Task<Bundle> CaptureAsync(
        FixtureCase fixture,
        Func<FixtureCase, IQueryDescriber> describer,
        string connection,
        IDescribeExchange live
    )
    {
        var token = TestContext.Current.CancellationToken;
        var (planned, dialect) = FixtureReplay.Plan(fixture, token);
        var query = planned.Query;
        var database = planned.Database ?? throw new InvalidOperationException($"The case {fixture} has no database.");
        var capture = new DescribeCapture(fixture.Engine);

        var opened = await describer(fixture).OpenAsync(database, connection, new CapturingExchange(live, capture));
        if (opened.Value is not { } session)
        {
            var refused =
                opened.Failure
                ?? throw new InvalidOperationException($"The open of the case {fixture} gave no failure.");
            return Raw(fixture, capture, null, Info(fixture.Engine, string.Empty, null, OutcomeOf(refused)));
        }

        await using (session)
        {
            var described = await session.DescribeAsync(
                new DescribeRequest(query.Name, SampleSql.Build(query), query.Parameters),
                token
            );
            var server = session.Server;
            string? expected = null;
            BundleOutcome outcome;
            if (described.Value is { } description)
            {
                var entry = EntryBuilder.Build(planned, dialect, database, server, description);
                expected = SidecarWriter.Write(
                    new Sidecar(
                        SidecarFormat.Version,
                        FixtureReplay.ToolVersion,
                        new EquatableArray<SidecarEntry>([entry])
                    )
                );
                outcome = BundleOutcome.Entry;
            }
            else
            {
                outcome = OutcomeOf(
                    described.Failure
                        ?? throw new InvalidOperationException($"The query of the case {fixture} gave no failure.")
                );
            }

            var info = Info(fixture.Engine, server.Version, server.DriverVersion, outcome);
            return Raw(fixture, capture, expected, info);
        }
    }

    private static Bundle Raw(FixtureCase fixture, DescribeCapture capture, string? expected, BundleInfo info) =>
        new(info, fixture.Query, CaptureJson.Write(capture), expected, fixture.ExplainSettings);

    private static BundleOutcome OutcomeOf(DescribeFailure failure) =>
        BundleOutcome.Failure(failure.Descriptor.Id, failure.Step is { } step ? DescribeStepName.Of(step) : null);

    private static BundleInfo Info(string engine, string serverVersion, string? driverVersion, BundleOutcome outcome) =>
        new(
            BundleJson.Version,
            Renamed: false,
            FixtureReplay.ToolVersion,
            SidecarFormat.Version,
            engine,
            serverVersion,
            driverVersion,
            NoRun,
            NoRun,
            outcome
        );

    // The driver's version that the capture's first "open" answers, or null when it has none.
    private static string? DriverVersionOf(string capture, FixtureCase fixture)
    {
        var read = CaptureJson.Read(capture);
        if (read.Value is not { } calls)
        {
            throw new InvalidOperationException($"The capture of the case {fixture} cannot be read.  {read.Failure}");
        }

        return
            calls.Calls.FirstOrDefault(static call => call.Method == Open) is { } open
            && open.Answer.ValueKind == JsonValueKind.Object
            && open.Answer.TryGetProperty(DriverVersion, out var version)
            && version.ValueKind == JsonValueKind.String
            ? version.GetString()
            : null;
    }
}
