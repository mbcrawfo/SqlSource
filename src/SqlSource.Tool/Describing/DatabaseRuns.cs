using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
/// without the value, and the run ends.  A run that was cancelled ends as one.
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

            Fail(queries);
            return;
        }

        var first = queries[0].Planned;
        var connection = connections.For(database.Name);
        if (connection.Value is not { } value)
        {
            reporter.Report(NoConnection(first, connection, connections));
            Fail(queries);
            return;
        }

        var guard = new Guard(value, host.GetEnvironmentVariable, cancellationToken);
        var request = new OpenRequest(database.Name, value, host.Exchange(database.Name));
        var opened = await guard.CallAsync(() => describer.OpenAsync(request, cancellationToken));
        if (opened.Session is not { } session)
        {
            var failure = guard.Call(() => opened.Failure ?? throw new InvalidOperationException(Guard.Nothing));
            reporter.Report(Redaction.Of(DescribeErrors.Failure(failure, first, database, server: null), value));
            Fail(queries);
            return;
        }

        try
        {
            await DescribeAllAsync(database, queries, session, guard, reporter, cancellationToken);
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
        IDescribeSession session,
        Guard guard,
        Reporter reporter,
        CancellationToken cancellationToken
    )
    {
        var value = guard.Secret;
        var server = guard.Call(() => session.Server);
        foreach (var query in queries)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var planned = query.Planned;
            var describe = new DescribeRequest(
                planned.Query.Name,
                SampleSql.Build(planned.Query),
                planned.Query.Parameters
            );
            var result = await guard.CallAsync(() => session.DescribeAsync(describe, cancellationToken));
            if (result.Description is { } description)
            {
                query.Entry = guard.Call(() =>
                    EntryBuilder.Build(planned, database.Dialect, database.Name, server, description)
                );
                query.State = QueryState.Described;
            }
            else
            {
                var failure = guard.Call(() => result.Failure ?? throw new InvalidOperationException(Guard.Nothing));
                reporter.Report(Redaction.Of(DescribeErrors.Failure(failure, planned, database, server), value));
                query.State = QueryState.Failed;
            }
        }
    }

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
                    && string.Equals(query.Planned.Database, database, StringComparison.OrdinalIgnoreCase)
                ),
        ];

    private static void Fail(List<QueryWork> queries)
    {
        foreach (var query in queries)
        {
            query.State = QueryState.Failed;
        }
    }

    // Runs what a describer gives or does.  Whatever it throws is a bug of the describer and is thrown again as
    // the SQLSRC200 to report, without the connection's value.
    private sealed class Guard(
        string secret,
        Func<string, string?> getEnvironmentVariable,
        CancellationToken cancellationToken
    )
    {
        public const string Nothing = "A describer returned nothing.";

        public string Secret => secret;

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
