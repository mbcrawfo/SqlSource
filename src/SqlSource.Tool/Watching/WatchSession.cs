using System;
using System.Buffers;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.CommandLine;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Tool.Describing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Projects;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Watching;

/// <summary>
/// A <c>describe --watch</c>: a pass, then another after each change to a file of the run, until it is stopped.
/// </summary>
/// <remarks>
/// <para>
/// A pass is a whole <c>describe</c> with a reporter of its own on the session's trace, so that an error of one pass
/// neither stops the next from deleting a sidecar nor ends the session.  It plans again from the manifests the session
/// keeps, and runs the manifest targets again only when a change asks for it, when the last evaluation reported an
/// error, or when a listed path that was deleted is still gone.
/// </para>
/// <para>
/// What a change asks is decided when it comes, by the <see cref="WatchSet" /> of the last evaluation, and a change
/// that asks nothing is dropped.  One that comes while the projects are evaluated is kept as it came, and read by the
/// sets before and after the evaluation once it ends.  A pass starts once <see cref="Debounce" /> has passed since the
/// last change it covers, by <see cref="ToolHost.Time" />, and a change that comes during a pass makes one more pass
/// after it.
/// </para>
/// <para>
/// The folders of the first set are watched before the first pass, and those of each evaluation's set as soon as it
/// ends, before the pass reads a file, so that no change of a pass is missed.  A folder of both sets keeps its watch.
/// </para>
/// </remarks>
internal sealed class WatchSession
{
    /// <summary>What the session writes to standard output before every pass after the first.</summary>
    internal const string ChangeDetectedMarker = "sqlsource: change detected; describing";

    /// <summary>What the session writes to standard output after every pass.</summary>
    internal const string WatchingMarker = "sqlsource: watching for changes; press Ctrl+C to stop";

    /// <summary>How long a pass waits after the last change it covers: an editor's save is several events.</summary>
    internal static readonly TimeSpan Debounce = TimeSpan.FromMilliseconds(300);

    /// <summary>The environment variable that makes the session poll its folders instead of watching them.</summary>
    internal const string PollingVariable = "DOTNET_USE_POLLING_FILE_WATCHER";

    // The digits of the Guid that ends the name of an evaluation's folder, as Guid.ToString("N") writes them.
    private const int ManifestFolderIdLength = 32;

    private static readonly SearchValues<char> HexDigits = SearchValues.Create("0123456789abcdef");

    private static readonly ContinuationLine PollInstead = new(
        "help",
        $"set {PollingVariable}=true to poll for changes instead"
    );

    private readonly RunUnit _unit;
    private readonly string _unitFolder;
    private readonly Usage _usage;
    private readonly ToolHost _host;
    private readonly Reporter _session;
    private readonly Action<LogValue> _counted;

    // The changes that came since the last pass began, and what they ask, those that came during the evaluation under
    // way, null when there is none, which are read when it ends, and the failures that the loop has not reported yet.
    // Guarded by _gate, as _set is.
    private readonly object _gate = new();
    private readonly HashSet<string> _gone = [with(RunProjects.PathComparer)];
    private readonly List<WatchFailure> _failures = [];
    private int _changes;
    private WatchEffect _effect;
    private long _lastChange;
    private TaskCompletionSource _wake = NewWake();
    private WatchSet _set;
    private List<(FileChange Change, long At)>? _duringEvaluation;

    // Read and written by the loop alone: the watch of each folder, by its path, with a null watch for one that could
    // not start, which is not tried again while the folder stays in the set; and the failures reported, by folder and
    // message.
    private readonly Dictionary<string, (bool Recursive, IDisposable? Watch)> _watches =
    [
        with(RunProjects.PathComparer),
    ];
    private readonly HashSet<(string Folder, string Message)> _reported = [];
    private ImmutableArray<ProjectManifest> _manifests = [];
    private bool _evaluationFailed = true;
    private int _pass;

    private WatchSession(RunUnit unit, Usage usage, ToolHost host, Reporter session, Action<LogValue> counted)
    {
        _unit = unit;
        _unitFolder = Path.GetDirectoryName(unit.Path) ?? throw new InvalidOperationException("A unit is a file.");
        _usage = usage;
        _host = host;
        _session = session;
        _counted = counted;
        _set = WatchSet.Of(unit.Path, _unitFolder, FoundProjects.None);
    }

