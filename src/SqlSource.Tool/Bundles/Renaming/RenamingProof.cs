using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// The proof of a renaming: the renamed bundle replayed as <c>replay</c> replays one must give its renamed entry, or
/// the raw failure's id and step, whole.
/// </summary>
/// <remarks>
/// A question the renamed capture does not answer is a request the pipeline made from the renamed query that the
/// renaming did not make alike, and is refused at that call.  Any other outcome is refused at the replay with its
/// first difference.
/// </remarks>
internal static class RenamingProof
{
    /// <summary>
    /// The path of a renamed bundle, which is in no file while it is proved.  What a step of a replay says of it names
    /// no path.
    /// </summary>
    public const string NoPath = "";

    /// <summary>The refusal of the renamed bundle, or null when it replays to its own answer.</summary>
    /// <param name="renamed">The renamed bundle.</param>
    /// <param name="describer">The describer of the bundle's engine, made with its planner settings.</param>
    /// <param name="token">Ends the replay.</param>
    public static async Task<RenamingRefusal?> ProveAsync(
        Bundle renamed,
        IQueryDescriber describer,
        CancellationToken token
    )
    {
        var capture = BundleReplay.CaptureOf(renamed, NoPath);
        if (!capture.Succeeded)
        {
            return Unreadable(BundleFiles.Capture, capture.Failure);
        }

        var planned = BundleReplay.Plan(renamed, BundleReplay.EngineOf(renamed), NoPath, token);
        if (!planned.Succeeded)
        {
            return Unreadable(BundleFiles.Query, planned.Failure);
        }

        var expected = BundleReplay.ExpectedOf(renamed, NoPath);
        if (!expected.Succeeded)
        {
            return Unreadable(BundleFiles.Expected, expected.Failure);
        }

        var (query, dialect) = planned.Value;
        var replayed = await BundleReplay.RunAsync(
            query,
            dialect,
            capture.Value,
            describer,
            NullRunLog.Instance,
            StillClock.Instance,
            token
        );
        return Compare(expected.Value, replayed);
    }

    /// <summary>A file of the renamed bundle that a step of the replay cannot read, with the step's error.</summary>
    public static RenamingRefusal Unreadable(string file, ToolDiagnostic error)
    {
        object[] arguments = [.. error.Arguments];
        var format = error.Descriptor.MessageFormat.ToString(CultureInfo.InvariantCulture);
        return new RenamingRefusal(
            RefusalStep.Replay,
            file,
            string.Format(CultureInfo.InvariantCulture, format, arguments)
        );
    }

    // A miss is a request the renaming did not make as the pipeline does.  An entry differs at its first member; a
    // failure, or an outcome of the other kind, by what each is.  A bundle with no answer to compare is proved by a
    // replay that asks nothing the capture lacks.
    private static RenamingRefusal? Compare(ReplayOutcome? expected, ReplayOutcome replayed)
    {
        if (replayed is ReplayOutcome.Missed missed)
        {
            return new RenamingRefusal(RefusalStep.RequestSql, $"{BundleFiles.Capture} call '{missed.Method}'", null);
        }

        if (expected is null || expected == replayed)
        {
            return null;
        }

        if (expected is ReplayOutcome.Described described && replayed is ReplayOutcome.Described other)
        {
            var differences = EntryDifferences.Of(described.Entry, other.Entry);
            return new RenamingRefusal(
                RefusalStep.Replay,
                BundleFiles.Expected,
                differences.Count > 0 ? differences[0] : null
            );
        }

        var file = expected is ReplayOutcome.Described ? BundleFiles.Expected : BundleFiles.Info;
        return new RenamingRefusal(RefusalStep.Replay, file, $"{TextOf(expected)} -> {TextOf(replayed)}");
    }

    private static string TextOf(ReplayOutcome outcome) =>
        outcome switch
        {
            ReplayOutcome.Described described => "the entry of " + described.Entry.Name,
            ReplayOutcome.Refused { Step: { } step } refused => $"{refused.Id} at {step}",
            ReplayOutcome.Refused refused => refused.Id,
            _ => throw new InvalidOperationException($"A replay gave the outcome {outcome}."),
        };

    // The clock of a replay that writes no event: nothing it times is seen, so it reads nothing of the host's.
    private sealed class StillClock : TimeProvider
    {
        public static StillClock Instance { get; } = new();

        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;

        public override long GetTimestamp() => 0;
    }
}
