using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Parsing;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
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
/// </remarks>
internal sealed partial class SqlServerSession : IDescribeSession
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

    private readonly IDescribeExchange _exchange;
    private readonly SqlServerLive _live;
    private readonly IRunLog _log;
    private readonly TimeProvider _time;

    private SqlServerSession(
        OpenRequest request,
        IRunLog log,
        SqlServerConnection connection,
        SqlServerLive live,
        ServerInfo server
    )
    {
        _exchange = request.Exchange;
        _time = request.Time;
        _live = live;
        _log = log;
        Connection = connection;
        Server = server;
    }

    public ServerInfo Server { get; }

    /// <summary>The session's connection, for the container tests to ask on.</summary>
    internal SqlServerConnection Connection { get; }

    /// <summary>Asks <c>open</c>, and gives the session of the server that answered.</summary>
    /// <param name="request">The database, its connection, the exchange and the clock.</param>
    /// <param name="log">
    /// Where the session's events go.  <c>open</c> has none: <c>database.open</c> holds its answer.
    /// </param>
    /// <param name="cancellationToken">Ends the call.</param>
    public static async Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    )
    {
        var connection = new SqlServerConnection(request.Connection);
        var live = new SqlServerLive(connection);
        var kept = false;
        try
        {
            var answer = await AskOpenAsync(request.Exchange, live, cancellationToken);
            if (answer.Error is { } error)
            {
                return Outcome<IDescribeSession, DescribeFailure>.Failed(SqlServerErrors.Open(error, request.Database));
            }

            var server = ServerOf(answer);
            if (IsBelowTheFloor(server.Version, answer.EngineEdition))
            {
                return Outcome<IDescribeSession, DescribeFailure>.Failed(
                    SqlServerErrors.VersionNotSupported(request.Database, server.Version)
                );
            }

            var session = new SqlServerSession(request, log, connection, live, server);
            kept = true;
            return new Outcome<IDescribeSession, DescribeFailure>(session);
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
    /// The description of a query: its parameters, each typed by the server.  A statement's rows are not described
    /// yet, so the result is that of a statement without rows.
    /// </summary>
    /// <remarks>
    /// The order: each declared type is read against T-SQL's grammar of a type name before anything is sent; then
    /// <c>declared</c> for each declared parameter, in the list's order; then <c>parameters</c>, with the SQL whose
    /// reused parameters are renamed to copies and the declarations; then the comparison of the server's parameters
    /// with the list, before any <c>probe</c>, so that a text whose parameters the two read differently is not probed;
    /// then, for each reused parameter whose copies' types differ, the probes of its copies.
    /// </remarks>
    public async Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
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
            if (read.Failure is { } reason)
            {
                return Failed(SqlServerErrors.TypeRefused(request, parameter, reason));
            }

            declarations.Add((parameter, read.Value!));
        }

        var declaredTypes = new Dictionary<string, SqlServerType>(StringComparer.OrdinalIgnoreCase);
        foreach (var (parameter, type) in declarations)
        {
            var resolved = await ResolveAsync(request, parameter, type, cancellationToken);
            if (resolved.Failure is { } failure)
            {
                return Failed(failure);
            }

            declaredTypes[parameter.Name] = resolved.Value!;
        }

        var copied = ParameterCopies.Rename(request.Sql, request.Parameters);
        var declared = declarations
            .Select(static declaration => (declaration.Parameter.Name, declaration.Type))
            .ToList();
        var answer = await ParametersAsync(
            request.Name,
            new SqlServerParametersRequest(
                copied.Sql,
                declared.Count == 0 ? null : SqlServerLive.Declarations(declared)
            ),
            cancellationToken
        );
        if (answer.Error is { } error)
        {
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
            _ = rowOf.TryAdd(Bare(row.Name), row);
        }

        var probes = new Probes(request, copied, declarations, rowOf);
        var reusedTypes = new Dictionary<string, SqlServerType>(StringComparer.OrdinalIgnoreCase);
        foreach (var reused in copied.Reused)
        {
            var unified = await UnifyAsync(request, reused, probes, rowOf, cancellationToken);
            if (unified.Failure is { } failure)
            {
                return Failed(failure);
            }

            reusedTypes[reused.Name] = unified.Value!;
        }

        return Outcome<QueryDescription, DescribeFailure>.Of(
            new QueryDescription(
                SidecarResultKind.None,
                new EquatableArray<DescribedParameter>([
                    .. request.Parameters.Select(parameter =>
                        DescribedOf(parameter, declaredTypes, reusedTypes, rowOf)
                    ),
                ]),
                null,
                null,
                null,
                null
            )
        );
    }

    public ValueTask DisposeAsync() => Connection.DisposeAsync();

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
        return unified.Failure switch
        {
            null => Outcome<SqlServerType, DescribeFailure>.Of(unified.Value!),
            { First: { } first, Second: { } second } => Outcome<SqlServerType, DescribeFailure>.Failed(
                SqlServerErrors.CopiesDisagree(request, reused.Name, first, second)
            ),
            _ => Outcome<SqlServerType, DescribeFailure>.Failed(SqlServerErrors.NoType(request, reused.Name)),
        };
    }

    // A parameter of the list with its type: declared, from "declared"; reused, from its copies; otherwise from its
    // row, which the comparison of the lists has found.
    private static DescribedParameter DescribedOf(
        SqlQueryParameter parameter,
        Dictionary<string, SqlServerType> declaredTypes,
        Dictionary<string, SqlServerType> reusedTypes,
        Dictionary<string, SqlServerParameterRow> rowOf
    )
    {
        if (declaredTypes.TryGetValue(parameter.Name, out var declared))
        {
            return new DescribedParameter(parameter.Name, declared, SidecarValues.TypeSource.Declared);
        }

        if (reusedTypes.TryGetValue(parameter.Name, out var reused))
        {
            return new DescribedParameter(parameter.Name, reused, SidecarValues.TypeSource.InferredFromCopies);
        }

        return rowOf.TryGetValue(parameter.Name, out var row)
            ? new DescribedParameter(
                parameter.Name,
                SqlServerTypes.FromParameter(row),
                SidecarValues.TypeSource.Inferred
            )
            : throw new InvalidOperationException(
                $"The server reported no type for the parameter '{parameter.Name}', and the lists agreed."
            );
    }

    // A name without its "@", as the server reports one.
    private static string Bare(string name) => name.StartsWith('@') ? name[1..] : name;

    private static Outcome<QueryDescription, DescribeFailure> Failed(DescribeFailure failure) =>
        Outcome<QueryDescription, DescribeFailure>.Failed(failure);

    // Below the floor, unless the edition is Azure's: a version whose first part is no number is not refused, since
    // the tool cannot tell what it is.
    private static bool IsBelowTheFloor(string version, int? edition) =>
        edition is not (AzureSqlDatabase or AzureSqlManagedInstance)
        && int.TryParse(
            version.AsSpan(
                0,
                version.IndexOf('.', StringComparison.Ordinal) is var dot and >= 0 ? dot : version.Length
            ),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var major
        )
        && major < MinimumMajorVersion;

    private static ServerInfo ServerOf(SqlServerOpenAnswer answer) =>
        new(
            answer.ProductVersion
                ?? throw new InvalidOperationException("The answer to 'open' holds no version and no error."),
            DriverName,
            answer.DriverVersion,
            new EquatableArray<ServerSetting>([
                .. (answer.Settings ?? []).Select(static setting => new ServerSetting(setting.Name, setting.Value)),
            ])
        );

    /// <summary>
    /// The requests of <c>probe</c> for one describe: the SQL that <c>parameters</c> was asked, and the declarations
    /// of every parameter of the list in its order, each copy of a reused parameter in its place.
    /// </summary>
    /// <remarks>
    /// The copy that is probed is declared at the type it is probed with; every other copy and undeclared parameter
    /// at the system type that <c>parameters</c> gave it, and one it gave none is left out; a declared parameter as it
    /// is declared.
    /// </remarks>
    private sealed class Probes(
        DescribeRequest request,
        CopiedSql copied,
        List<(SqlQueryParameter Parameter, string Type)> declarations,
        Dictionary<string, SqlServerParameterRow> rowOf
    )
    {
        // Each declared parameter's declaration, as DeclaredTypeText read it, by its name.
        private readonly Dictionary<string, string> _declarationOf = declarations.ToDictionary(
            static declaration => declaration.Parameter.Name,
            static declaration => declaration.Type,
            StringComparer.OrdinalIgnoreCase
        );

        public SqlServerProbeRequest Request(string probed, string type)
        {
            var declared = new List<(string Name, string Type)>();
            foreach (var parameter in request.Parameters)
            {
                if (parameter.Type is not null)
                {
                    declared.Add((parameter.Name, _declarationOf[parameter.Name]));
                    continue;
                }

                var copies = copied.Reused.FirstOrDefault(reused =>
                    string.Equals(reused.Name, parameter.Name, StringComparison.OrdinalIgnoreCase)
                );
                foreach (var name in copies?.Copies ?? [parameter.Name])
                {
                    if (string.Equals(name, probed, StringComparison.OrdinalIgnoreCase))
                    {
                        declared.Add((name, type));
                    }
                    else if (rowOf.TryGetValue(name, out var row) && row.SuggestedSystemTypeName is { } suggested)
                    {
                        declared.Add((name, suggested));
                    }
                }
            }

            return new SqlServerProbeRequest(copied.Sql, SqlServerLive.Declarations(declared));
        }
    }
}