    /// <summary>
    /// Runs the session on the unit: the first pass, then a pass after each change, until the token is cancelled.
    /// </summary>
    /// <param name="unit">The unit, which was found.</param>
    /// <param name="usage">The command line as <see cref="UsageCheck" /> read it, which narrows every pass.</param>
    /// <param name="parsed">
    /// The command line as System.CommandLine read it: <c>--force</c> forces the first pass.
    /// </param>
    /// <param name="host">What the session takes from outside, its watches and its clock included.</param>
    /// <param name="session">The run's reporter, whose trace every pass shares.</param>
    /// <param name="counted">Given the counts of each selected database after each pass.</param>
    /// <param name="cancellationToken">Stops the pass under way and ends the session.</param>
    /// <returns>Never: it ends by an <see cref="OperationCanceledException" /> of the token.</returns>
    internal static async Task RunAsync(
        RunUnit unit,
        Usage usage,
        ParseResult parsed,
        ToolHost host,
        Reporter session,
        Action<LogValue> counted,
        CancellationToken cancellationToken
    )
    {
        var watch = new WatchSession(unit, usage, host, session, counted);
        try
        {
            await watch.LoopAsync(DescribeCommand.ModeOf(parsed), cancellationToken);
        }
        finally
        {
            watch.CloseWatches();
        }
    }

