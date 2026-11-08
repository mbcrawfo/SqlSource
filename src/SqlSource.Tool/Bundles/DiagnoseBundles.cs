using System;
using System.Threading;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Planning;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// The bundles of <c>diagnose</c>: the raw bundle of a query described through a capturing exchange, before any
/// renaming.
/// </summary>
internal static class DiagnoseBundles
{
    /// <summary>
    /// The raw bundle of the query: its <c>query.sql</c> as <see cref="BundleQueryFile" /> writes it, the capture as
    /// the run wrote it, a <c>bundle.json</c> that is not renamed, and, for an entry, an <c>expected.json</c> of it
    /// alone, with the hash <see cref="BundleReplay.Plan" /> gives the bundle's <c>query.sql</c>, so that the bundle
    /// replays to its own answer.  It has no <c>explain-settings.txt</c>: the run's describer is the host's.
    /// </summary>
    /// <param name="query">The query, as the plan holds it.</param>
    /// <param name="fileText">The text of its file, as the plan read it.</param>
    /// <param name="dialect">The dialect the file was read by.</param>
    /// <param name="described">
    /// What describing the query gave: the capture of its session, which opened and closed, and the entry or the
    /// describer's refusal.
    /// </param>
    /// <param name="host">Where the tool runs, which <c>bundle.json</c> names.</param>
    /// <exception cref="ArgumentException">
    /// Neither an entry nor a failure, or both, are given; the session did not open or did not close; or the text is
    /// not that of the query's file.
    /// </exception>
    public static Bundle Raw(
        PlannedQuery query,
        string fileText,
        SqlDialect dialect,
        DescribedQuery described,
        ToolHost host
    )
    {
        var (capture, entry, failure) = described;
        if ((entry is null) == (failure is null))
        {
            throw new ArgumentException(
                "A described query has an entry or a failure, and only one.",
                nameof(described)
            );
        }

        var server =
            capture.Server ?? throw new ArgumentException("The run's session was never opened.", nameof(described));
        var text = capture.Text ?? throw new ArgumentException("The run's session did not close.", nameof(described));
        var engine = capture.Capture.Engine;
        var outcome = failure is null ? BundleOutcome.Entry : OutcomeOf(failure);
        var info = new BundleInfo(
            BundleJson.Version,
            Renamed: false,
            PackageVersion.Prefix,
            SidecarFormat.Version,
            engine,
            server.Version,
            server.DriverVersion,
            host.Runtime,
            host.OperatingSystem,
            outcome
        );

        var bundle = new Bundle(
            info,
            BundleQueryFile.Of(query, fileText, dialect),
            text,
            Expected: null,
            ExplainSettings: []
        );
        return entry is null ? bundle : bundle with { Expected = ExpectedOf(bundle, entry) };
    }

    // A refusal as bundle.json names it: its id, and its step as an error prints it, or none.
    private static BundleOutcome OutcomeOf(DescribeFailure failure) =>
        BundleOutcome.Failure(failure.Descriptor.Id, failure.Step is { } step ? DescribeStepName.Of(step) : null);

    // The sidecar of the entry alone, with the hash of the bundle's query.sql.
    private static string ExpectedOf(Bundle bundle, SidecarEntry entry)
    {
        // A query.sql that BundleQueryFile wrote always plans.
        var expected = ExpectedFile.Of(
            bundle.Query,
            bundle.Info!.Engine,
            entry,
            SidecarFormat.Version,
            PackageVersion.Prefix,
            CancellationToken.None
        );
        return expected.Succeeded
            ? expected.Value
            : throw new InvalidOperationException(
                $"The query.sql of the query '{entry.Name}' does not plan: {expected.Failure.Descriptor.Id}."
            );
    }
}
