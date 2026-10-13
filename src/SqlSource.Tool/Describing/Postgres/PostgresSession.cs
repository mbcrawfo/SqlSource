using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// A session with one PostgreSQL database.  Every call it makes to the server goes through the exchange, and the
/// connection is made inside a <c>live</c> function and nowhere else.
/// </summary>
internal sealed class PostgresSession : IDescribeSession
{
    public const string DriverName = "Npgsql";

    // The settings of a session that change a description, in the order "open" answers them.
    private static readonly string[] SettingNames = ["search_path", "plan_cache_mode", "standard_conforming_strings"];

    private const string OpenSql =
        "SELECT pg_catalog.current_setting('server_version'), pg_catalog.current_setting('search_path'), "
        + "pg_catalog.current_setting('plan_cache_mode'), pg_catalog.current_setting('standard_conforming_strings')";

    private PostgresSession(PostgresConnection connection, ServerInfo server)
    {
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
    [SuppressMessage(
        "Reliability",
        "CA2000:Dispose objects before losing scope",
        Justification = "The session owns the connection and is its caller's to close: the run closes what it "
            + "opened.  The analyzer follows a disposable that is returned itself and not one that is returned in "
            + "an Outcome.  docs/tech-debt/TD-0041."
    )]
    public static async Task<Outcome<IDescribeSession, DescribeFailure>> OpenAsync(
        OpenRequest request,
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

            var session = new PostgresSession(connection, ServerOf(answer));
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

    // The steps of a describe arrive with the calls they make.  Until then a query has no typed parameter.
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

    // What the server can cause is the answer's error.  A cancellation passes through, and anything else is a bug.
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
}
