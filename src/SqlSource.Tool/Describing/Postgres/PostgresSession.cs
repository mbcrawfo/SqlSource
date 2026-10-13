using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
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

    // The settings of a session that change a description, in the order "open" answers them.
    private static readonly string[] SettingNames = ["search_path", "plan_cache_mode", "standard_conforming_strings"];

    private const string OpenSql =
        "SELECT pg_catalog.current_setting('server_version'), pg_catalog.current_setting('search_path'), "
        + "pg_catalog.current_setting('plan_cache_mode'), pg_catalog.current_setting('standard_conforming_strings')";

    private const string ParameterTypesSql =
        "SELECT parameter_types::oid[] FROM pg_catalog.pg_prepared_statements WHERE name = @name";

    // The closure of the types asked for, in one query: a recursive term that follows the element of an array, the
    // base of a domain, the subtype of a range and the range of a multirange.  A domain has the category of its
    // base, so an array is a base type of the category A.
    private const string TypesSql = """
        WITH RECURSIVE closure (oid) AS (
            SELECT t.oid FROM pg_catalog.pg_type AS t WHERE t.oid = ANY (@oids)
            UNION
            SELECT link.oid
            FROM closure AS c
            JOIN pg_catalog.pg_type AS t ON t.oid = c.oid
            LEFT JOIN pg_catalog.pg_range AS r ON r.rngtypid = t.oid
            LEFT JOIN pg_catalog.pg_range AS m ON m.rngmultitypid = t.oid
            CROSS JOIN LATERAL (
                VALUES
                    (CASE WHEN t.typtype = 'b' AND t.typcategory = 'A' THEN t.typelem END),
                    (CASE WHEN t.typtype = 'd' THEN t.typbasetype END),
                    (r.rngsubtype),
                    (m.rngtypid)
            ) AS link (oid)
            WHERE link.oid IS NOT NULL AND link.oid <> 0
        )
        SELECT
            t.oid,
            t.typname::text,
            n.nspname::text,
            t.typtype::text,
            t.typcategory::text,
            t.typelem,
            t.typbasetype,
            r.rngsubtype,
            mr.rngsubtype,
            pg_catalog.format_type(t.oid, -1),
            (
                SELECT pg_catalog.array_agg(e.enumlabel::text ORDER BY e.enumsortorder)
                FROM pg_catalog.pg_enum AS e
                WHERE e.enumtypid = t.oid
            )
        FROM closure AS c
        JOIN pg_catalog.pg_type AS t ON t.oid = c.oid
        JOIN pg_catalog.pg_namespace AS n ON n.oid = t.typnamespace
        LEFT JOIN pg_catalog.pg_range AS r ON r.rngtypid = t.oid
        LEFT JOIN pg_catalog.pg_range AS m ON m.rngmultitypid = t.oid
        LEFT JOIN pg_catalog.pg_range AS mr ON mr.rngtypid = m.rngtypid
        ORDER BY t.oid
        """;

    private readonly IDescribeExchange _exchange;
    private readonly IRunLog _log;
    private readonly TimeProvider _time;
    private int _statements;

    private PostgresSession(OpenRequest request, IRunLog log, PostgresConnection connection, ServerInfo server)
    {
        _exchange = request.Exchange;
        _time = request.Time;
        _log = log;
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
        var kept = false;
        try
        {
            var answer = await request.Exchange.AskAsync(
                "open",
                PostgresOpenRequest.Instance,
                (_, token) => OpenLiveAsync(connection, token),
                cancellationToken
            );
            if (answer.Error is { } error)
            {
                return Outcome<IDescribeSession, DescribeFailure>.Failed(PostgresErrors.Open(error, request.Database));
            }

            var session = new PostgresSession(request, log, connection, ServerOf(answer));
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
        var answer = await _exchange.AskAsync("prepare", request, PrepareLiveAsync, cancellationToken);
        PostgresEvents.Prepare(_log, query, request, answer, ElapsedSince(started));
        return answer;
    }

    private async Task<PostgresTypesAnswer> TypesAsync(
        PostgresTypesRequest request,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("types", request, TypesLiveAsync, cancellationToken);
        PostgresEvents.Types(_log, request, answer, ElapsedSince(started));
        return answer;
    }

    // The error of the call is logged and never reported.
    private async Task DeallocateAsync(PostgresDeallocateRequest request, CancellationToken cancellationToken)
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("deallocate", request, DeallocateLiveAsync, cancellationToken);
        PostgresEvents.Deallocate(_log, request, answer, ElapsedSince(started));
    }

    private long ElapsedSince(long started) => (long)_time.GetElapsedTime(started).TotalMilliseconds;

    // In each "live" function, what the server can cause is the answer's error.  A cancellation passes through, and
    // anything else is a bug.
    private static async Task<PostgresOpenAnswer> OpenLiveAsync(PostgresConnection connection, CancellationToken token)
    {
        try
        {
            await using var command = await connection.CommandAsync(OpenSql, token);
            await using var reader = await command.ExecuteReaderAsync(token);
            if (!await reader.ReadAsync(token))
            {
                throw new InvalidOperationException("The server answered 'open' with no row.");
            }

            return new PostgresOpenAnswer(
                VersionOf(reader.GetString(0)),
                PostgresConnection.DriverVersion,
                [.. SettingNames.Select((name, index) => new PostgresSetting(name, reader.GetString(index + 1)))],
                null
            );
        }
        catch (NpgsqlException exception)
        {
            return new PostgresOpenAnswer(null, null, null, PostgresConnection.ErrorOf(exception));
        }
    }

    private async Task<PostgresPrepareAnswer> PrepareLiveAsync(PostgresPrepareRequest asked, CancellationToken token)
    {
        try
        {
            // The one text that holds a user's SQL: the driver is given it as one statement and may not split it.
            var text = ParameterPlaceholders.PrepareText(asked.Statement, asked.Types, asked.Sql);
            await Connection.ExecuteAsOneStatementAsync(text, token);

            await using var read = await Connection.CommandAsync(ParameterTypesSql, token);
            _ = read.Parameters.AddWithValue("name", asked.Statement);
            var types =
                await read.ExecuteScalarAsync(token) as uint[]
                ?? throw new InvalidOperationException($"The server lists no prepared statement '{asked.Statement}'.");
            return new PostgresPrepareAnswer([.. types.Select(static oid => (long)oid)], null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresPrepareAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }

    private async Task<PostgresTypesAnswer> TypesLiveAsync(PostgresTypesRequest asked, CancellationToken token)
    {
        try
        {
            await using var command = await Connection.CommandAsync(TypesSql, token);
            _ = command.Parameters.Add(
                new NpgsqlParameter("oids", asked.Oids.Select(static oid => (uint)oid).ToArray())
                {
                    DataTypeName = "oid[]",
                }
            );
            await using var reader = await command.ExecuteReaderAsync(token);
            var rows = new List<PostgresTypeRow>();
            while (await reader.ReadAsync(token))
            {
                rows.Add(
                    new PostgresTypeRow(
                        await reader.GetFieldValueAsync<uint>(0, token),
                        reader.GetString(1),
                        reader.GetString(2),
                        reader.GetString(3),
                        reader.GetString(4),
                        await reader.GetFieldValueAsync<uint>(5, token),
                        await reader.GetFieldValueAsync<uint>(6, token),
                        await reader.IsDBNullAsync(7, token) ? null : await reader.GetFieldValueAsync<uint>(7, token),
                        await reader.IsDBNullAsync(8, token) ? null : await reader.GetFieldValueAsync<uint>(8, token),
                        reader.GetString(9),
                        await reader.IsDBNullAsync(10, token)
                            ? null
                            : await reader.GetFieldValueAsync<string[]>(10, token)
                    )
                );
            }

            return new PostgresTypesAnswer(rows, null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresTypesAnswer(null, PostgresConnection.ErrorOf(exception));
        }
    }

    private async Task<PostgresDeallocateAnswer> DeallocateLiveAsync(
        PostgresDeallocateRequest asked,
        CancellationToken token
    )
    {
        try
        {
            await using var command = await Connection.CommandAsync($"DEALLOCATE {asked.Statement}", token);
            _ = await command.ExecuteNonQueryAsync(token);
            return new PostgresDeallocateAnswer(null);
        }
        catch (NpgsqlException exception)
        {
            return new PostgresDeallocateAnswer(PostgresConnection.ErrorOf(exception));
        }
    }
}
