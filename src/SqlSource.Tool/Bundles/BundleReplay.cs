using System;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using SqlSource.Generation;
using SqlSource.Generation.Sidecars;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Snapshot.Reading;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Bundles;

/// <summary>
/// Replays a bundle: its one query planned as a run plans it, and described by its engine's describer from its
/// capture, with no database.  <c>replay</c> and the renaming's proof share it.
/// </summary>
/// <remarks>
/// <para>
/// The steps are apart, so that a caller reports what each gives: <see cref="CaptureOf" />, <see cref="EngineOf" />,
/// <see cref="Plan" />, <c>BundleDescribers.For</c>, then <see cref="RunAsync" />, whose outcome is compared with
/// <see cref="ExpectedOf" />'s.  Every error is at the bundle's path, through <see cref="BundleErrors" />.
/// </para>
/// <para>
/// A question that the capture does not answer is a <see cref="ReplayOutcome.Missed" />, never an exception: it is
/// what a bundle of another version gives, and no bug.  Any other exception of the describer is one, and is let
/// through.
/// </para>
/// </remarks>
internal static class BundleReplay
{
    /// <summary>The connection of a replayed session.  Nothing opens, and the describer needs a value.</summary>
    public const string Connection = "replay";

    /// <summary>The method of a miss that names none, as <c>SQLSRC290</c> quotes it.</summary>
    public const string UnknownQuestion = "an unknown question";

    private const string VersionKey = "captureVersion";
    private const string EngineKey = "engine";

    /// <summary>
    /// The bundle's capture, or the error that says why it cannot be read: <c>SQLSRC289</c> for a
    /// <c>captureVersion</c> newer than this tool reads, and <c>SQLSRC288</c> for anything else, an engine other than
    /// <c>bundle.json</c>'s among them.
    /// </summary>
    /// <param name="bundle">The bundle.</param>
    /// <param name="path">The bundle's full path, where an error is.</param>
    public static Outcome<DescribeCapture, ToolDiagnostic> CaptureOf(Bundle bundle, string path)
    {
        // Before the capture is read, since a newer form may have changed its other members.
        if (
            MemberOf(bundle.Capture, VersionKey) is { ValueKind: JsonValueKind.Number } version
            && version.TryGetInt32(out var number)
            && number > DescribeCapture.Version
        )
        {
            return Outcome<DescribeCapture, ToolDiagnostic>.Failed(
                BundleErrors.OfNewerTool(path, VersionKey, number, DescribeCapture.Version)
            );
        }

        var read = CaptureJson.Read(bundle.Capture);
        if (!read.Succeeded)
        {
            return Failed<DescribeCapture>(path, AsReason(read.Failure));
        }

        var capture = read.Value;
        return bundle.Info is { } info && !string.Equals(info.Engine, capture.Engine, StringComparison.Ordinal)
            ? Failed<DescribeCapture>(
                path,
                $"{BundleFiles.Info} names the engine '{info.Engine}', and {BundleFiles.Capture} '{capture.Engine}'"
            )
            : Outcome<DescribeCapture, ToolDiagnostic>.Of(capture);
    }

    /// <summary>
    /// The engine of the bundle: its <c>bundle.json</c>'s, or else its capture's.  A capture with no engine gives the
    /// empty text, which is no engine: <see cref="CaptureOf" /> says what is wrong with it.
    /// </summary>
    public static string EngineOf(Bundle bundle) =>
        bundle.Info?.Engine
        ?? (
            MemberOf(bundle.Capture, EngineKey) is { ValueKind: JsonValueKind.String } engine
                ? engine.GetString() ?? string.Empty
                : string.Empty
        );

