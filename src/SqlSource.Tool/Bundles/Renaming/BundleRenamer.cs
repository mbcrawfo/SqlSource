using System;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Bundles.Renaming;

/// <summary>
/// Renames a raw bundle so that it may be posted: every name of the user's replaced by a keyed hash, every literal by
/// one, every comment emptied, and the result proved by a replay.
/// </summary>
/// <remarks>
/// <para>
/// The steps run in this order, and the first refusal ends the run after its step: drop what the pipeline does not
/// read; read the copies of reused parameters that the capture records; register the names of the catalog, the
/// engine's own and the query's parameters; rename <c>query.sql</c>; rename every string of each call by its member's
/// rule, then let the engine's part derive what its pipeline makes; rename the entry; check every token; and replay
/// the renamed bundle, which must give its renamed answer.
/// </para>
/// <para>
/// Every step that knows an engine asks the engine's part, and the renaming reads no file: the caller reads the raw
/// bundle, makes the describer with its planner settings, and writes what this gives.
/// </para>
/// </remarks>
internal static class BundleRenamer
{
    /// <summary>The renamed bundle and its mapping, or the refusal that says why it cannot be renamed safely.</summary>
    /// <param name="raw">
    /// The raw bundle, with a <c>bundle.json</c> that says it is not renamed, and whose capture, query and expected
    /// answer <c>BundleReplay</c> reads.
    /// </param>
    /// <param name="engine">The engine's part of the renaming, of the bundle's engine.</param>
    /// <param name="describer">The bundle's describer, as <c>BundleDescribers.For</c> makes it for a replay.</param>
    /// <param name="key">The run's key.</param>
    /// <param name="token">Ends the renaming.</param>
    /// <exception cref="ArgumentException">
    /// The bundle has no <c>bundle.json</c>, is renamed already, or is of another engine than the part's, or its
    /// capture or its <c>expected.json</c> cannot be read.
    /// </exception>
    public static async Task<Outcome<RenamedBundle, RenamingRefusal>> RenameAsync(
        Bundle raw,
        IEngineRenaming engine,
        IQueryDescriber describer,
        RenamingKey key,
        CancellationToken token
    )
    {
        if (raw.Info is not { Renamed: false } info)
        {
            throw new ArgumentException("Only a raw bundle, whose bundle.json says so, is renamed.", nameof(raw));
        }

        var read = CaptureJson.Read(raw.Capture);
        if (read.Value is not { } capture)
        {
            throw new ArgumentException($"The capture of the raw bundle cannot be read: {read.Failure}", nameof(raw));
        }

        if (!string.Equals(capture.Engine, SqlDialectName.Canonical(engine.Dialect), StringComparison.Ordinal))
        {
            throw new ArgumentException($"The raw bundle is of the engine '{capture.Engine}'.", nameof(raw));
        }

        var mapping = new NameMapping(key, engine.Fold);
        var calls = CaptureRenamer.CallsOf(capture);

        CaptureRenamer.Drop(engine, calls);
        var copies = RenamingCopies.Of(engine, calls, static call => call.Original);
        var run = RenamingRun.For(engine, mapping, raw.Query, copies);
        CaptureRenamer.Register(engine, calls, run);
        run.RegisterParameters();
        if (run.Refusal is { } registered)
        {
            return Refused(registered);
        }

        var query = run.RenameQueryFile(raw.Query);
        if (run.Refusal is { } ofQuery)
        {
            return Refused(ofQuery);
        }

        CaptureRenamer.Rename(engine, calls, run);
        if (run.Refusal is { } ofCapture)
        {
            return Refused(ofCapture);
        }

        var expected = raw.Expected is { } text ? EntryRenamer.Rename(engine, run, text, query, token) : null;
        if (run.Refusal is { } ofEntry)
        {
            return Refused(ofEntry);
        }

        if (TokenCheck.Check(engine, mapping, calls, query, expected) is { } ofToken)
        {
            return Refused(ofToken);
        }

        var renamed = new Bundle(
            info with
            {
                Renamed = true,
            },
            query,
            CaptureJson.Write(CaptureRenamer.CaptureOf(capture.Engine, calls)),
            expected,
            raw.ExplainSettings
        );
        if (await RenamingProof.ProveAsync(renamed, describer, token) is { } ofReplay)
        {
            return Refused(ofReplay);
        }

        return Outcome<RenamedBundle, RenamingRefusal>.Of(new RenamedBundle(renamed, [.. mapping.Names]));
    }

    private static Outcome<RenamedBundle, RenamingRefusal> Refused(RenamingRefusal refusal) =>
        Outcome<RenamedBundle, RenamingRefusal>.Failed(refusal);
}
