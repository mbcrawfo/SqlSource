using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;
using SqlSource.Tool.Planning;
using SqlSource.Tool.Reporting;

namespace SqlSource.Tool.Describing;

/// <summary>
/// Step 3 of a run: for each database that has a query to describe, one session, and its queries one at a time.
/// A connection is not for several threads, and a describe takes milliseconds.
/// </summary>
/// <remarks>
/// <para>
/// It reports as it goes, so that a run on a slow database shows what it found.  When it returns, no query is
/// <see cref="QueryState.ToDescribe" />: each was described or failed.
/// </para>
/// <para>
/// This is the one place outside <c>Cli</c> and a pass of <c>WatchSession</c> that catches every exception: a
/// describer's, which is a bug of the describer and may hold the connection's value.  It is thrown again as a
/// <see cref="DescriberFaultException" /> without the value, and the run ends, or under <c>--watch</c> the pass.  A
/// description that holds the value, which would be written into a sidecar, is such a bug too
/// (<see cref="EntrySecretCheck" />), and so is a capture that holds it.  A cancellation is not caught: a run that
/// was cancelled ends as cancelled, not as <c>SQLSRC200</c>.
/// </para>
/// <para>
/// A run given a <see cref="RunCapture" />, as <c>diagnose</c>'s is, records its session and keeps what a query's
/// refusal was: the refusal is reported without being written, since the bundle holds it.  A run given none is
/// <c>describe</c>'s.
/// </para>
/// </remarks>
internal static class DatabaseRuns
{
    /// <summary>
    /// Runs each selected database.
    /// </summary>
    /// <param name="selected">The selected databases, in the plan's order.</param>
    /// <param name="files">The files, with the queries to describe.</param>
    /// <param name="connections">The connections of the selected databases.</param>
    /// <param name="host">The describers, the environment and the clock.</param>
    /// <param name="reporter">Where the errors go, with the run's trace.</param>
    /// <param name="cancellationToken">Ends the run.</param>
    /// <param name="online">What <c>--online</c> said: the help for a database that cannot be opened.</param>
    /// <param name="offline">
    /// Whether the run is offline (<see cref="DescribeRun" />): a database with no connection fails its queries and
    /// reports nothing, where the run would report <c>SQLSRC213</c>.
    /// </param>
    /// <param name="capture">
    /// Where the run records its one session, or null for a run that records nothing.  A second session for it is a
    /// fault of the tool.
    /// </param>
    public static async Task RunAsync(
        ImmutableArray<PlannedDatabase> selected,
        ImmutableArray<FileWork> files,
        Connections connections,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken,
        OnlineMode online = OnlineMode.Off,
        bool offline = false,
        RunCapture? capture = null
    )
    {
        foreach (var database in selected)
        {
            RunEvents.Database(
                reporter.Trace,
                database,
                connections.For(database.Name).Source,
                host.Describers.Find(database.Dialect) is not null
            );

            var queries = ToDescribe(files, database.Name);
            if (queries.Count == 0)
            {
                // Nothing of it is asked of a database, so it needs neither a describer nor a connection.
                continue;
            }

            cancellationToken.ThrowIfCancellationRequested();
            await RunDatabaseAsync(
                database,
                files,
                queries,
                connections,
                host,
                reporter,
                new DatabaseMode(online, offline, capture),
                cancellationToken
            );
        }
    }

