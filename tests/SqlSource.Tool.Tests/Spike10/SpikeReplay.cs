using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Spike10;

// The steps of FixtureReplay over a ReplayingExchange of a case's capture, giving what happened as a SpikeOutcome
// and throwing nothing.
internal static partial class SpikeReplay
{
    public static Task<SpikeOutcome> RunAsync(FixtureCase fixture) => RunAsync(fixture, fixture.Capture);

    // The same replay over the given text of the capture, so that a mutation needs no file.
    public static async Task<SpikeOutcome> RunAsync(FixtureCase fixture, string captureText)
    {
        try
        {
            return await ReplayAsync(fixture, captureText);
        }
        catch (CaptureMissException exception)
        {
            return Classify(exception);
        }
#pragma warning disable CA1031 // The spike reports any exception as an outcome.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            return new SpikeOutcome(SpikeKind.Thrown, exception.Message);
        }
    }

    private static SpikeOutcome Classify(CaptureMissException exception)
    {
        var match = MissPattern().Match(exception.Message);
        return match.Success
            ? new SpikeOutcome(SpikeKind.Miss, $"{match.Groups["method"].Value} {match.Groups["request"].Value}")
            : new SpikeOutcome(SpikeKind.Unreadable, exception.Message);
    }

    private static async Task<SpikeOutcome> ReplayAsync(FixtureCase fixture, string captureText)
    {
        var read = CaptureJson.Read(captureText);
        if (read.Value is not { } capture)
        {
            return new SpikeOutcome(SpikeKind.Unreadable, read.Failure ?? "the capture cannot be read");
        }

        var token = TestContext.Current.CancellationToken;
        var (planned, dialect) = FixtureReplay.Plan(fixture, token);
        var query = planned.Query;
        var database = planned.Database ?? throw new InvalidOperationException($"The case {fixture} has no database.");

        var opened = await DescriberOf(fixture)
            .OpenAsync(database, FixtureReplay.Connection, new ReplayingExchange(capture));
        if (opened.Value is not { } session)
        {
            return FailureOf(opened.Failure);
        }

        await using (session)
        {
            var described = await session.DescribeAsync(
                new DescribeRequest(query.Name, SampleSql.Build(query), query.Parameters),
                token
            );
            if (described.Value is not { } description)
            {
                return FailureOf(described.Failure);
            }

            var entry = EntryBuilder.Build(planned, dialect, database, session.Server, description);
            return new SpikeOutcome(
                SpikeKind.Entry,
                SidecarWriter.Write(
                    new Sidecar(
                        SidecarFormat.Version,
                        FixtureReplay.ToolVersion,
                        new EquatableArray<SidecarEntry>([entry])
                    )
                )
            );
        }
    }

    private static SpikeOutcome FailureOf(DescribeFailure? failure) =>
        failure is null
            ? new SpikeOutcome(SpikeKind.Thrown, "an outcome has neither a value nor a failure")
            : new SpikeOutcome(SpikeKind.Failure, $"{failure.Descriptor.Id} {failure.Step?.ToString() ?? "open"}");

    private static IQueryDescriber DescriberOf(FixtureCase fixture) =>
        fixture.Engine switch
        {
            "postgres" => new PostgresDescriber(fixture.ExplainSettings),
            "mssql" => new SqlServerDescriber(),
            _ => throw new InvalidOperationException($"No describer is known for the engine of the case {fixture}."),
        };

    [GeneratedRegex(
        @"^The capture has no further answer to '(?<method>[^']*)' for the request (?<request>.*)\.$",
        RegexOptions.Singleline
    )]
    private static partial Regex MissPattern();
}
