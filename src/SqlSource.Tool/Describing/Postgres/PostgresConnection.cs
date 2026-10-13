using System;
using System.Threading;
using System.Threading.Tasks;
using Npgsql;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The one thing of the describer that holds the connection's value, and the one place that makes a connection and
/// a command.  Nothing here runs until a <c>live</c> function asks for it, so a replayed session touches no
/// driver.
/// </summary>
/// <remarks>
/// <para>
/// A session has a data source of its own.  A fresh one loads the type catalog when it first opens; a plain
/// <see cref="NpgsqlConnection" /> shares a catalog by connection string for the life of the process, which goes
/// stale when the schema changes.
/// </para>
/// <para>
/// A command's text is not a constant here, which is what <c>CA2100</c> flags: <c>.editorconfig</c> turns the rule
/// off for this file and says why.
/// </para>
/// </remarks>
/// <param name="connectionString">The connection's value.  It goes to the driver and to nothing else.</param>
internal sealed class PostgresConnection(string connectionString) : IAsyncDisposable
{
    /// <summary>
    /// What is said of a connection string the driver cannot read.  The driver's own text is not passed on: its
    /// inner exception quotes the value.
    /// </summary>
    public const string UnreadableConnectionString = "Npgsql cannot read the connection string";

    private NpgsqlDataSource? _source;
    private NpgsqlConnection? _connection;

    /// <summary>The version of the driver, as <c>major.minor.patch</c>.</summary>
    public static string? DriverVersion => typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString(3);

    /// <summary>The session's connection, opened by the first call.</summary>
    /// <exception cref="NpgsqlException">
    /// The server could not be reached or refused, or the driver cannot read the connection string.
    /// </exception>
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection;
        }

        try
        {
            _source ??= NpgsqlDataSource.Create(connectionString);
            _connection = await _source.OpenConnectionAsync(cancellationToken);
            return _connection;
        }
        catch (ArgumentException)
        {
            // The driver's word for a string it cannot read: a keyword it does not know, a value that is no number,
            // no host.  A user's mistake, and so an answer of "open" like any other.  The exception is not kept.
            throw new NpgsqlException(UnreadableConnectionString);
        }
    }

    /// <summary>A command on the session's connection.  Its caller disposes it.</summary>
    public async Task<NpgsqlCommand> CommandAsync(string sql, CancellationToken cancellationToken) =>
        new(sql, await OpenAsync(cancellationToken));

    /// <summary>What the driver threw, as the answer of a call holds it.</summary>
    public static PostgresServerError ErrorOf(NpgsqlException exception)
    {
        if (exception is not PostgresException server)
        {
            return new PostgresServerError(null, exception.Message, null, null, null);
        }

        int? position = server.Position > 0 ? server.Position : null;
        return new PostgresServerError(server.SqlState, server.MessageText, server.Detail, server.Hint, position);
    }

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
        }

        if (_source is not null)
        {
            await _source.DisposeAsync();
        }
    }

    /// <summary>The connection without its value.</summary>
    public override string ToString() => nameof(PostgresConnection);
}
