using System;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Testing.Fixtures;

// How the fixture cases of an engine are described against its server: compared with the case's expected.json, or,
// when a person asks for it, recorded.  tools/record-fixtures.sh asks by setting RecordVariable for the test class of
// each engine.
internal static class FixtureCaseRuns
{
    public const string RecordVariable = "SQLSOURCE_RECORD_FIXTURES";

    // As SidecarWriter writes the key.
    private const string VersionKey = "\"serverVersion\": \"";

    // Whether the run records the expectations it would compare: the variable is set and not empty.
    public static bool IsRecording => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(RecordVariable));

    // Describes the case against the server and compares the sidecar with the case's expected.json, which only the
    // version of the server may differ in.  When recording, it writes the case's capture.json and expected.json
    // instead and compares nothing.
    public static async Task ShouldDescribeOrRecordAsync(
        FixtureCase fixture,
        string engine,
        Func<FixtureCase, IQueryDescriber> describer,
        string connection
    )
    {
        if (IsRecording)
        {
            var capture = new DescribeCapture(engine);
            var recorded = await FixtureReplay.DescribeThroughAsync(
                fixture,
                describer,
                connection,
                new CapturingExchange(LiveExchange.Instance, capture)
            );

            await FixtureCaseFiles.WriteRecordingAsync(
                fixture,
                capture,
                recorded,
                SecretOf(fixture, describer(fixture), connection),
                TestContext.Current.CancellationToken
            );
            return;
        }

        var live = await FixtureReplay.DescribeThroughAsync(fixture, describer, connection, LiveExchange.Instance);

        WithOneServerVersion(live).ShouldBe(WithOneServerVersion(fixture.Expected));
    }

    // What a run removes of the connection, made as DatabaseRuns makes it, for the database the case is planned for.
    private static ConnectionSecret SecretOf(FixtureCase fixture, IQueryDescriber describer, string connection)
    {
        var (planned, dialect) = FixtureReplay.Plan(fixture, TestContext.Current.CancellationToken);
        var database = planned.Database ?? throw new InvalidOperationException($"The case {fixture} has no database.");
        return new ConnectionSecret(
            connection,
            describer.SecretsOf(connection, Environment.GetEnvironmentVariable),
            [database, SqlDialectName.Canonical(dialect)],
            [describer.RewriteOf(connection)]
        );
    }

    // An expected.json keeps the version it was recorded against.  CI describes the same cases on two servers, so the
    // two texts are compared with one value there; each engine's FixtureVersionTests holds that version to the
    // default image.
    public static string WithOneServerVersion(string sidecar)
    {
        var key = sidecar.IndexOf(VersionKey, StringComparison.Ordinal);
        key.ShouldBeGreaterThanOrEqualTo(0, "the entry has no serverVersion");
        var value = key + VersionKey.Length;
        var end = sidecar.IndexOf('"', value);
        end.ShouldBeGreaterThanOrEqualTo(0, "the serverVersion of the entry has no closing quote");
        return string.Concat(sidecar.AsSpan(0, value), "*", sidecar.AsSpan(end));
    }
}
