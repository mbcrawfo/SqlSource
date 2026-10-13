using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// A session with one PostgreSQL database.  Every call it makes to the server goes through the exchange, and the
/// connection is made inside a <c>live</c> function and nowhere else.
/// </summary>
/// <remarks>
/// A query is described through one prepared statement: <c>PREPARE</c> takes a declared type as written, resolves
/// it, checks it against every use of its parameter, and infers the rest.  A prepared statement is session state
/// and survives a rollback, so it is freed in a <c>finally</c>, outside any transaction; one left behind would be
/// a leak until the session ends.  Its name is a counter, since the name is part of a request and a capture must be
/// the same each time it is recorded.
/// </remarks>
internal sealed class PostgresSession : IDescribeSession
{
    public const string DriverName = "Npgsql";

    private readonly IDescribeExchange _exchange;
    private readonly IRunLog _log;
    private readonly PostgresLive _live;
    private readonly TimeProvider _time;
    private int _statements;

    private PostgresSession(
        OpenRequest request,
        IRunLog log,
        PostgresConnection connection,
        PostgresLive live,
        ServerInfo server
    )
    {
        _exchange = request.Exchange;
        _time = request.Time;
        _log = log;
        _live = live;
        Connection = connection;
        Server = server;
    }

    public ServerInfo Server { get; }

    /// <summary>
    /// The session's connection.  The container tests ask on it what a describe left behind:
    /// <c>pg_prepared_statements</c> shows the statements of its own session alone.
    /// </summary>
    internal PostgresConnection Connection { get; }

