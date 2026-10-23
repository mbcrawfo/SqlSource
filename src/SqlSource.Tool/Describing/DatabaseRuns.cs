using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Diagnostics;
using SqlSource.Parsing;
using SqlSource.Snapshot;
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
/// This is the one place outside <c>Cli</c> that catches every exception: a describer's, which is a bug of the
/// describer and may hold the connection's value.  It is thrown again as a <see cref="DescriberFaultException" />
/// without the value, and the run ends.  A description that holds the value, which would be written into a sidecar,
/// is such a bug too (<see cref="EntrySecretCheck" />).  A run that was cancelled ends as one.
/// </para>
/// </remarks>
internal static class DatabaseRuns
{
    public static async Task RunAsync(
        ImmutableArray<PlannedDatabase> selected,
        ImmutableArray<FileWork> files,
        Connections connections,
        ToolHost host,
        Reporter reporter,
        CancellationToken cancellationToken
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
            await RunDatabaseAsync(database, queries, connections, host, reporter, cancellationToken);
        }
    }

    // The first of the four that holds is reported, and the queries fail: no describer, at each query; a variable
    // that two databases read; no connection; a session that cannot be opened.  The last three once, at the first
    // query.
    private static async Task RunDatabaseAsync(
        PlannedDatabase database,
        List<QueryWork> queries,
        Connections connections,
        ToolHost host,
        Reporter reporter,
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
            var error = NoConnection(first, connection, connections);
            reporter.Report(error);
            Fail(queries, error.Descriptor.Id, reporter.Trace);
            return;
        }

        // From here on the run holds the connection's value, and what a describer gives may hold it or a password
        // of it.  The describer says which parts are secret, under a guard that knows only the whole value, and its
        // list is copied there, since reading it is the describer's code too; then everything said of this database
        // goes through a trace that removes both.
        var parts = new Guard(new ConnectionSecret(value), host.GetEnvironmentVariable, cancellationToken).Call(() =>
            (IReadOnlyList<string>)[.. describer.SecretsOf(value) ?? throw new InvalidOperationException(Guard.Nothing)]
        );
        var engine = SqlDialectName.Canonical(database.Dialect);
        var secret = new ConnectionSecret(value, parts, [database.Name, engine]);
        var trace = reporter.Trace.Without(secret);
        var guard = new Guard(secret, host.GetEnvironmentVariable, cancellationToken);
        var request = new OpenRequest(database.Name, value, host.Exchange(database.Name), host.Time);
        var log = new SessionRunLog(reporter.Trace.Log, secret, engine + ".");
        var opened = await guard.CallAsync(() => describer.OpenAsync(request, log, cancellationToken));
        // The value and not Succeeded, here and where a query is described: a describer may have given null for
        // either, and an outcome that holds neither is a fault.
        if (opened.Value is not { } session)
        {
            var failure = ReportFailure(guard, () => opened.Failure, first, database, server: null, reporter);
            RunEvents.NotOpened(trace, database, failure.Descriptor.Id);
            Fail(queries, failure.Descriptor.Id, trace);
            return;
        }

        var run = new SessionRun(session, guard, host.Time, reporter, trace);
        try
        {
            await DescribeAllAsync(database, queries, run, cancellationToken);
        }
        catch
        {
            // The first exception is the run's end: a session that fails to close after it adds nothing.
            await Guard.CloseQuietlyAsync(session);
            throw;
        }

        await guard.CallAsync(session.DisposeAsync);
    }

    private static async Task DescribeAllAsync(
        PlannedDatabase database,
        List<QueryWork> queries,
        SessionRun run,
        CancellationToken cancellationToken
    )
    {
        var guard = run.Guard;
        var secret = guard.Secret;
        var server = guard.Call(() => run.Session.Server);
        guard.Call(() => RunEvents.Opened(run.Trace, database, server));
        foreach (var query in queries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var planned = query.Planned;
            var describe = new DescribeRequest(
                planned.Query.Name,
                SampleSql.Build(planned.Query),
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
                var failure = ReportFailure(guard, () => result.Failure, planned, database, server, run.Reporter);
                query.State = QueryState.Failed;
                RunEvents.Query(run.Trace, query, error: failure.Descriptor.Id);
            }
        }
    }

    // A failure a describer gave is read under the guard, where a null is the describer's fault, and redacted before
    // it is printed.  It is returned redacted, for the events the caller writes.
    private static DescribeFailure ReportFailure(
        Guard guard,
        Func<DescribeFailure?> failureOf,
        PlannedQuery planned,
        PlannedDatabase database,
        ServerInfo? server,
        Reporter reporter
    )
    {
        var secret = guard.Secret;
        var failure = Redaction.Of(
            guard.Call(() => failureOf() ?? throw new InvalidOperationException(Guard.Nothing)),
            secret
        );
        reporter.Report(Redaction.Of(DescribeErrors.Failure(failure, planned, database, server), secret));
        return failure;
    }

    // What a describer gave goes into a sidecar, which is committed: a value in it is a bug of the describer.
    private static SidecarEntry NoSecretIn(SidecarEntry entry, ConnectionSecret secret) =>
        EntrySecretCheck.Holds(entry, secret)
            ? throw new InvalidOperationException(
                $"The describer put the connection's value in its description of the query '{entry.Name}'."
            )
            : entry;

    private static ToolDiagnostic NoConnection(PlannedQuery first, DatabaseConnection connection, Connections all)
    {
        if (connection.SharesVariableWith is { } other)
        {
            return DescribeErrors.SharedVariable(first, connection, other);
        }

        return all.UnnamedIsSetAndNotUsed
            ? DescribeErrors.UnnamedNotUsed(first, all)
            : DescribeErrors.NoConnection(first, connection, all);
    }

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

    // The queries fail with one error, which was reported: each has its event.
    private static void Fail(List<QueryWork> queries, string error, RunTrace trace)
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
        RunTrace Trace
    );

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