    // The first of the four that holds is reported, and the queries fail: no describer, at each query; a variable
    // that two databases read; no connection, which an offline run does not report; a session that cannot be opened.
    // The last three once, at the first query.
    private static async Task RunDatabaseAsync(
        PlannedDatabase database,
        ImmutableArray<FileWork> files,
        List<QueryWork> queries,
        Connections connections,
        ToolHost host,
        Reporter reporter,
        DatabaseMode mode,
        CancellationToken cancellationToken
    )
    {
        if (host.Describers.Find(database.Dialect) is not { } describer)
        {
            foreach (var query in queries)
            {
                reporter.Report(DescribeErrors.NoDescriber(query.Planned, database.Dialect));
            }

            Fail(queries, ToolDiagnostics.NoDescriberForDialect.Id, reporter.Trace);
            return;
        }

        var first = queries[0].Planned;
        var connection = connections.For(database.Name);
        if (connection.Value is not { } value)
        {
            if (mode.Offline)
            {
                // The queries are held back as for SQLSRC213, and the run says so once, on its own line.
                Fail(queries, error: null, reporter.Trace);
                return;
            }

            var error = NoConnection(first, connection, connections, ProjectFolders(files, queries), host);
            reporter.Report(error);
            Fail(queries, error.Descriptor.Id, reporter.Trace);
            return;
        }

        // From here on the run holds the connection's value, and what a describer gives may hold it or a password
        // of it.  The describer says which parts are secret and how its driver writes the value again, under a guard
        // that knows only the whole value, and its list is copied there, since reading it is the describer's code
        // too; then everything said of this database goes through a trace that removes both.
        var given = new Guard(new ConnectionSecret(value), host.GetEnvironmentVariable, cancellationToken).Call(() =>
            new GivenSecrets(
                [
                    .. describer.SecretsOf(value, host.GetEnvironmentVariable)
                        ?? throw new InvalidOperationException(Guard.Nothing),
                ],
                describer.RewriteOf(value)
            )
        );
        var engine = SqlDialectName.Canonical(database.Dialect);
        var secret = new ConnectionSecret(value, given.Parts, [database.Name, engine], [given.Rewrite]);
        var trace = reporter.Trace.Without(secret);
        var guard = new Guard(secret, host.GetEnvironmentVariable, cancellationToken);
        var request = new OpenRequest(
            database.Name,
            value,
            ExchangeOf(database.Name, engine, host, mode.Capture),
            host.Time
        );
        var log = new SessionRunLog(reporter.Trace.Log, secret, engine + ".");
        var opened = await guard.CallAsync(() => describer.OpenAsync(request, log, cancellationToken));
        // The value and not Succeeded, here and where a query is described: a describer may have given null for
        // either, and an outcome that holds neither is a fault.
        if (opened.Value is not { } session)
        {
            var failure = ReportFailure(
                guard,
                () => opened.Failure,
                first,
                database,
                server: null,
                reporter,
                mode.Online
            );
            RunEvents.NotOpened(trace, database, failure.Descriptor.Id);
            Fail(queries, failure.Descriptor.Id, trace);
            return;
        }

        var run = new SessionRun(session, guard, host.Time, reporter, trace, mode.Capture);
        try
        {
            await DescribeAllAsync(database, queries, run, mode.Online, cancellationToken);
        }
        catch
        {
            // The first exception is the run's end: a session that fails to close after it adds nothing.
            await Guard.CloseQuietlyAsync(session);
            throw;
        }

        await guard.CallAsync(session.DisposeAsync);
        if (mode.Capture is { } capture)
        {
            capture.Text = guard.Call(() => NoSecretIn(capture.Capture, secret));
        }
    }

    // The host's exchange, or for a run that records, the host's behind one that adds each call to the capture.
    private static IDescribeExchange ExchangeOf(string database, string engine, ToolHost host, RunCapture? capture)
    {
        var exchange = host.Exchange(database);
        if (capture is null)
        {
            return exchange;
        }

        // A bug of the tool, outside the guard: the calls of two sessions, or of another engine, replay as neither.
        if (
            capture.Capture.Calls.Count != 0
            || !string.Equals(capture.Capture.Engine, engine, StringComparison.Ordinal)
        )
        {
            throw new InvalidOperationException(
                $"A capture of '{capture.Capture.Engine}' records one session, and the run opens '{database}' of "
                    + $"'{engine}' after {capture.Capture.Calls.Count} calls."
            );
        }

        return new CapturingExchange(exchange, capture.Capture);
    }

