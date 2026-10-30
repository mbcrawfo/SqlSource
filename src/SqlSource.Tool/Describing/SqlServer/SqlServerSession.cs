using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer.Columns;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using SqlSource.Tool.Describing.SqlServer.Statements;
using SqlSource.Tool.Describing.SqlServer.Types;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// A session with one SQL Server database.  Every call it makes to the server goes through the exchange, and the
/// connection is made inside a <c>live</c> function and nowhere else.
/// </summary>
/// <remarks>
/// <para>
/// A query's parameters are described by <c>sp_describe_undeclared_parameters</c>, which refuses a parameter used
/// twice.  So each use of a reused parameter is renamed to a copy of its own (<see cref="ParameterCopies" />), and
/// the copies' types are unified: when they differ, a <c>probe</c> asks whether each copy binds declared <c>xml</c>
/// and declared <c>int</c>, and a copy that binds both ways types nothing and is left out.  A declared parameter's
/// type is the server's reading of its declaration, asked first, one <c>declared</c> for each.
/// </para>
/// <para>
/// A parameter is declared when its marker gives a type: a <c>-- param: @p null</c> with no type is inferred.
/// </para>
/// <para>
/// A query's columns are those of its first result set as <c>sys.dm_exec_describe_first_result_set</c> describes it
/// in browse mode 2, under the declarations of every parameter (<see cref="SqlServerColumns" />).  The server's
/// nullability is kept, except where a proof of the query's text, with the server's answer in <c>operands</c> about
/// the operands the proof needs, shows a column cannot be <c>NULL</c> (<see cref="ColumnProofs" />).  When every column
/// comes from one table or view, the server's description of <c>SELECT *</c> from it is what the result is matched
/// against (<see cref="SqlServerTableMatcher" />).  There is no plan walk, so an entry with rows records <c>plan</c>
/// as <c>skipped</c>.
/// </para>
/// </remarks>
internal sealed partial class SqlServerSession : DescriberSession
{
    public const string DriverName = "Microsoft.Data.SqlClient";

    /// <summary>The oldest major version of SQL Server the describer supports: 14, SQL Server 2017.</summary>
    public const int MinimumMajorVersion = 14;

    // The editions of SERVERPROPERTY('EngineEdition') that report a version of their own, which is no release of SQL
    // Server, and have what the describer asks: Azure SQL Database and Azure SQL Managed Instance.
    private const int AzureSqlDatabase = 5;
    private const int AzureSqlManagedInstance = 8;

    // The types a probe declares a copy with: xml converts to almost nothing, so a copy in a typed context other than
    // one of xml fails as xml, and one in a context of xml fails as int.
    private const string ProbeXml = "xml";
    private const string ProbeInt = "int";

    // The session's database, DB_NAME(), whose columns alone have an origin; null in a capture recorded before "open"
    // asked it.
    private readonly string? _database;
    private readonly SqlServerLive _live;
    private readonly IRunLog _log;

    private SqlServerSession(
        OpenRequest request,
        IRunLog log,
        SqlServerConnection connection,
        SqlServerLive live,
        ServerInfo server,
        string? database
    )
        : base(request.Exchange, request.Time, server)
    {
        _database = database;
        _live = live;
        _log = log;
        Connection = connection;
    }

    /// <summary>The session's connection, for the container tests to ask on.</summary>
    internal SqlServerConnection Connection { get; }