    /// <summary>
    /// The one query of <c>query.sql</c> as the plan of a run holds it: read in the engine's dialect, with the hash of
    /// its SQL, for the database its markers name or else the one named as the dialect.
    /// </summary>
    /// <remarks>
    /// An engine that is no describable dialect is <c>SQLSRC291</c>.  A file that cannot be parsed, that does not hold
    /// exactly one query, or whose <c>-- dialect:</c> names another dialect is <c>SQLSRC288</c>.
    /// </remarks>
    /// <param name="bundle">The bundle.</param>
    /// <param name="engine">The bundle's engine, as <see cref="EngineOf" /> gives it.</param>
    /// <param name="path">The bundle's full path, where an error is.</param>
    /// <param name="token">Ends the parse.</param>
    public static Outcome<(PlannedQuery Query, SqlDialect Dialect), ToolDiagnostic> Plan(
        Bundle bundle,
        string engine,
        string path,
        CancellationToken token
    )
    {
        if (!SqlDialectName.TryParse(engine, out var choice) || !SqlDescribableDialects.Contains(choice.Dialect))
        {
            return Outcome<(PlannedQuery, SqlDialect), ToolDiagnostic>.Failed(BundleErrors.NoDescriber(path, engine));
        }

        var parsed = SqlFileReader.Read(
            new QueryText(Path.Combine(path, BundleFiles.Query), bundle.Query),
            normalizedPath: null,
            choice,
            commentsWanted: false,
            token
        );
        if (parsed.Errors.Count > 0)
        {
            var error = parsed.Errors[0].ToDiagnostic();
            return Failed<(PlannedQuery, SqlDialect)>(
                path,
                $"{BundleFiles.Query} cannot be parsed: {error.Id}: {error.GetMessage(CultureInfo.InvariantCulture)}"
            );
        }

        if (parsed.Queries.Count != 1)
        {
            return Failed<(PlannedQuery, SqlDialect)>(
                path,
                $"{BundleFiles.Query} holds {parsed.Queries.Count} queries, and a bundle holds one"
            );
        }

        if (parsed.Dialect != choice.Dialect)
        {
            return Failed<(PlannedQuery, SqlDialect)>(
                path,
                $"{BundleFiles.Query} is read as '{SqlDialectName.Canonical(parsed.Dialect)}', and the bundle is of "
                    + $"the engine '{engine}'"
            );
        }

        var query = parsed.Queries[0];
        return Outcome<(PlannedQuery, SqlDialect), ToolDiagnostic>.Of(
            (
                new PlannedQuery(
                    query,
                    NeedsEntry: true,
                    query.Markers.Database ?? SqlDialectName.Canonical(parsed.Dialect),
                    SqlQueryHash.Compute(parsed.Dialect, query.Segments, query.Tokens, query.Parameters)
                ),
                parsed.Dialect
            )
        );
    }

    /// <summary>
    /// Describes the query as a run does, answered from the capture: its entry, the describer's refusal, or the
    /// question the capture does not answer.
    /// </summary>
    /// <remarks>
    /// The describer writes to <paramref name="log" /> with the engine before each event's name, as a run's log has
    /// them.  A <see cref="CaptureMissException" /> of opening, describing or closing the session is
    /// <see cref="ReplayOutcome.Missed" />, of <see cref="UnknownQuestion" /> when it names no method; any other
    /// exception is a bug, and is let through.
    /// </remarks>
    /// <param name="query">The query as <see cref="Plan" /> gives it.</param>
    /// <param name="dialect">Its dialect as <see cref="Plan" /> gives it.</param>
    /// <param name="capture">The calls to answer from, as <see cref="CaptureOf" /> gives them.</param>
    /// <param name="describer">The describer of the dialect, as <c>BundleDescribers.For</c> gives it.</param>
    /// <param name="log">Where the describer's events go.</param>
    /// <param name="time">The clock the describer times its calls with.</param>
    /// <param name="token">Ends the replay.</param>
    public static async Task<ReplayOutcome> RunAsync(
        PlannedQuery query,
        SqlDialect dialect,
        DescribeCapture capture,
        IQueryDescriber describer,
        IRunLog log,
        TimeProvider time,
        CancellationToken token
    )
    {
        var database = query.Database ?? SqlDialectName.Canonical(dialect);
        try
        {
            var opened = await describer.OpenAsync(
                new OpenRequest(database, Connection, new ReplayingExchange(capture), time),
                new PrefixedLog(log, SqlDialectName.Canonical(dialect)),
                token
            );
            if (!opened.Succeeded)
            {
                return RefusedBy(opened.Failure);
            }

            await using var session = opened.Value;
            var sql = query.Query;
            var described = await session.DescribeAsync(
                new DescribeRequest(sql.Name, SampleSql.Build(sql, dialect), sql.Parameters),
                token
            );
            return described.Succeeded
                ? new ReplayOutcome.Described(
                    EntryBuilder.Build(query, dialect, database, session.Server, described.Value)
                )
                : RefusedBy(described.Failure);
        }
        catch (CaptureMissException miss)
        {
            return new ReplayOutcome.Missed(miss.Method ?? UnknownQuestion, miss.Message);
        }
    }

