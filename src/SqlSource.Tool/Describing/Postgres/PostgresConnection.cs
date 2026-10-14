using System;
using System.Data;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// The one thing of the describer that holds the connection's value, and the one place that makes a connection, a
/// command and a batch.  Nothing here runs until a <c>live</c> function asks for it, so a replayed session touches
/// no driver.
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
/// <param name="log">
/// The describer's log.  It is given to the driver alone, which writes its own messages to it when the log keeps
/// anything: see <see cref="NpgsqlRunLogger" />.
/// </param>
internal sealed class PostgresConnection(string connectionString, IRunLog log) : IAsyncDisposable
{
    /// <summary>
    /// What is said of a connection string the driver cannot read.  The driver's own text is not passed on: its
    /// inner exception quotes the value.
    /// </summary>
    public const string UnreadableConnectionString = "Npgsql cannot read the connection string";

    /// <summary>
    /// What is said of a call on a connection that the server has ended: a restart, an administrator, an idle
    /// timeout.  The driver's own word for it is an exception that is none of a server.
    /// </summary>
    public const string LostConnection = "the connection to the server was lost";

    private NpgsqlDataSource? _source;
    private NpgsqlConnection? _connection;
    private ILoggerFactory? _driverLog;

    /// <summary>The version of the driver, as <c>major.minor.patch</c>.</summary>
    public static string? DriverVersion => typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString(3);

    /// <summary>The session's connection, opened by the first call.</summary>
    /// <exception cref="NpgsqlException">
    /// The server could not be reached or refused, the driver cannot read the connection string, or the connection
    /// was open and is so no longer.
    /// </exception>
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            // The driver closes a connection on which a call found that the server had gone, and for a command on
            // a closed one it throws what is no NpgsqlException.  A server causes it, so it is an answer's error
            // like any other.  The session is not opened again: what it had prepared is gone with its backend.
            return _connection.State == ConnectionState.Open ? _connection : throw new NpgsqlException(LostConnection);
        }

        try
        {
            _source ??= BuildSource();
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

    // The builder reads the string, so its ArgumentException for one it cannot read is thrown here, inside the
    // caller's try.
    private NpgsqlDataSource BuildSource()
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        _driverLog = NpgsqlRunLogger.For(log);
        if (_driverLog is not null)
        {
            _ = builder.UseLoggerFactory(_driverLog);
        }

        return builder.Build();
    }

    /// <summary>
    /// A command on the session's connection.  Its caller disposes it.  The driver splits a command's text at each
    /// <c>;</c> it finds outside a string and a comment, and runs every part: it is for a text that holds nothing
    /// of a user's.
    /// </summary>
    public async Task<NpgsqlCommand> CommandAsync(string sql, CancellationToken cancellationToken) =>
        new(sql, await OpenAsync(cancellationToken));

    /// <summary>
    /// A transaction on the session's connection.  Its caller rolls it back, and disposes it: disposing one that
    /// was not ended rolls it back too.
    /// </summary>
    public async Task<NpgsqlTransaction> BeginAsync(CancellationToken cancellationToken) =>
        await (await OpenAsync(cancellationToken)).BeginTransactionAsync(cancellationToken);

    /// <summary>
    /// A batch of one command that holds a user's SQL.  Its caller adds parameters to the command, runs or describes
    /// it, and disposes the batch.
    /// </summary>
    /// <remarks>
    /// The driver does not read the command of a batch that has no parameters and never splits it: it sends the
    /// text as one statement to parse, and the server refuses a text that holds a second one, <c>42601</c>, before
    /// it runs anything.  With a named parameter the driver reads the text to place the parameter, and refuses a
    /// text in which it finds a <c>;</c> that something follows: it throws a <see cref="NotSupportedException" />
    /// and sends nothing.  So whatever the text holds, and however the tool's lexer read it, one statement at most
    /// reaches the server.
    /// </remarks>
    public async Task<NpgsqlBatch> BatchOfOneAsync(string sql, CancellationToken cancellationToken)
    {
        var batch = new NpgsqlBatch(await OpenAsync(cancellationToken));
        try
        {
            batch.BatchCommands.Add(new NpgsqlBatchCommand(sql));
            return batch;
        }
        catch
        {
            await batch.DisposeAsync();
            throw;
        }
    }

    /// <summary>
    /// Runs a text that holds a user's SQL, as a batch of one command: see <see cref="BatchOfOneAsync" />.  So
    /// whatever the text holds, and however the tool's lexer read it, one statement at most is run.
    /// </summary>
    /// <exception cref="NpgsqlException">The server refused the text, or the connection is lost.</exception>
    public async Task ExecuteAsOneStatementAsync(string sql, CancellationToken cancellationToken)
    {
        await using var batch = await BatchOfOneAsync(sql, cancellationToken);
        _ = await batch.ExecuteNonQueryAsync(cancellationToken);
    }

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
        try
        {
            if (_connection is not null)
            {
                await _connection.DisposeAsync();
            }
        }
        finally
        {
            // Whatever closing the connection threw, the data source and its pool are not left behind.
            if (_source is not null)
            {
                await _source.DisposeAsync();
            }

            _driverLog?.Dispose();
        }
    }

    /// <summary>The connection without its value.</summary>
    public override string ToString() => nameof(PostgresConnection);
}