    /// <summary>Asks <c>open</c>, and gives the session of the server that answered.</summary>
    /// <param name="request">The database, its connection, the exchange and the clock.</param>
    /// <param name="log">Where the session's events go.</param>
    /// <param name="cancellationToken">Ends the call.</param>
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The session owns the connection and is its caller's to close: the run closes what it "
            + "opened.  The analyzer follows a disposable that is returned itself and not one that is returned in "
            + "an Outcome.  docs/tech-debt/TD-0041."
    )]
    public static async Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    )
    {
        var connection = new PostgresConnection(request.Connection);
        var live = new PostgresLive(connection);
        var kept = false;
        try
        {
            var answer = await request.Exchange.AskAsync(
                "open",
                PostgresOpenRequest.Instance,
                (_, token) => live.OpenAsync(token),
                cancellationToken
            );
            if (answer.Error is { } error)
            {
                return Outcome<IDescribeSession, DescribeFailure>.Failed(PostgresErrors.Open(error, request.Database));
            }

            var session = new PostgresSession(request, log, connection, live, ServerOf(answer));
            kept = true;
            return Outcome<IDescribeSession, DescribeFailure>.Of(session);
        }
        finally
        {
            if (!kept)
            {
                await connection.DisposeAsync();
            }
        }
    }

    /// <summary>
    /// <c>server_version</c> as a sidecar records it: <c>18.4 (Debian 18.4-1.pgdg13+1)</c> is <c>18.4</c>.
    /// </summary>
    public static string VersionOf(string serverVersion)
    {
        var space = serverVersion.IndexOf(' ', StringComparison.Ordinal);
        return space < 0 ? serverVersion : serverVersion[..space];
    }

    public async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    )
    {
        // Before anything is sent: one statement, and every declared type written as a type name.
        if (ParameterPlaceholders.Rewrite(request.Sql, request.Parameters) is not { } sql)
        {
            return Failed(PostgresErrors.SeveralStatements(request));
        }

        var types = ParameterPlaceholders.TypeList(request.Parameters);
        if (!types.Succeeded)
        {
            return Failed(PostgresErrors.TypeRefused(request, types.Failure));
        }

        // The counter advances for a prepare that fails too: a name is used once.
        _statements++;
        var prepare = new PostgresPrepareRequest(
            string.Create(CultureInfo.InvariantCulture, $"sqlsource_{_statements}"),
            sql,
            types.Value
        );
        var prepared = await PrepareAsync(request.Name, prepare, cancellationToken);
        if (prepared.Error is { } error)
        {
            // There is no statement to free.
            return Failed(PostgresErrors.Prepare(error, request, prepare));
        }

        try
        {
            return await TypeParametersAsync(request, prepared, cancellationToken);
        }
        finally
        {
            // With the describe's own token: a cancelled describe leaves the statement to die with its session.
            // The error of the call is never reported.
            await DeallocateAsync(new PostgresDeallocateRequest(prepare.Statement), cancellationToken);
        }
    }

    // Each parameter of the query's list gets the type the server resolved for it.
    private async Task<Outcome<QueryDescription, DescribeFailure>> TypeParametersAsync(
        DescribeRequest request,
        PostgresPrepareAnswer prepared,
        CancellationToken cancellationToken
    )
    {
        var count = request.Parameters.Count;
        var resolved =
            prepared.ParameterTypes
            ?? throw new InvalidOperationException("The answer to 'prepare' holds no types and no error.");
        if (resolved.Count < count)
        {
            throw new InvalidOperationException(
                $"The server resolved {resolved.Count} types for the {count} parameters of '{request.Name}'."
            );
        }

        if (count == 0)
        {
            return Described([]);
        }

        // The server lists a type for a "$n" that the SQL writes itself, after the query's own.  It is no parameter
        // of the query: the list's are the first.
        var oids = resolved.Take(count).ToList();
        var answer = await TypesAsync(new PostgresTypesRequest([.. oids.Distinct().Order()]), cancellationToken);
        if (answer.Error is { } error)
        {
            return Failed(PostgresErrors.Types(error, request));
        }

        var rows = (
            answer.Rows ?? throw new InvalidOperationException("The answer to 'types' holds no rows and no error.")
        ).ToDictionary(static row => row.Oid);
        var parameters = new List<DescribedParameter>(count);
        for (var index = 0; index < count; index++)
        {
            var parameter = request.Parameters[index];
            if (PostgresTypes.IsPseudo(oids[index], rows))
            {
                // The server accepts a pseudo-type, declared or inferred, and no value has one.
                return Failed(PostgresErrors.PseudoType(request, parameter, rows[oids[index]].FormatType));
            }

            // A parameter has no facets: the protocol gives it an OID and no modifier.
            parameters.Add(
                new DescribedParameter(
                    parameter.Name,
                    PostgresTypes.Build(oids[index], rows, PostgresFacets.None),
                    parameter.Type is null ? SidecarValues.TypeSource.Inferred : SidecarValues.TypeSource.Declared
                )
            );
        }

        return Described(parameters);
    }

    // The parameters, the result kind None and nothing else: the session asks the server for no columns.
    private static Outcome<QueryDescription, DescribeFailure> Described(IReadOnlyList<DescribedParameter> parameters) =>
        Outcome<QueryDescription, DescribeFailure>.Of(
            new QueryDescription(
                SidecarResultKind.None,
                new EquatableArray<DescribedParameter>([.. parameters]),
                null,
                null,
                null,
                null
            )
        );

    public ValueTask DisposeAsync() => Connection.DisposeAsync();

    private static Outcome<QueryDescription, DescribeFailure> Failed(DescribeFailure failure) =>
        Outcome<QueryDescription, DescribeFailure>.Failed(failure);

    private static ServerInfo ServerOf(PostgresOpenAnswer answer) =>
        new(
            answer.ServerVersion
                ?? throw new InvalidOperationException("The answer to 'open' holds no version and no error."),
            DriverName,
            answer.DriverVersion,
            new EquatableArray<ServerSetting>([
                .. (answer.Settings ?? []).Select(static setting => new ServerSetting(setting.Name, setting.Value)),
            ])
        );

    // Each call is asked through the exchange and written to the log where it happens, with how long it took by the
    // run's clock.  Under a replay the event is written all the same.
    private async Task<PostgresPrepareAnswer> PrepareAsync(
        string query,
        PostgresPrepareRequest request,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("prepare", request, _live.PrepareAsync, cancellationToken);
        PostgresEvents.Prepare(_log, query, request, answer, ElapsedSince(started));
        return answer;
    }

    private async Task<PostgresTypesAnswer> TypesAsync(
        PostgresTypesRequest request,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("types", request, _live.TypesAsync, cancellationToken);
        PostgresEvents.Types(_log, request, answer, ElapsedSince(started));
        return answer;
    }

    // The error of the call is logged and never reported.
    private async Task DeallocateAsync(PostgresDeallocateRequest request, CancellationToken cancellationToken)
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("deallocate", request, _live.DeallocateAsync, cancellationToken);
        PostgresEvents.Deallocate(_log, request, answer, ElapsedSince(started));
    }

    private long ElapsedSince(long started) => (long)_time.GetElapsedTime(started).TotalMilliseconds;
}