    private static async Task DescribeAllAsync(
        PlannedDatabase database,
        List<QueryWork> queries,
        SessionRun run,
        OnlineMode online,
        CancellationToken cancellationToken
    )
    {
        var guard = run.Guard;
        var secret = guard.Secret;
        var server = guard.Call(() => run.Session.Server);
        guard.Call(() => RunEvents.Opened(run.Trace, database, server));
        if (run.Capture is { } capture)
        {
            capture.Server = server;
        }

        foreach (var query in queries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var planned = query.Planned;
            var describe = new DescribeRequest(
                planned.Query.Name,
                SampleSql.Build(planned.Query, database.Dialect),
                planned.Query.Parameters
            );
            var started = run.Time.GetTimestamp();
            var result = await guard.CallAsync(() => run.Session.DescribeAsync(describe, cancellationToken));
            var duration = (long)run.Time.GetElapsedTime(started).TotalMilliseconds;
            if (result.Value is { } description)
            {
                query.Entry = guard.Call(() =>
                    NoSecretIn(
                        EntryBuilder.Build(planned, database.Dialect, database.Name, server, description),
                        secret
                    )
                );
                query.State = QueryState.Described;
                RunEvents.Query(run.Trace, query, duration);
            }
            else
            {
                var failure = ReportFailure(
                    guard,
                    () => result.Failure,
                    planned,
                    database,
                    server,
                    run.Reporter,
                    online,
                    write: run.Capture is null
                );
                query.Failure = failure;
                query.State = QueryState.Failed;
                RunEvents.Query(run.Trace, query, error: failure.Descriptor.Id);
            }
        }
    }

    // A failure a describer gave is read under the guard, where a null is the describer's fault, and redacted before
    // it is printed, or before it is reported without being written.  It is returned redacted, for the events the
    // caller writes.
    private static DescribeFailure ReportFailure(
        Guard guard,
        Func<DescribeFailure?> failureOf,
        PlannedQuery planned,
        PlannedDatabase database,
        ServerInfo? server,
        Reporter reporter,
        OnlineMode online,
        bool write = true
    )
    {
        var secret = guard.Secret;
        var failure = Redaction.Of(
            guard.Call(() => failureOf() ?? throw new InvalidOperationException(Guard.Nothing)),
            secret
        );
        var error = Redaction.Of(DescribeErrors.Failure(failure, planned, database, server, online), secret);
        if (write)
        {
            reporter.Report(error);
        }
        else
        {
            reporter.ReportWithoutWriting(error);
        }

        return failure;
    }

    // What a describer gave goes into a sidecar, which is committed: a value in it is a bug of the describer.
    private static SidecarEntry NoSecretIn(SidecarEntry entry, ConnectionSecret secret) =>
        EntrySecretCheck.Holds(entry, secret)
            ? throw new InvalidOperationException(
                $"The describer put the connection's value in its description of the query '{entry.Name}'."
            )
            : entry;

    // A capture goes into a bundle that is sent, and the writer refuses one that holds a whole form or a part.
    private static string NoSecretIn(DescribeCapture capture, ConnectionSecret secret)
    {
        var written = CaptureJson.Write(capture, secret);
        return written.Succeeded ? written.Value : throw new InvalidOperationException(written.Failure);
    }

    private static ToolDiagnostic NoConnection(
        PlannedQuery first,
        DatabaseConnection connection,
        Connections all,
        IEnumerable<string> projectFolders,
        ToolHost host
    )
    {
        if (connection.SharesVariableWith is { } other)
        {
            return DescribeErrors.SharedVariable(first, connection, other);
        }

        return all.UnnamedIsSetAndNotUsed
            ? DescribeErrors.UnnamedNotUsed(first, all)
            : DescribeErrors.NoConnection(first, connection, all, projectFolders, host.ReadTextFile);
    }

    // The folders of the projects that the queries are planned under, in the plan's order.
    private static IEnumerable<string> ProjectFolders(ImmutableArray<FileWork> files, List<QueryWork> queries) =>
        files
            .Where(file => file.Queries.Any(queries.Contains))
            .Select(static file => Path.GetDirectoryName(file.File.ProjectPath)!)
            .Distinct(StringComparer.Ordinal);

