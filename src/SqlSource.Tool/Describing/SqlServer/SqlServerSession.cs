using System;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// A session with one SQL Server database.  Every call it makes to the server goes through the exchange, and the
/// connection is made inside a <c>live</c> function and nowhere else.
/// </summary>
internal sealed partial class SqlServerSession : IDescribeSession
{
    public const string DriverName = "Microsoft.Data.SqlClient";

    /// <summary>The oldest major version of SQL Server the describer supports: 14, SQL Server 2017.</summary>
    public const int MinimumMajorVersion = 14;

    // The editions of SERVERPROPERTY('EngineEdition') that report a version of their own, which is no release of SQL
    // Server, and have what the describer asks: Azure SQL Database and Azure SQL Managed Instance.
    private const int AzureSqlDatabase = 5;
    private const int AzureSqlManagedInstance = 8;

    private SqlServerSession(SqlServerConnection connection, ServerInfo server)
    {
        Connection = connection;
        Server = server;
    }

    public ServerInfo Server { get; }

    /// <summary>The session's connection, for the container tests to ask on.</summary>
    internal SqlServerConnection Connection { get; }

    /// <summary>Asks <c>open</c>, and gives the session of the server that answered.</summary>
    /// <param name="request">The database, its connection, the exchange and the clock.</param>
    /// <param name="log">Where the session's events go.</param>
    /// <param name="cancellationToken">Ends the call.</param>
    public static async Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
        IRunLog log,
        CancellationToken cancellationToken
    )
    {
        // The describer writes no event of its own until its events are added: the run's database.open holds what
        // open answered.
        _ = log;
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

            var session = new SqlServerSession(connection, server);
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
    /// The description of a query.  Until the parameters are described it is that of a statement without rows and
    /// without parameters, and nothing is asked.
    /// </summary>
    public Task<Outcome<QueryDescription, DescribeFailure>> DescribeAsync(
        DescribeRequest request,
        CancellationToken cancellationToken
    ) =>
        Task.FromResult(
            Outcome<QueryDescription, DescribeFailure>.Of(
                new QueryDescription(
                    SidecarResultKind.None,
                    EquatableArray<DescribedParameter>.Empty,
                    null,
                    null,
                    null,
                    null
                )
            )
        );

    public ValueTask DisposeAsync() => Connection.DisposeAsync();

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
}