    /// <summary>
    /// What the bundle says describing its query gave: the one entry of <c>expected.json</c>, else the failure of
    /// <c>bundle.json</c>, else null.  An <c>expected.json</c> of a sidecar format newer than this tool reads is
    /// <c>SQLSRC289</c>, as a folder may have no <c>bundle.json</c> to say it; one that is no sidecar this tool reads
    /// otherwise, or that does not hold exactly one entry, is <c>SQLSRC288</c>.
    /// </summary>
    /// <remarks>
    /// The entry has no <see cref="SidecarEntry.NameSpan" />, as one that a replay gives has none, so that the two are
    /// equal when they describe the same.
    /// </remarks>
    /// <param name="bundle">The bundle.</param>
    /// <param name="path">The bundle's full path, where an error is.</param>
    public static Outcome<ReplayOutcome?, ToolDiagnostic> ExpectedOf(Bundle bundle, string path)
    {
        if (bundle.Expected is not { } text)
        {
            return Outcome<ReplayOutcome?, ToolDiagnostic>.Of(bundle.Info is { } info ? OutcomeOf(info.Outcome) : null);
        }

        var read = SidecarReader.Read(text);
        if (read.Error is { } error)
        {
            return Failed<ReplayOutcome?>(
                path,
                $"{BundleFiles.Expected} is not a sidecar: {SidecarFileChecks.ReasonOf(error)}"
            );
        }

        if (read.Sidecar is not { } sidecar)
        {
            if (read.FormatVersion is { } version && version > SidecarFormat.Version)
            {
                return Outcome<ReplayOutcome?, ToolDiagnostic>.Failed(
                    BundleErrors.OfNewerTool(path, SidecarKeys.FormatVersion, version, SidecarFormat.Version)
                );
            }

            return Failed<ReplayOutcome?>(
                path,
                $"{BundleFiles.Expected} is of the sidecar format {read.FormatVersion}, and this version of "
                    + $"sqlsource reads {SidecarFormat.Version}"
            );
        }

        return sidecar.Queries.Count == 1
            ? Outcome<ReplayOutcome?, ToolDiagnostic>.Of(
                new ReplayOutcome.Described(sidecar.Queries[0] with { NameSpan = default })
            )
            : Failed<ReplayOutcome?>(
                path,
                $"{BundleFiles.Expected} holds {sidecar.Queries.Count} entries, and a bundle's holds one"
            );
    }

    // What bundle.json says was given when there is no expected.json: a failure, or nothing to compare with.
    private static ReplayOutcome.Refused? OutcomeOf(BundleOutcome outcome) =>
        outcome.Kind switch
        {
            BundleOutcomeKind.Entry => null,
            BundleOutcomeKind.Failure => new ReplayOutcome.Refused(
                outcome.Id ?? throw new InvalidOperationException("A failure of bundle.json has no id."),
                outcome.Step
            ),
            _ => throw UnexpectedEnumValueException.Of(outcome.Kind),
        };

    private static ReplayOutcome.Refused RefusedBy(DescribeFailure failure) =>
        new(failure.Descriptor.Id, failure.Step is { } step ? DescribeStepName.Of(step) : null);

    // A sentence of CaptureJson.Read as the reason of SQLSRC288, which starts in lower case and has no full stop.
    private static string AsReason(string sentence)
    {
        var reason = sentence.TrimEnd('.');
        return reason.Length == 0 ? reason : char.ToLowerInvariant(reason[0]) + reason[1..];
    }

    // A member of the root of a JSON text, or null when the text is no JSON object or has no such member.  The
    // element is cloned, since the document is disposed.
    private static JsonElement? MemberOf(string text, string key)
    {
        try
        {
            using var document = JsonDocument.Parse(text);
            return
                document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(key, out var member)
                ? member.Clone()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Outcome<T, ToolDiagnostic> Failed<T>(string path, string reason) =>
        Outcome<T, ToolDiagnostic>.Failed(BundleErrors.CannotBeRead(path, reason));

    // query.sql as the generator's reader takes a file: the text read from the bundle, at a path in the bundle.
    private sealed class QueryText(string path, string text) : AdditionalText
    {
        private readonly SourceText _text = SourceText.From(text);

        public override string Path => path;

        public override SourceText GetText(CancellationToken cancellationToken = default) => _text;
    }

    // The log a describer is given: the engine and a dot before each event's name, as a run's describer log puts
    // them.  A replay has no connection, so nothing is removed.
    private sealed class PrefixedLog(IRunLog inner, string engine) : IRunLog
    {
        public bool Writes => inner.Writes;

        public void Write(string name, params LogMember[] members) => inner.Write(engine + "." + name, members);
    }
}
