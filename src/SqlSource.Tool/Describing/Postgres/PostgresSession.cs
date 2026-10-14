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
/// it, checks it against every use of its parameter, and infers the rest.  The statement is then described through
/// an <c>EXECUTE</c> that is never run, which gives its columns.  For a query that the outer-join gate lets in, an
/// <c>EXPLAIN</c> of that <c>EXECUTE</c> under <c>force_generic_plan</c> gives the plan that
/// <see cref="NullabilityWalk" /> walks.  A prepared statement is session state and
/// survives a rollback, so it is freed in a <c>finally</c>, outside any transaction; one left behind would be a
/// leak until the session ends.  Its name is a counter, since the name is part of a request and a capture must be
/// the same each time it is recorded.
/// </remarks>
internal sealed class PostgresSession : IDescribeSession
{
    public const string DriverName = "Npgsql";

    private readonly IDescribeExchange _exchange;
    private readonly IRunLog _log;
    private readonly PostgresLive _live;
    private readonly TimeProvider _time;
    private readonly IReadOnlyList<string> _explainSettings;
    private int _statements;

    private PostgresSession(
        OpenRequest request,
        IRunLog log,
        PostgresConnection connection,
        PostgresLive live,
        ServerInfo server,
        IReadOnlyList<string> explainSettings
    )
    {
        _exchange = request.Exchange;
        _time = request.Time;
        _log = log;
        _live = live;
        Connection = connection;
        Server = server;
        _explainSettings = explainSettings;
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
    /// <param name="explainSettings">
    /// The settings of the planner that every <c>explain</c> carries: none, but in a test.
    /// </param>
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
        IReadOnlyList<string> explainSettings,
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

            var session = new PostgresSession(request, log, connection, live, ServerOf(answer), explainSettings);
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
            return await DescribePreparedAsync(request, prepare.Statement, prepared, cancellationToken);
        }
        finally
        {
            // With the describe's own token: a cancelled describe leaves the statement to die with its session.
            // The error of the call is never reported.
            await DeallocateAsync(new PostgresDeallocateRequest(prepare.Statement), cancellationToken);
        }
    }

    // What is asked of a statement that was prepared, in this order: its columns, the tables they name, and every
    // type that the parameters, the columns and their origins have, once; Npgsql's own reading of the SQL; and, for
    // a gated query with rows, its generic plan.  The table match is made last and asks nothing: it reads the
    // nullability the walk set.
    private async Task<Outcome<QueryDescription, DescribeFailure>> DescribePreparedAsync(
        DescribeRequest request,
        string statement,
        PostgresPrepareAnswer prepared,
        CancellationToken cancellationToken
    )
    {
        var parameterOids = ParameterOids(request, prepared);

        var answer = await ColumnsAsync(
            request.Name,
            new PostgresColumnsRequest(statement, request.Parameters.Count),
            cancellationToken
        );
        if (answer.Error is { } refused)
        {
            return Failed(PostgresErrors.Columns(refused, request));
        }

        var columns =
            answer.Columns
            ?? throw new InvalidOperationException("The answer to 'columns' holds no columns and no error.");

        var tables = await TablesOfAsync(columns, cancellationToken);
        if (!tables.Succeeded)
        {
            return Failed(PostgresErrors.Tables(tables.Failure, request));
        }

        // Another session may have dropped a table or a column since "columns": that is the server's change and not
        // a bug, and ColumnOrigin.Find would throw for it.
        if (
            columns
                .Select(column => ColumnOrigin.MissingFrom(column, tables.Value))
                .OfType<string>()
                .FirstOrDefault() is
            { } gone
        )
        {
            return Failed(PostgresErrors.SchemaChanged(request, DescribeStep.Catalog, gone));
        }

        ColumnOrigin?[] origins = [.. columns.Select(column => ColumnOrigin.Find(column, tables.Value))];

        // After "tables": the type of an origin attribute is where a domain is found, so the one request of "types"
        // needs the catalog's answer.
        var rows = await RowsOfAsync(
            [
                .. parameterOids,
                .. columns.Select(static column => column.TypeOid),
                .. origins.OfType<ColumnOrigin>().Select(static origin => origin.Attribute.TypeOid),
            ],
            cancellationToken
        );
        if (!rows.Succeeded)
        {
            return Failed(PostgresErrors.Types(rows.Failure, request));
        }

        if (MissingType(request, parameterOids, columns, origins, rows.Value) is { } lost)
        {
            return Failed(PostgresErrors.SchemaChanged(request, DescribeStep.ResolveType, lost));
        }

        var parameters = TypeParameters(request, parameterOids, rows.Value);
        if (!parameters.Succeeded)
        {
            return Failed(parameters.Failure);
        }

        // A statement without rows has nothing a join could null-extend.
        var gated = columns.Count > 0 && OuterJoinGate.IsGated(request.Sql);
        var built = BuildColumns(request, columns, origins, rows.Value, gated);
        if (!built.Succeeded)
        {
            return Failed(built.Failure);
        }

        if (await CheckAsync(request, parameters.Value, columns, rows.Value, cancellationToken) is { } differs)
        {
            return Failed(differs);
        }

        // After the check, and for a gated query alone: one that is not gated pays nothing.
        var walked = gated
            ? await WalkAsync(request, statement, ArgumentCount(prepared), built.Value, origins, cancellationToken)
            : null;
        var recorded = RecordedColumns(built.Value, walked);

        // After everything that sets a column's nullability: the last check of the match reads it.
        var match = MatchOf(request, recorded, origins);
        return Described(parameters.Value, recorded, walked?.Plan ?? SidecarValues.Plan.NotNeeded, match);
    }

    // The columns as they are recorded: the walk's when there was one, and else as they were built.
    private static IReadOnlyList<SidecarColumn> RecordedColumns(
        IReadOnlyList<SidecarColumn> built,
        WalkOutcome? walked
    ) => walked is null ? built : [.. walked.Columns.Select(static column => column.Column)];

    // How many arguments an EXECUTE of the statement takes: a type for each parameter the server resolved, which
    // counts a "$n" that the SQL writes itself.  The describe of "columns" takes any number, and EXPLAIN EXECUTE
    // refuses any but this one.
    private static int ArgumentCount(PostgresPrepareAnswer prepared) =>
        prepared.ParameterTypes?.Count
        ?? throw new InvalidOperationException("The answer to 'prepare' holds no types and no error.");

    // Asks for the generic plan of the statement, with a NULL for each of its arguments, and walks it.  A plan the
    // server will not give is no fault of the query: the error is logged and never reported, and the columns keep
    // the heuristic, as they do when the text is no plan.
    private async Task<WalkOutcome> WalkAsync(
        DescribeRequest request,
        string statement,
        int arguments,
        IReadOnlyList<SidecarColumn> columns,
        ColumnOrigin?[] origins,
        CancellationToken cancellationToken
    )
    {
        var asked = new PostgresExplainRequest(statement, arguments, _explainSettings);
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("explain", asked, _live.ExplainAsync, cancellationToken);
        PostgresEvents.Explain(_log, request.Name, asked, answer, ElapsedSince(started));

        var outcome = answer switch
        {
            { Error: not null } => NullabilityWalk.Unavailable(WalkReasons.ExplainFailed, columns),
            { Plan: { } text } when PlanNode.Read(text) is { } root => NullabilityWalk.Run(
                root,
                columns,
                origins,
                OuterJoinGate.NamesOldOrNew(request.Sql)
            ),
            _ => NullabilityWalk.Unavailable(WalkReasons.Unreadable, columns),
        };
        PostgresEvents.Walk(_log, request.Name, outcome);
        return outcome;
    }

    // A statement without rows has no match, and no event of one.
    private (SidecarTable? Table, string Result)? MatchOf(
        DescribeRequest request,
        IReadOnlyList<SidecarColumn> columns,
        ColumnOrigin?[] origins
    )
    {
        if (columns.Count == 0)
        {
            return null;
        }

        var match = TableMatcher.Match(columns, origins);
        PostgresEvents.TableMatch(_log, request.Name, match.Table, match.Result);
        return match;
    }

    // The first type that was asked for and has no row, in the order of the request, or null: the parameters, the
    // columns, the attributes of their origins.  A type that another session dropped since "tables" has none, and
    // PostgresTypes.Build and the classes of a column would throw for it.  A type that the closure reaches by itself,
    // the base of a domain or the element of an array, comes from the query of the row that reaches it, so it is
    // never missing alone.
    private static string? MissingType(
        DescribeRequest request,
        IReadOnlyList<long> parameterOids,
        IReadOnlyList<PostgresColumnRow> columns,
        ColumnOrigin?[] origins,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        for (var index = 0; index < parameterOids.Count; index++)
        {
            if (!rows.ContainsKey(parameterOids[index]))
            {
                var written = PostgresErrors.Written(request.Parameters[index]);
                return $"the type of the parameter '{written}' no longer exists";
            }
        }

        var column = columns.FirstOrDefault(column => !rows.ContainsKey(column.TypeOid));
        if (column is not null)
        {
            return $"the type of the column '{column.Name}' no longer exists";
        }

        for (var index = 0; index < origins.Length; index++)
        {
            if (origins[index] is { } origin && !rows.ContainsKey(origin.Attribute.TypeOid))
            {
                return $"the type of the column '{columns[index].Name}' no longer exists in its table";
            }
        }

        return null;
    }

    // The types the server resolved for the parameters of the query's list.  It lists a type for a "$n" that the SQL
    // writes itself, after the query's own: that is no parameter of the query, and the list's are the first.
    private static IReadOnlyList<long> ParameterOids(DescribeRequest request, PostgresPrepareAnswer prepared)
    {
        var count = request.Parameters.Count;
        var resolved =
            prepared.ParameterTypes
            ?? throw new InvalidOperationException("The answer to 'prepare' holds no types and no error.");
        return resolved.Count < count
            ? throw new InvalidOperationException(
                $"The server resolved {resolved.Count} types for the {count} parameters of '{request.Name}'."
            )
            : [.. resolved.Take(count)];
    }

    // The rows of every type asked for, by OID.  The request is sorted and holds each OID once, so that it is the
    // same however a query orders what it names.  Nothing is asked when there is no type to ask for.
    private async Task<Outcome<IReadOnlyDictionary<long, PostgresTypeRow>, PostgresServerError>> RowsOfAsync(
        IReadOnlyList<long> oids,
        CancellationToken cancellationToken
    )
    {
        if (oids.Count == 0)
        {
            return Outcome<IReadOnlyDictionary<long, PostgresTypeRow>, PostgresServerError>.Of(
                new Dictionary<long, PostgresTypeRow>()
            );
        }

        var answer = await TypesAsync(new PostgresTypesRequest([.. oids.Distinct().Order()]), cancellationToken);
        return answer.Error is { } error
            ? Outcome<IReadOnlyDictionary<long, PostgresTypeRow>, PostgresServerError>.Failed(error)
            : Outcome<IReadOnlyDictionary<long, PostgresTypeRow>, PostgresServerError>.Of(
                (
                    answer.Rows
                    ?? throw new InvalidOperationException("The answer to 'types' holds no rows and no error.")
                ).ToDictionary(static row => row.Oid)
            );
    }

    // The tables the columns name, by OID.  The request is sorted and holds each table once.  Nothing is asked when
    // no column names a table.
    private async Task<Outcome<IReadOnlyDictionary<long, PostgresTableRow>, PostgresServerError>> TablesOfAsync(
        IReadOnlyList<PostgresColumnRow> columns,
        CancellationToken cancellationToken
    )
    {
        long[] oids =
        [
            .. columns.Select(static column => column.TableOid).Where(static oid => oid != 0).Distinct().Order(),
        ];
        if (oids.Length == 0)
        {
            return Outcome<IReadOnlyDictionary<long, PostgresTableRow>, PostgresServerError>.Of(
                new Dictionary<long, PostgresTableRow>()
            );
        }

        var answer = await TablesAsync(new PostgresTablesRequest(oids), cancellationToken);
        return answer.Error is { } error
            ? Outcome<IReadOnlyDictionary<long, PostgresTableRow>, PostgresServerError>.Failed(error)
            : Outcome<IReadOnlyDictionary<long, PostgresTableRow>, PostgresServerError>.Of(
                (
                    answer.Tables
                    ?? throw new InvalidOperationException("The answer to 'tables' holds no tables and no error.")
                ).ToDictionary(static table => table.Oid)
            );
    }

    // Each parameter of the query's list gets the type the server resolved for it.
    private static Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure> TypeParameters(
        DescribeRequest request,
        IReadOnlyList<long> oids,
        IReadOnlyDictionary<long, PostgresTypeRow> rows
    )
    {
        var parameters = new List<DescribedParameter>(oids.Count);
        for (var index = 0; index < oids.Count; index++)
        {
            var parameter = request.Parameters[index];
            if (PostgresTypes.IsPseudo(oids[index], rows))
            {
                // The server accepts a pseudo-type, declared or inferred, and no value has one.
                return Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure>.Failed(
                    PostgresErrors.PseudoType(request, parameter, rows[oids[index]].FormatType)
                );
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

        return Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure>.Of(parameters);
    }

    // The columns in order.  The first of a pseudo-type ends the describe: no value has such a type.  A domain is
    // never over one, so the type the protocol reported is the one to ask.
    private Outcome<IReadOnlyList<SidecarColumn>, DescribeFailure> BuildColumns(
        DescribeRequest request,
        IReadOnlyList<PostgresColumnRow> columns,
        ColumnOrigin?[] origins,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        bool gated
    )
    {
        var built = new List<SidecarColumn>(columns.Count);
        for (var index = 0; index < columns.Count; index++)
        {
            var column = columns[index];
            if (PostgresTypes.IsPseudo(column.TypeOid, rows))
            {
                return Outcome<IReadOnlyList<SidecarColumn>, DescribeFailure>.Failed(
                    PostgresErrors.ColumnPseudoType(request, column.Name, rows[column.TypeOid].FormatType)
                );
            }

            var recorded = PostgresColumns.Build(column, origins[index], rows, gated);
            PostgresEvents.Column(_log, request.Name, recorded, gated);
            built.Add(recorded);
        }

        return Outcome<IReadOnlyList<SidecarColumn>, DescribeFailure>.Of(built);
    }

    // Asks Npgsql to describe the SQL as generated code will send it, and says what it saw otherwise than the
    // prepared statement's columns, or null.  It is asked for a query without parameters too: one Parse, which
    // still proves the two describes saw one query.
    private async Task<DescribeFailure?> CheckAsync(
        DescribeRequest request,
        IReadOnlyList<DescribedParameter> parameters,
        IReadOnlyList<PostgresColumnRow> columns,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        CancellationToken cancellationToken
    )
    {
        var asked = new PostgresCheckRequest(
            ParameterPlaceholders.AsWritten(request.Sql)
                ?? throw new InvalidOperationException(
                    $"The SQL of '{request.Name}' was prepared and holds more than one statement."
                ),
            [.. parameters.Select(CheckParameterOf)]
        );
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("check", asked, _live.CheckAsync, cancellationToken);
        var verdict = NpgsqlCheck.Read(answer, columns, rows);
        PostgresEvents.Check(_log, request.Name, asked, verdict, ElapsedSince(started));
        return PostgresErrors.Check(request, asked, answer, verdict);
    }

    // A parameter as generated code will bind it: by the name of its type, as the sidecar will record it.
    private static PostgresCheckParameter CheckParameterOf(DescribedParameter parameter) =>
        new(
            parameter.Name,
            parameter.Type?.Name
                ?? throw new InvalidOperationException($"The parameter '{parameter.Name}' was given no type.")
        );

    // A statement without rows has its parameters and nothing else: no columns, no table and no provenance.  One
    // with rows says what became of its plan, "not-needed" when it was not gated, and which table its result is,
    // or the first check of the match that failed.
    private static Outcome<QueryDescription, DescribeFailure> Described(
        IReadOnlyList<DescribedParameter> parameters,
        IReadOnlyList<SidecarColumn> columns,
        string plan,
        (SidecarTable? Table, string Result)? match
    )
    {
        var typed = new EquatableArray<DescribedParameter>([.. parameters]);
        return Outcome<QueryDescription, DescribeFailure>.Of(
            match is { } found
                ? new QueryDescription(
                    SidecarResultKind.Rows,
                    typed,
                    new EquatableArray<SidecarColumn>([.. columns]),
                    found.Table,
                    plan,
                    found.Result
                )
                : new QueryDescription(SidecarResultKind.None, typed, null, null, null, null)
        );
    }

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

    private async Task<PostgresColumnsAnswer> ColumnsAsync(
        string query,
        PostgresColumnsRequest request,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("columns", request, _live.ColumnsAsync, cancellationToken);
        PostgresEvents.Columns(_log, query, request, answer, ElapsedSince(started));
        return answer;
    }

    private async Task<PostgresTablesAnswer> TablesAsync(
        PostgresTablesRequest request,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("tables", request, _live.TablesAsync, cancellationToken);
        PostgresEvents.Tables(_log, request, answer, ElapsedSince(started));
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
