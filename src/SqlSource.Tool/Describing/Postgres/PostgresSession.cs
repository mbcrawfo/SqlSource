using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres.Walk;
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
internal sealed partial class PostgresSession : IDescribeSession
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
                PostgresCallNames.Open,
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

    public async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    )
    {
        // Before anything is sent: one statement, of a kind PREPARE takes, with no "$n" of its own, and every declared
        // type written as a type name.
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
            _ = await DeallocateAsync(new PostgresDeallocateRequest(prepare.Statement), cancellationToken);
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
        var (parameterOids, arguments) = ParameterOids(request, prepared);

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
        // a bug.
        var found = ColumnOrigin.FindAll(columns, tables.Value);
        if (!found.Succeeded)
        {
            return Failed(PostgresErrors.SchemaChanged(request, DescribeStep.Catalog, found.Failure));
        }

        var origins = found.Value;

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

        var types = CheckTypes(request, parameterOids, columns, origins, rows.Value);
        if (!types.Succeeded)
        {
            return Failed(PostgresErrors.SchemaChanged(request, DescribeStep.ResolveType, types.Failure));
        }

        var spellings = await SpellingsOfAsync(parameterOids, columns, origins, types.Value, cancellationToken);
        if (!spellings.Succeeded)
        {
            return Failed(PostgresErrors.Types(spellings.Failure, request));
        }

        var parameters = TypeParameters(request, parameterOids, types.Value, spellings.Value);
        if (!parameters.Succeeded)
        {
            return Failed(parameters.Failure);
        }

        // A statement without rows has nothing a join could null-extend.
        var gated = columns.Count > 0 && OuterJoinGate.IsGated(request.Sql);
        var built = BuildColumns(request, columns, origins, types.Value, spellings.Value, gated);
        if (!built.Succeeded)
        {
            return Failed(built.Failure);
        }

        if (await CheckAsync(request, parameters.Value, columns, types.Value, cancellationToken) is { } differs)
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
                new WalkInput(statement, arguments, built.Value, origins),
                new WalkScope(
                    gated,
                    OuterJoinGate.NamesOldOrNew(request.Sql),
                    PostgresSearchPath.PutsCatalogFirst(SettingOf("search_path")),
                    tables.Value,
                    ColumnTypes: [.. columns.Select(static column => column.TypeOid)]
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

    // Asks for the generic plan of the statement, with a NULL for each of its arguments, and walks it.  A plan the
    // server will not give is no fault of the query: the error is logged and never reported, and the columns keep
    // the heuristic, as they do when the text is no plan.  The one error that is reported is 26000, a statement
    // that is gone: the connection does not keep a session, and the describe is no longer of this query.
    private async Task<Outcome<WalkOutcome, DescribeFailure>> WalkAsync(
        DescribeRequest request,
        WalkInput input,
        WalkScope scope,
        CancellationToken cancellationToken
    )
    {
        var (statement, arguments, columns, origins) = input;
        var answer = await ExplainAsync(
            request.Name,
            new PostgresExplainRequest(statement, arguments, _explainSettings),
            cancellationToken
        );

        if (answer.Error is { SqlState: PostgresErrors.InvalidSqlStatementName } gone)
        {
            return Outcome<WalkOutcome, DescribeFailure>.Failed(
                PostgresErrors.SessionNotKept(
                    _database,
                    DescribeStep.Explain,
                    PostgresErrors.ServerLinesWithoutPosition(gone)
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
            var cache = new PlanInstancesCache(root, partitions);
            var relations = await RelationsOfAsync(request, cache, origins, scope.Tables, cancellationToken);
            outcome = NullabilityWalk.Run(cache, columns, origins, scope with { Tables = relations });
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
    // logged and never reported: every Append is then read as a set operation, as when no partition is known.
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

        var answer = await PartitionsAsync(request.Name, new PostgresPartitionsRequest(members), cancellationToken);
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
        PlanInstancesCache cache,
        ColumnOrigin?[] origins,
        IReadOnlyDictionary<long, PostgresTableRow> tables,
        CancellationToken cancellationToken
    )
    {
        if (!origins.Any(static origin => origin is null) || !NullabilityWalk.MayAnswerFromBranches(cache))
        {
            return tables;
        }

        var answered = tables.Values.Select(static row => new PostgresRelationName(row.Schema, row.Name)).ToHashSet();
        PostgresRelationName[] missing =
        [
            .. PlanRelations.Of(cache.Tree).Where(relation => !answered.Contains(relation)),
        ];
        if (missing.Length == 0)
        {
            return tables;
        }

        var answer = await RelationsAsync(request.Name, new PostgresRelationsRequest(missing), cancellationToken);
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
        CheckedTypes types,
        CancellationToken cancellationToken
    )
    {
        var rows = types.Rows;
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

    // The columns in order.  The first of a pseudo-type ends the describe: no value has such a type.  A domain is
    // never over one, so the type the protocol reported is the one to ask.
    private Outcome<IReadOnlyList<SidecarColumn>, DescribeFailure> BuildColumns(
        DescribeRequest request,
        IReadOnlyList<PostgresColumnRow> columns,
        ColumnOrigin?[] origins,
        CheckedTypes types,
        TypeSpellings spellings,
        bool gated
    )
    {
        var rows = types.Rows;
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
        CheckedTypes types,
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
        var (answer, durationMs) = await TimedAskAsync(
            PostgresCallNames.Check,
            asked,
            _live.CheckAsync,
            cancellationToken
        );
        var verdict = NpgsqlCheck.Read(answer, columns, types.Rows, request.Sql, request.Parameters);
        PostgresEvents.Check(_log, request.Name, asked, verdict, durationMs);
        return PostgresErrors.Check(request, parameters, asked, answer, verdict);
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();

    // What the walk is given of the statement: its name, how many arguments an EXECUTE of it takes, the columns as
    // they were built and their origins.
    private sealed record WalkInput(
        string Statement,
        int Arguments,
        IReadOnlyList<SidecarColumn> Columns,
        ColumnOrigin?[] Origins
    );
}