    // The queries of a database that the run decided to describe, in the plan's order.
    private static List<QueryWork> ToDescribe(ImmutableArray<FileWork> files, string database) =>
        [
            .. files
                .SelectMany(static file => file.Queries)
                .Where(query =>
                    query.State == QueryState.ToDescribe
                    && DatabaseName.Comparer.Equals(query.Planned.Database, database)
                ),
        ];

    // The queries fail with one error, which was reported, or with none when the run is offline: each has its event.
    private static void Fail(List<QueryWork> queries, string? error, RunTrace trace)
    {
        foreach (var query in queries)
        {
            query.State = QueryState.Failed;
            RunEvents.Query(trace, query, error: error);
        }
    }

    // What belongs to one open session: everything a query of its database is described with.  It holds the session
    // and does not own it: RunDatabaseAsync closes it.  The trace is the one without the connection's value.
    private sealed record SessionRun(
        IDescribeSession Session,
        Guard Guard,
        TimeProvider Time,
        Reporter Reporter,
        RunTrace Trace,
        RunCapture? Capture
    );

    // What the run's options make of each database: the help of one that cannot be opened, whether one with no
    // connection is held back silently, and where its session is recorded.
    private sealed record DatabaseMode(OnlineMode Online, bool Offline, RunCapture? Capture);

    // What a describer says of a connection's value before its session is opened.  The rewrite is none for a value
    // that its driver cannot read.  It is a class and not a record, since a record prints its members.
    private sealed class GivenSecrets(IReadOnlyList<string> parts, string? rewrite)
    {
        public IReadOnlyList<string> Parts => parts;

        public string? Rewrite => rewrite;
    }

    // Runs what a describer gives or does.  Whatever it throws is a bug of the describer and is thrown again as
    // the SQLSRC200 to report, without the connection's value and the parts it knows.
    private sealed class Guard(
        ConnectionSecret secret,
        Func<string, string?> getEnvironmentVariable,
        CancellationToken cancellationToken
    )
    {
        public const string Nothing = "A describer returned nothing.";

        // What a call that gives nothing gives Call<T>, so that both are guarded in one place.
        private static readonly object Done = new();

        public ConnectionSecret Secret => secret;

        public async Task<T> CallAsync<T>(Func<Task<T>> call)
            where T : class
        {
            try
            {
                return await call() ?? throw new InvalidOperationException(Nothing);
            }
            catch (Exception exception) when (!IsCancellation(exception))
            {
                throw Fault(exception);
            }
        }

        public async Task CallAsync(Func<ValueTask> call)
        {
            try
            {
                await call();
            }
            catch (Exception exception) when (!IsCancellation(exception))
            {
                throw Fault(exception);
            }
        }

        // Closes a session after the run has failed or was cancelled.  What closing throws is dropped.
        public static async Task CloseQuietlyAsync(IDescribeSession session)
        {
            try
            {
                await session.DisposeAsync();
            }
            catch (Exception exception) when (exception is not OutOfMemoryException)
            {
                // Nothing of it may reach the output: the first exception is the one that is reported.
            }
        }

        public T Call<T>(Func<T> call)
            where T : class
        {
            try
            {
                return call() ?? throw new InvalidOperationException(Nothing);
            }
            catch (Exception exception) when (!IsCancellation(exception))
            {
                throw Fault(exception);
            }
        }

        public void Call(Action call) =>
            Call(() =>
            {
                call();
                return Done;
            });

        private bool IsCancellation(Exception exception) =>
            exception is OperationCanceledException && cancellationToken.IsCancellationRequested;

        // The value is taken out of the whole text of the exception before the text is cut into lines, as a value
        // may span lines, and out of the error again, as the second line of defence.
        private DescriberFaultException Fault(Exception exception) =>
            new(
                Redaction.Of(
                    UnexpectedFailure.Of(exception, getEnvironmentVariable, text => Redaction.Of(text, secret)),
                    secret
                )
            );
    }
}
