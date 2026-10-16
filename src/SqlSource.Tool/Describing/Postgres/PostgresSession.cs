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
/// an <c>EXECUTE</c> that is never run, which gives its columns.  For a query that the outer-join gate lets in, or
/// one with a column of no origin, an <c>EXPLAIN</c> of that <c>EXECUTE</c> under <c>force_generic_plan</c> gives
/// the plan that <see cref="NullabilityWalk" /> walks.  A prepared statement is session state and
/// survives a rollback, so it is freed in a <c>finally</c>, outside any transaction; one left behind would be a
/// leak until the session ends.  Its name is a counter, since the name is part of a request and a capture must be
/// the same each time it is recorded.
/// </remarks>
internal sealed class PostgresSession : IDescribeSession
{
    public const string DriverName = "Npgsql";

    /// <summary>The oldest major version of PostgreSQL the describer supports.</summary>
    public const int MinimumMajorVersion = 14;

    private readonly string _database;
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
        _database = request.Database;
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
        var connection = new PostgresConnection(request.Connection, log);
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

            var server = ServerOf(answer);
            if (MajorVersionOf(server.Version) is { } major && major < MinimumMajorVersion)
            {
                return Outcome<IDescribeSession, DescribeFailure>.Failed(
                    PostgresErrors.VersionNotSupported(request.Database, server.Version)
                );
            }

            var session = new PostgresSession(request, log, connection, live, server, explainSettings);
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

    /// <summary>
    /// The major version of a version as a sidecar records it: its leading digits, so <c>13.12</c> is 13 and
    /// <c>18beta1</c> is 18.  Null when it starts with no digit, or with more of them than an <see cref="int" />
    /// holds: the tool cannot tell what it is.
    /// </summary>
    public static int? MajorVersionOf(string version)
    {
        var end = 0;
        while (end < version.Length && char.IsAsciiDigit(version[end]))
        {
            end++;
        }

        return int.TryParse(version.AsSpan(0, end), NumberStyles.None, CultureInfo.InvariantCulture, out var major)
            ? major
            : null;
    }