    /// <summary>Asks <c>open</c>, and gives the session of the server that answered.</summary>
    /// <param name="request">The database, its connection, the exchange and the clock.</param>
    /// <param name="log">
    /// Where the session's events go.  <c>open</c> has none: <c>database.open</c> holds its answer.  When it keeps
    /// anything, SqlClient's own events go there too while the session lives, and while it fails to open
    /// (<see cref="SqlServerConnection" />).
    /// </param>
    /// <param name="cancellationToken">Ends the call.</param>
    public static Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    )
    {
        var connection = new SqlServerConnection(request.Connection, log);
        var live = new SqlServerLive(connection);
        return OpenAsync(
            request,
            connection,
            new SessionOpening<SqlServerOpenRequest, SqlServerOpenAnswer>(
                SqlServerCallNames.Open,
                SqlServerOpenRequest.Instance,
                (_, token) => live.OpenAsync(token),
                answer => answer.Error is { } error ? SqlServerErrors.Open(error, request.Database) : null,
                ServerOf,
                static (answer, server) => IsBelowTheFloor(server.Version, answer.EngineEdition),
                server => SqlServerErrors.VersionNotSupported(request.Database, server.Version),
                (answer, server) => new SqlServerSession(request, log, connection, live, server, answer.Database)
            ),
            cancellationToken
        );
    }

    /// <summary>
    /// The description of a query: its parameters, each typed by the server, and the columns of its first result set,
    /// with the table whose column list they are, or none for a statement without rows.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The order: each declared type is read against T-SQL's grammar of a type name before anything is sent; then
    /// <c>declared</c> for each declared parameter, in the list's order; then <c>parameters</c>, with the SQL whose
    /// reused parameters are renamed to copies and the declarations; then the comparison of the server's parameters
    /// with the list, before any <c>probe</c>, so that a text whose parameters the two read differently is not probed;
    /// then, for each reused parameter whose copies' types differ, the probes of its copies; then <c>columns</c>, with
    /// the SQL as written and every parameter declared; then <c>operands</c>, when a proof of a column needs the
    /// server, with the same declarations; then, when every column has an origin in one table, <c>table</c> for it,
    /// and the match.
    /// </para>
    /// <para>
    /// A <c>parameters</c> that fails with <see cref="SqlServerErrors.RefusedBatch" /> asks <c>columns</c> with the
    /// declared parameters alone before it is reported, since that error says only that the batch did not compile
    /// and <c>columns</c> may give its cause (<see cref="SqlServerErrors.AfterRefusal" />).
    /// </para>
    /// </remarks>
    public override async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    )
    {
        var declarations = new List<(SqlQueryParameter Parameter, string Type)>();
        foreach (var parameter in request.Parameters)
        {
            if (parameter.Type is not { } text)
            {
                continue;
            }

            var read = DeclaredTypeText.Read(text);
            if (!read.Succeeded)
            {
                return Failed(SqlServerErrors.TypeRefused(request, parameter, read.Failure));
            }

            declarations.Add((parameter, read.Value));
        }

        var declaredTypes = new Dictionary<string, SqlServerType>(StringComparer.OrdinalIgnoreCase);
        foreach (var (parameter, type) in declarations)
        {
            var resolved = await ResolveAsync(request, parameter, type, cancellationToken);
            if (!resolved.Succeeded)
            {
                return Failed(resolved.Failure);
            }

            declaredTypes[parameter.Name] = resolved.Value;
        }

        var copied = ParameterCopies.Rename(request.Sql, request.Parameters);
        var declared = declarations
            .Select(static declaration => (declaration.Parameter.Name, declaration.Type))
            .ToList();
        var declaredParams = declared.Count == 0 ? null : SqlServerLive.Declarations(declared);
        var answer = await ParametersAsync(
            request.Name,
            new SqlServerParametersRequest(copied.Sql, declaredParams),
            cancellationToken
        );
        if (answer.Error is { } error)
        {
            if (error.Errors.Any(static row => row.Number == SqlServerErrors.RefusedBatch))
            {
                var refusal = await ColumnsAsync(
                    request.Name,
                    new SqlServerColumnsRequest(request.Sql, declaredParams),
                    cancellationToken
                );
                return Failed(SqlServerErrors.AfterRefusal(request, error, copied, refusal));
            }

            return Failed(SqlServerErrors.Parameters(request, error, copied));
        }

        var rows =
            answer.Rows
            ?? throw new InvalidOperationException("The answer to 'parameters' holds no rows and no error.");
        var differences = ParameterCopies.CompareLists(
            request
                .Parameters.Where(static parameter => parameter.Type is null)
                .Select(static parameter => parameter.Name),
            rows.Select(static row => row.Name),
            copied.ParameterOfCopy
        );
        if (differences.Count > 0)
        {
            return Failed(SqlServerErrors.ListsDiffer(request, differences));
        }

        var rowOf = new Dictionary<string, SqlServerParameterRow>(StringComparer.OrdinalIgnoreCase);
        foreach (var row in rows)
        {
            _ = rowOf.TryAdd(SqlParameterName.WithoutPrefix(row.Name, "@"), row);
        }

        // Each declared parameter's declaration, as DeclaredTypeText read it, by its name.
        var declarationOf = declarations.ToDictionary(
            static declaration => declaration.Parameter.Name,
            static declaration => declaration.Type,
            StringComparer.OrdinalIgnoreCase
        );
        var probes = new Probes(request, copied, declarationOf, rowOf);
        var reusedTypes = new Dictionary<string, SqlServerType>(StringComparer.OrdinalIgnoreCase);
        foreach (var reused in copied.Reused)
        {
            var unified = await UnifyAsync(request, reused, probes, rowOf, cancellationToken);
            if (!unified.Succeeded)
            {
                return Failed(unified.Failure);
            }

            reusedTypes[reused.Name] = unified.Value;
        }

        var parameters = new EquatableArray<DescribedParameter>([
            .. request.Parameters.Select(parameter => DescribedOf(parameter, declaredTypes, reusedTypes, rowOf)),
        ]);
        return await ResultAsync(request, parameters, ColumnsParams(parameters, declarationOf), cancellationToken);
    }

    public override ValueTask DisposeAsync() => Connection.DisposeAsync();

    // The columns of the query's first result set, through "columns", corrected by the proofs through "operands", and
    // the match of its table, through "table", with the parameters already described.
    private async Task<Outcome<QueryDescription, DescribeFailure>> ResultAsync(
        DescribeRequest request,
        EquatableArray<DescribedParameter> parameters,
        string? declarations,
        CancellationToken cancellationToken
    )
    {
        var answer = await ColumnsAsync(
            request.Name,
            new SqlServerColumnsRequest(request.Sql, declarations),
            cancellationToken
        );
        var rows = RowsOf(request, answer, DescribeStep.DescribeColumns, SqlServerCallNames.Columns);
        if (!rows.Succeeded)
        {
            return Failed(rows.Failure);
        }

        var built = SqlServerColumns.Build(rows.Value, _database);
        if (!built.Succeeded)
        {
            return Failed(SqlServerErrors.NoColumnName(request, built.Failure));
        }

        if (built.Value.Count == 0)
        {
            return Outcome<QueryDescription, DescribeFailure>.Of(
                new QueryDescription(SidecarResultKind.None, parameters, null, null, null, null)
            );
        }

        var proved = await ProvedAsync(request, built.Value, rows.Value, declarations, cancellationToken);
        foreach (var column in proved)
        {
            SqlServerEvents.Column(_log, request.Name, column.Column, column.NoOriginReason);
        }

        var columns = new EquatableArray<SidecarColumn>([.. proved.Select(static column => column.Column)]);
        var matched = await MatchAsync(request, columns, cancellationToken);
        if (!matched.Succeeded)
        {
            return Failed(matched.Failure);
        }

        var (table, tableMatch) = matched.Value;
        return Outcome<QueryDescription, DescribeFailure>.Of(
            new QueryDescription(
                SidecarResultKind.Rows,
                parameters,
                columns,
                table,
                SidecarValues.Plan.Skipped,
                tableMatch
            )
        );
    }

    // The columns, each that a proof shows cannot be NULL answered not null, through "operands" when a proof needs the
    // server.  What "operands" answers never fails the describe: an answer that proves nothing leaves the proofs of
    // the text alone, and every other column keeps the server's answer.
    private async Task<IReadOnlyList<BuiltColumn>> ProvedAsync(
        DescribeRequest request,
        IReadOnlyList<BuiltColumn> built,
        IReadOnlyList<SqlServerColumnRow> rows,
        string? declarations,
        CancellationToken cancellationToken
    )
    {
        var proofs = ColumnProofs.Read(request.Sql, built, rows);
        if (proofs.OperandsSql is not { } operandsSql)
        {
            return proofs.Apply(null);
        }

        var answer = await OperandsAsync(
            request.Name,
            new SqlServerColumnsRequest(operandsSql, declarations),
            cancellationToken
        );
        return proofs.Apply(OperandsQuery.AnswersOf(answer, proofs.OperandCount));
    }

    // The table whose column list the columns are, or the first check that failed.  "table" is asked only when every
    // column has an origin in one table, since the first two checks need none, and a call that fails for a table the
    // match never reads would fail the query.  Its rows are corrected as the columns of SELECT * from the table alone
    // are, so that a query the proofs corrected is compared with a table they corrected.
    private async Task<Outcome<(SidecarTable? Table, string TableMatch), DescribeFailure>> MatchAsync(
        DescribeRequest request,
        EquatableArray<SidecarColumn> columns,
        CancellationToken cancellationToken
    )
    {
        IReadOnlyList<SqlServerColumnRow>? rows = null;
        if (SqlServerTableMatcher.TableOf(columns) is { Schema: { } schema, Table: { } name })
        {
            var answer = await TableAsync(request.Name, new SqlServerTableRequest(schema, name), cancellationToken);
            var described = RowsOf(request, answer, DescribeStep.TableMatch, SqlServerCallNames.Table);
            if (!described.Succeeded)
            {
                return Outcome<(SidecarTable?, string), DescribeFailure>.Failed(described.Failure);
            }

            rows = ColumnProofs.TableRows(described.Value);
        }

        var (table, tableMatch) = SqlServerTableMatcher.Match(columns, rows);
        SqlServerEvents.TableMatch(_log, request.Name, table, tableMatch);
        return Outcome<(SidecarTable?, string), DescribeFailure>.Of((table, tableMatch));
    }

    // The type the server reads a declared parameter's declaration as, through "declared".
    private async Task<Outcome<SqlServerType, DescribeFailure>> ResolveAsync(
        DescribeRequest request,
        SqlQueryParameter parameter,
        string type,
        CancellationToken cancellationToken
    )
    {
        var answer = await DeclaredAsync(
            request.Name,
            new SqlServerDeclaredRequest(
                SqlServerLive.DeclaredSelect(parameter.Name),
                SqlServerLive.Declarations([(parameter.Name, type)])
            ),
            cancellationToken
        );
        if (answer.Error is { } error)
        {
            return Outcome<SqlServerType, DescribeFailure>.Failed(
                SqlServerErrors.Failed(request, error, DescribeStep.ResolveType)
            );
        }

        if (answer.Errors is { Count: > 0 })
        {
            return Outcome<SqlServerType, DescribeFailure>.Failed(SqlServerErrors.Declared(request, parameter, answer));
        }

        return answer.Rows is [var row]
            ? Outcome<SqlServerType, DescribeFailure>.Of(SqlServerTypes.FromDeclared(row))
            : throw new InvalidOperationException("The answer to 'declared' holds no error and not one row.");
    }

    // The type of a reused parameter from its copies: their one type when every copy is typed and the types agree, and
    // otherwise the type of the copies the probes do not leave out.
    private async Task<Outcome<SqlServerType, DescribeFailure>> UnifyAsync(
        DescribeRequest request,
        ReusedParameter reused,
        Probes probes,
        Dictionary<string, SqlServerParameterRow> rowOf,
        CancellationToken cancellationToken
    )
    {
        var types = reused
            .Copies.Select(copy => rowOf.TryGetValue(copy, out var row) ? SqlServerTypes.FromParameter(row) : null)
            .ToList();
        if (ParameterCopies.Agreed(types) is { } agreed)
        {
            return Outcome<SqlServerType, DescribeFailure>.Of(agreed);
        }

        var findings = new List<CopyFinding>(types.Count);
        for (var index = 0; index < types.Count; index++)
        {
            var copy = reused.Copies[index];
            var asXml = await ProbeAsync(request.Name, probes.Request(copy, ProbeXml), cancellationToken);
            if (asXml.Error is { } xmlError)
            {
                return Outcome<SqlServerType, DescribeFailure>.Failed(
                    SqlServerErrors.Failed(request, xmlError, DescribeStep.DescribeParameters)
                );
            }

            var ignored = false;
            if (asXml.Binds == true)
            {
                var asInt = await ProbeAsync(request.Name, probes.Request(copy, ProbeInt), cancellationToken);
                if (asInt.Error is { } intError)
                {
                    return Outcome<SqlServerType, DescribeFailure>.Failed(
                        SqlServerErrors.Failed(request, intError, DescribeStep.DescribeParameters)
                    );
                }

                ignored = asInt.Binds == true;
            }

            findings.Add(new CopyFinding(types[index], ignored));
        }

        var unified = ParameterCopies.Unify(findings);
        return unified.Succeeded
            ? Outcome<SqlServerType, DescribeFailure>.Of(unified.Value)
            : unified.Failure switch
            {
                { First: { } first, Second: { } second } => Outcome<SqlServerType, DescribeFailure>.Failed(
                    SqlServerErrors.CopiesDisagree(request, reused.Name, first, second)
                ),
                _ => Outcome<SqlServerType, DescribeFailure>.Failed(SqlServerErrors.NoType(request, reused.Name)),
            };
    }
}