    private async Task LoopAsync(RunMode firstMode, CancellationToken cancellationToken)
    {
        // Before the first pass reads a file, so that a change made during it makes one more pass.
        Watch(_set);
        await PassAsync(0, evaluate: true, firstMode, cancellationToken);
        ReportFailures();
        while (!cancellationToken.IsCancellationRequested)
        {
            // The wait begins before the marker, so that whoever reads the marker sees the wait for a change that came
            // during the pass.
            var next = NextChangesAsync(cancellationToken);
            await _host.Out.WriteLineAsync(WatchingMarker);
            var changes = await next;

            ReportFailures();
            await _host.Out.WriteLineAsync(ChangeDetectedMarker);
            var evaluate =
                changes.Effect == WatchEffect.Evaluate
                || _evaluationFailed
                || changes.Gone.Any(static path => !File.Exists(path));
            await PassAsync(changes.Count, evaluate, RunMode.Describe, cancellationToken);
            ReportFailures();
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    // One pass, with a reporter of its own.  An exception is the pass's error, as Cli.RunCommandAsync makes it of a
    // run's, and the session goes on; a cancellation ends the session.
    private async Task PassAsync(int changes, bool evaluate, RunMode mode, CancellationToken cancellationToken)
    {
        var pass = ++_pass;
        var reporter = new Reporter(_host.Error, _session.Trace);
        var started = _host.Time.GetTimestamp();
        RunEvents.PassStarted(_session.Trace, pass, changes, evaluate);
        try
        {
            if (evaluate)
            {
                await EvaluateAsync(reporter, cancellationToken);
            }

            if (
                await DescribeCommand.PlanFromAsync(_unit, _manifests, _usage, _host, reporter, cancellationToken) is
                { } plan
            )
            {
                await DescribeCommand.DescribePlanAsync(
                    plan,
                    _usage,
                    _unit,
                    mode,
                    _host,
                    reporter,
                    _counted,
                    cancellationToken
                );
            }
        }
        catch (DescriberFaultException fault)
        {
            // The error of an exception that a describer threw, already without the connection's value.
            reporter.Report(fault.Diagnostic);
        }
        catch (Exception exception)
            when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            reporter.Report(UnexpectedFailure.Of(exception, _host.GetEnvironmentVariable));
        }

        var durationMs = (long)_host.Time.GetElapsedTime(started).TotalMilliseconds;
        RunEvents.PassEnded(_session.Trace, pass, reporter.Count, durationMs);
    }

    // Runs the manifest targets again, and keeps what they gave: the manifests, the set that the changes are read by,
    // and whether the evaluation reported an error, which makes the next pass evaluate again.  Then watches the folders
    // of the new set, before the pass reads a file.
    private async Task EvaluateAsync(Reporter reporter, CancellationToken cancellationToken)
    {
        _evaluationFailed = true;
        var errors = reporter.Count;
        lock (_gate)
        {
            _duringEvaluation = [];
        }

        WatchSet? set = null;
        try
        {
            var found = await DescribeCommand.FindManifestsAsync(_unit, _usage, _host, reporter, cancellationToken);
            _manifests = found.Manifests;
            _evaluationFailed = reporter.Count > errors;
            set = WatchSet.Of(_unit.Path, _unitFolder, found);
        }
        finally
        {
            // A change during the evaluation may be of what it read or of what it gives, so it asks as much as the
            // larger of the two sets says.  An evaluation that threw leaves the set as it was.
            lock (_gate)
            {
                var before = _set;
                _set = set ?? before;
                foreach (var (change, at) in _duringEvaluation)
                {
                    KeepIfItAsks(change, at, before);
                }

                _duringEvaluation = null;
            }
        }

        Watch(set);
    }

    // Waits for the changes of the next pass: until a change comes, then until Debounce has passed since the last
    // one.  Everything up to its first wait runs before it returns, so a change that is pending has its timer then.
    private async Task<Changes> NextChangesAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            Task? wake = null;
            long last;
            lock (_gate)
            {
                if (_changes == 0)
                {
                    _wake = NewWake();
                    wake = _wake.Task;
                }

                last = _lastChange;
            }

            if (wake is not null)
            {
                await wake.WaitAsync(cancellationToken);
                continue;
            }

            var remaining = Debounce - _host.Time.GetElapsedTime(last);
            if (remaining > TimeSpan.Zero)
            {
                // The rest runs where the timer fired, which under a test is the thread that moves the clock: the wait
                // that follows is made at the time the clock stands at, before it moves on.
                await Task.Delay(remaining, _host.Time, cancellationToken).ConfigureAwait(false);
                continue;
            }

            lock (_gate)
            {
                // A change that came since the look at the clock moves the pass on again.
                if (_lastChange != last)
                {
                    continue;
                }

                var changes = new Changes(_changes, _effect, [.. _gone]);
                _changes = 0;
                _effect = WatchEffect.None;
                _gone.Clear();
                return changes;
            }
        }
    }

    // What a watch tells of a change: kept, with what it asks, when it asks something.  One in a folder where an
    // evaluation writes its manifests is the tool's own, and dropped.  One that comes during an evaluation is
    // kept as it came until the evaluation ends.
    private void Changed(FileChange change)
    {
        if (IsManifestFolder(change.Path) && (change.OldPath is null || IsManifestFolder(change.OldPath)))
        {
            return;
        }

        lock (_gate)
        {
            var at = _host.Time.GetTimestamp();
            if (_duringEvaluation is { } held)
            {
                held.Add((change, at));
                return;
            }

            KeepIfItAsks(change, at, _set);
        }
    }

    // Keeps the change, which came at the time given, with what it asks by the current set and by the one given, when
    // it asks something.  Under _gate.
    private void KeepIfItAsks(FileChange change, long at, WatchSet other)
    {
        var effect = _set.Classify(change);
        var gone = _set.Gone(change);
        if (!ReferenceEquals(other, _set))
        {
            var effectBefore = other.Classify(change);
            effect = effectBefore > effect ? effectBefore : effect;
            gone = [.. gone.Union(other.Gone(change), RunProjects.PathComparer)];
        }

        if (effect == WatchEffect.None && gone.IsEmpty)
        {
            return;
        }

        _gone.UnionWith(gone);
        Keep(effect, at);
    }

    // True for a folder that an evaluation makes in ToolHost.TempDirectory, <prefix><32 digits of a Guid>, and for a
    // path below it.  Not for anything else of that folder, which may hold a user's project, as /tmp does.
    private bool IsManifestFolder(string path)
    {
        var temporary = Path.TrimEndingDirectorySeparator(_host.TempDirectory) + Path.DirectorySeparatorChar;
        if (!path.StartsWith(temporary, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var name = path.AsSpan(temporary.Length);
        var end = name.IndexOfAny(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        name = end < 0 ? name : name[..end];
        return name.Length == ProjectEvaluator.ManifestFolderPrefix.Length + ManifestFolderIdLength
            && name.StartsWith(ProjectEvaluator.ManifestFolderPrefix, StringComparison.Ordinal)
            && name[ProjectEvaluator.ManifestFolderPrefix.Length..].IndexOfAnyExcept(HexDigits) < 0;
    }

    // What a watch tells of its failure, a lost batch of events included, and what a watch that cannot start is: the
    // folder may have changed unseen, so the next pass evaluates.  A lost batch of events is not reported.  Any other
    // failure is kept for the loop to report, so that it is not written in the middle of a pass's output.
    private void Failed(WatchFailure failure)
    {
        lock (_gate)
        {
            Keep(WatchEffect.Evaluate, _host.Time.GetTimestamp());
            if (!failure.Overflow)
            {
                _failures.Add(failure);
            }
        }
    }

    // Reports the failures kept since the last call, after the pass during which they came, or before the next pass
    // when none ran: each once for each folder and message of the session, since a limit of the system raises the same
    // error again and again.
    private void ReportFailures()
    {
        WatchFailure[] failures;
        lock (_gate)
        {
            failures = [.. _failures];
            _failures.Clear();
        }

        // A watch that polls already does what the help line says.
        ContinuationLine[] help = PollsForChanges() ? [] : [PollInstead];

        // Where adds each to what was reported as it goes, so a failure twice in one batch is reported once.
        foreach (var failure in failures.Where(failure => _reported.Add((failure.Folder, failure.Message))))
        {
            _session.Report(
                ToolDiagnostic.ForFile(ToolDiagnostics.WatchFailed, failure.Folder, failure.Message).WithLines(help)
            );
        }
    }

    // Under _gate.  A change held through an evaluation keeps the time it came, which may be before the last one kept.
    private void Keep(WatchEffect effect, long at)
    {
        _changes++;
        _effect = effect > _effect ? effect : _effect;
        _lastChange = Math.Max(_lastChange, at);
        _ = _wake.TrySetResult();
    }

    // Watches each folder of the set that has no watch yet, then stops the watch of each folder that the set no longer
    // holds, so that a folder of both is watched throughout.  A folder that does not exist is skipped, and tried again
    // by the next set; one whose watch could not start is not, while it stays in the set.  Nothing for no set.
    private void Watch(WatchSet? set)
    {
        if (set is null)
        {
            return;
        }

        var poll = PollsForChanges();
        var opened = new List<(string Folder, bool Recursive, IDisposable? Watch)>();
        foreach (var (folder, recursive) in set.Folders)
        {
            if (
                (_watches.TryGetValue(folder, out var open) && open.Recursive == recursive) || !Directory.Exists(folder)
            )
            {
                continue;
            }

            IDisposable? watch = null;
            try
            {
                watch = _host.Watch(new WatchRequest(folder, recursive, Poll: poll, Changed, Failed));
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                Failed(new WatchFailure(folder, Overflow: false, exception.Message));
            }

            opened.Add((folder, recursive, watch));
        }

        var held = set.Folders.Select(static entry => entry.Folder).ToHashSet(RunProjects.PathComparer);
        foreach (var (folder, open) in _watches.ToList())
        {
            if (!held.Contains(folder) || opened.Exists(entry => RunProjects.PathComparer.Equals(entry.Folder, folder)))
            {
                open.Watch?.Dispose();
                _ = _watches.Remove(folder);
            }
        }

        foreach (var (folder, recursive, watch) in opened)
        {
            _watches[folder] = (recursive, watch);
        }
    }

    // True or 1, whatever the case, as the variable of .NET's own watchers is read.
    private bool PollsForChanges() =>
        _host.GetEnvironmentVariable(PollingVariable) is { } value
        && (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value == "1");

    private void CloseWatches()
    {
        foreach (var (_, watch) in _watches.Values)
        {
            watch?.Dispose();
        }

        _watches.Clear();
    }

    private static TaskCompletionSource NewWake() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    // The changes that one pass covers.
    private sealed record Changes(int Count, WatchEffect Effect, ImmutableArray<string> Gone);
}