    public async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    )
    {
        // Before anything is sent: one statement, of a kind PREPARE takes, and every declared type written as a type
        // name.
        if (ParameterPlaceholders.Rewrite(request.Sql, request.Parameters) is not { } sql)
        {
            return Failed(PostgresErrors.SeveralStatements(request));
        }

        if (StatementShape.RefusedKind(request.Sql) is { } kind)
        {
            return Failed(PostgresErrors.NotDescribable(request, kind));
        }

        if (StatementShape.PositionalParameter(request.Sql) is { } written)
        {
            return Failed(PostgresErrors.PositionalParameter(request, written));
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
            return Failed(PostgresErrors.Prepare(error, request, prepare, _database));
        }

        try
        {
            return await DescribePreparedAsync(request, prepare.Statement, prepared, cancellationToken);
        }
        finally
        {
            // With the describe's own token: a cancelled describe leaves the statement to die with its session,
            // because a cancelled run must not wait on a server, and the run closes the session next.  The error of
            // the call is never reported.
            await DeallocateAsync(new PostgresDeallocateRequest(prepare.Statement), cancellationToken);
        }
    }

    // What is asked of a statement that was prepared, in this order: its columns, the tables they name, and every
    // type that the parameters, the columns and their origins have, once; the server's spelling of each type with
    // each modifier the built types need, once; Npgsql's own reading of the SQL; and, for a query with rows that is
    // gated or has a column of no origin, its generic plan, then the ancestors of the relations an Append of the plan
    // may scan as partitions, and for a set operation the relations its branches scan that "tables" did not answer.
    // The table match is made last and asks nothing: it reads the nullability the walk set.
    private async Task<Outcome<QueryDescription, DescribeFailure>> DescribePreparedAsync(
        DescribeRequest request,
        string statement,
        PostgresPrepareAnswer prepared,
        CancellationToken cancellationToken
    )
    {
        var parameterOids = ParameterOids(request, prepared);

        // Behind a pooler in transaction mode this call can reach another backend: one that lacks the statement
        // answers no columns and no error, which "check" reports as SQLSRC231, and one that holds a statement of the
        // same name from an earlier run answers that statement's columns (TD-0049).
        var answer = await ColumnsAsync(
            request.Name,
            new PostgresColumnsRequest(statement, request.Parameters.Count),
            cancellationToken
        );
        if (answer.Error is { } refused)
        {
            return Failed(PostgresErrors.Columns(refused, request, _database));
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

        var spellings = await SpellingsOfAsync(parameterOids, columns, origins, rows.Value, cancellationToken);
        if (!spellings.Succeeded)
        {
            return Failed(PostgresErrors.Types(spellings.Failure, request));
        }

        var parameters = TypeParameters(request, parameterOids, rows.Value, spellings.Value);
        if (!parameters.Succeeded)
        {
            return Failed(parameters.Failure);
        }

        // A statement without rows has nothing a join could null-extend.
        var gated = columns.Count > 0 && OuterJoinGate.IsGated(request.Sql);
        var built = BuildColumns(request, columns, origins, rows.Value, spellings.Value, gated);
        if (!built.Succeeded)
        {
            return Failed(built.Failure);
        }

        if (await CheckAsync(request, parameters.Value, columns, rows.Value, cancellationToken) is { } differs)
        {
            return Failed(differs);
        }

        // After the check, for a gated query and for one with a column of no origin, which the plan may answer: one
        // whose every column is a table's or a view's and that is not gated pays nothing.
        WalkOutcome? walked = null;
        if (gated || (columns.Count > 0 && origins.Any(static origin => origin is null)))
        {
            var walk = await WalkAsync(
                request,
                statement,
                ArgumentCount(prepared),
                built.Value,
                origins,
                new WalkScope(
                    gated,
                    OuterJoinGate.NamesOldOrNew(request.Sql),
                    PostgresSearchPath.PutsCatalogFirst(SettingOf("search_path")),
                    tables.Value,
                    ColumnTypes: [.. columns.Select(static column => column.TypeOid)],
                    SetOperationAliasMayBeWritten: request.Sql.Contains("*SELECT*", StringComparison.Ordinal)
                ),
                cancellationToken
            );
            if (!walk.Succeeded)
            {
                return Failed(walk.Failure);
            }

            walked = walk.Value;
        }

        var recorded = RecordedColumns(built.Value, walked);

        // After everything that sets a column's nullability: the last check of the match reads it.
        var match = MatchOf(request, recorded, origins);
        return Described(parameters.Value, recorded, walked?.Plan ?? SidecarValues.Plan.NotNeeded, match);
    }

    // A setting as "open" read it, or null.
    private string? SettingOf(string name) =>
        Server.Settings.FirstOrDefault(setting => string.Equals(setting.Name, name, StringComparison.Ordinal))?.Value;

    // The columns as they are recorded: the walk's when there was one, and else as they were built.
    private static IReadOnlyList<SidecarColumn> RecordedColumns(
        IReadOnlyList<SidecarColumn> built,
        WalkOutcome? walked
    ) => walked is null ? built : [.. walked.Columns.Select(static column => column.Column)];

    // How many arguments an EXECUTE of the statement takes: a type for each parameter the server resolved, which is
    // the query's own, since the SQL holds no "$n" of its own.  The describe of "columns" takes any number, and
    // EXPLAIN EXECUTE refuses any but this one.
    private static int ArgumentCount(PostgresPrepareAnswer prepared) =>
        prepared.ParameterTypes?.Count
        ?? throw new InvalidOperationException("The answer to 'prepare' holds no types and no error.");

    // Asks for the generic plan of the statement, with a NULL for each of its arguments, and walks it.  A plan the
    // server will not give is no fault of the query: the error is logged and never reported, and the columns keep
    // the heuristic, as they do when the text is no plan.  The one error that is reported is 26000, a statement
    // that is gone: the connection does not keep a session, and the describe is no longer of this query.
    private async Task<Outcome<WalkOutcome, DescribeFailure>> WalkAsync(
        DescribeRequest request,
        string statement,
        int arguments,
        IReadOnlyList<SidecarColumn> columns,
        ColumnOrigin?[] origins,
        WalkScope scope,
        CancellationToken cancellationToken
    )
    {
        var asked = new PostgresExplainRequest(statement, arguments, _explainSettings);
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("explain", asked, _live.ExplainAsync, cancellationToken);
        PostgresEvents.Explain(_log, request.Name, asked, answer, ElapsedSince(started));

        if (answer.Error is { SqlState: PostgresErrors.InvalidSqlStatementName } gone)
        {
            return Outcome<WalkOutcome, DescribeFailure>.Failed(
                PostgresErrors.SessionNotKept(
                    _database,
                    DescribeStep.Explain,
                    PostgresErrors.ServerLines(gone with { Position = null }, 0)
                )
            );
        }

        WalkOutcome outcome;
        if (answer.Error is not null)
        {
            outcome = NullabilityWalk.Unavailable(WalkReasons.ExplainFailed, columns);
        }
        else if (answer.Plan is { } text && PlanNode.Read(text) is { } root)
        {
            var partitions = await PartitionsOfAsync(request, root, cancellationToken);
            var relations = await RelationsOfAsync(request, root, partitions, origins, scope.Tables, cancellationToken);
            outcome = NullabilityWalk.Run(
                root,
                columns,
                origins,
                scope with
                {
                    Partitions = partitions,
                    Tables = relations,
                }
            );
        }
        else
        {
            outcome = NullabilityWalk.Unavailable(WalkReasons.Unreadable, columns);
        }

        PostgresEvents.Walk(_log, request.Name, outcome);
        return Outcome<WalkOutcome, DescribeFailure>.Of(outcome);
    }

    // The ancestors of each relation the members of the plan's Appends scan, for the walk to tell a partitioned table
    // from a set operation, or null when the plan has no Append of that shape and nothing is asked.  An error is
    // logged and never reported: every Append is then read as a set operation, as it was before the call.
    private async Task<IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>>?> PartitionsOfAsync(
        DescribeRequest request,
        PlanNode root,
        CancellationToken cancellationToken
    )
    {
        var members = PlanPartitions.MembersOf(root);
        if (members.Count == 0)
        {
            return null;
        }

        var asked = new PostgresPartitionsRequest(members);
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("partitions", asked, _live.PartitionsAsync, cancellationToken);
        PostgresEvents.Partitions(_log, request.Name, asked, answer, ElapsedSince(started));
        return (answer.Rows ?? []).ToDictionary(
            static row => new PostgresRelationName(row.Schema, row.Name),
            static row => row.Ancestors
        );
    }

    // The tables of "tables" with a row of "relations" for each relation the plan scans that "tables" did not
    // answer, for the walk to read a set operation's branches.  It is asked only when a column has no origin and the
    // walk may answer it from the branches (NullabilityWalk.MayAnswerFromBranches), and a relation is left to ask for.
    // An error is logged and never reported: the walk goes on with the rows of "tables" alone.
    private async Task<IReadOnlyDictionary<long, PostgresTableRow>> RelationsOfAsync(
        DescribeRequest request,
        PlanNode root,
        IReadOnlyDictionary<PostgresRelationName, IReadOnlyList<long>>? partitions,
        ColumnOrigin?[] origins,
        IReadOnlyDictionary<long, PostgresTableRow> tables,
        CancellationToken cancellationToken
    )
    {
        if (!origins.Any(static origin => origin is null) || !NullabilityWalk.MayAnswerFromBranches(root, partitions))
        {
            return tables;
        }

        var answered = tables.Values.Select(static row => new PostgresRelationName(row.Schema, row.Name)).ToHashSet();
        PostgresRelationName[] missing = [.. PlanRelations.Of(root).Where(relation => !answered.Contains(relation))];
        if (missing.Length == 0)
        {
            return tables;
        }

        var asked = new PostgresRelationsRequest(missing);
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("relations", asked, _live.RelationsAsync, cancellationToken);
        PostgresEvents.Relations(_log, request.Name, asked, answer, ElapsedSince(started));
        var merged = new Dictionary<long, PostgresTableRow>(tables);
        foreach (var row in answer.Tables ?? [])
        {
            _ = merged.TryAdd(row.Oid, row);
        }

        return merged;
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

    // The types the server resolved for the parameters of the query's list, which are the first of its types.  The SQL
    // holds no "$n" of its own, so the server resolves no more; a text that the tool's lexer and the server read
    // differently could still make it list more, and the extra are left out.
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

    // The server's spelling of every pair of a type and a modifier that the parameters and the columns will be built
    // with: a parameter's domain base, and each column's type with the modifier of the row description.  The request
    // is sorted and holds each pair once.  Nothing is asked when there is no pair, as for a capture recorded before
    // the modifier was read, whose columns hold none.
    private async Task<Outcome<TypeSpellings, PostgresServerError>> SpellingsOfAsync(
        IReadOnlyList<long> parameterOids,
        IReadOnlyList<PostgresColumnRow> columns,
        ColumnOrigin?[] origins,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        CancellationToken cancellationToken
    )
    {
        PostgresTypmodPair[] pairs =
        [
            .. parameterOids
                .SelectMany(oid => PostgresTypes.PairsOf(oid, rows, TypeModifiers.NoModifier))
                .Concat(
                    columns.SelectMany(
                        (column, index) =>
                            column.TypeModifier is { } modifier
                                ? PostgresTypes.PairsOf(ColumnTypes.OidOf(column, origins[index], rows), rows, modifier)
                                : []
                    )
                )
                .Distinct()
                .OrderBy(static pair => pair.Oid)
                .ThenBy(static pair => pair.Modifier),
        ];
        if (pairs.Length == 0)
        {
            return Outcome<TypeSpellings, PostgresServerError>.Of(TypeSpellings.None);
        }

        var answer = await SpellingsAsync(new PostgresSpellingsRequest(pairs), cancellationToken);
        return answer.Error is { } error
            ? Outcome<TypeSpellings, PostgresServerError>.Failed(error)
            : Outcome<TypeSpellings, PostgresServerError>.Of(
                TypeSpellings.From(
                    answer.Rows
                        ?? throw new InvalidOperationException("The answer to 'spellings' holds no rows and no error.")
                )
            );
    }

    // Each parameter of the query's list gets the type the server resolved for it.
    private static Outcome<IReadOnlyList<DescribedParameter>, DescribeFailure> TypeParameters(
        DescribeRequest request,
        IReadOnlyList<long> oids,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        TypeSpellings spellings
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

            // The protocol gives a parameter an OID and no modifier, so its own type has none: the base of a domain
            // has the domain's.  The name it binds by is on its own type alone, and null when no name can say the
            // type, which "check" refuses before it asks.
            parameters.Add(
                new DescribedParameter(
                    parameter.Name,
                    PostgresTypes.Build(oids[index], rows, TypeModifiers.NoModifier, spellings) with
                    {
                        BindName = PostgresBindNames.Of(oids[index], rows),
                    },
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
        TypeSpellings spellings,
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

            var recorded = PostgresColumns.Build(column, origins[index], rows, spellings, gated);
            PostgresEvents.Column(_log, request.Name, recorded, gated);
            built.Add(recorded);
        }

        return Outcome<IReadOnlyList<SidecarColumn>, DescribeFailure>.Of(built);
    }

    // Asks Npgsql to describe the SQL as generated code will send it, and says what it saw otherwise than the
    // prepared statement's columns, or null.  It is asked for a query without parameters too: one Parse, which
    // still proves the two describes saw one query.  A parameter whose type has no name to bind by is refused
    // before anything is asked.
    private async Task<DescribeFailure?> CheckAsync(
        DescribeRequest request,
        IReadOnlyList<DescribedParameter> parameters,
        IReadOnlyList<PostgresColumnRow> columns,
        IReadOnlyDictionary<long, PostgresTypeRow> rows,
        CancellationToken cancellationToken
    )
    {
        for (var index = 0; index < parameters.Count; index++)
        {
            if (parameters[index].Type is { BindName: null } unnamed)
            {
                return PostgresErrors.Unnamed(request, index, unnamed.Name);
            }
        }

        var asked = new PostgresCheckRequest(
            ParameterPlaceholders.AsWritten(request.Sql)
                ?? throw new InvalidOperationException(
                    $"The SQL of '{request.Name}' was prepared and holds more than one statement."
                ),
            [.. parameters.Select(CheckParameterOf)]
        );
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("check", asked, _live.CheckAsync, cancellationToken);
        var verdict = NpgsqlCheck.Read(answer, columns, rows, request.Sql, request.Parameters);
        PostgresEvents.Check(_log, request.Name, asked, verdict, ElapsedSince(started));
        return PostgresErrors.Check(request, parameters, asked, answer, verdict);
    }

    // A parameter as generated code will bind it: by the name its type binds by, as the sidecar will record it.
    private static PostgresCheckParameter CheckParameterOf(DescribedParameter parameter) =>
        new(
            parameter.Name,
            parameter.Type?.BindName
                ?? throw new InvalidOperationException(
                    $"The parameter '{parameter.Name}' was given no name to bind by."
                )
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

    private async Task<PostgresSpellingsAnswer> SpellingsAsync(
        PostgresSpellingsRequest request,
        CancellationToken cancellationToken
    )
    {
        var started = _time.GetTimestamp();
        var answer = await _exchange.AskAsync("spellings", request, _live.SpellingsAsync, cancellationToken);
        PostgresEvents.Spellings(_log, request, answer, ElapsedSince(started));
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
