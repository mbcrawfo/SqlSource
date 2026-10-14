using System;
using System.Data;
using System.IO;
using System.Security.Cryptography;
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

    /// <summary>
    /// What is said of a connection string that sets <c>Multiplexing=true</c>: the driver then shares physical
    /// connections between commands, and a prepared statement is session state that a later command may not find.
    /// </summary>
    public const string MultiplexingRefused = "Multiplexing=true does not keep one session";

    /// <summary>
    /// What is said of a file that the connection string or the environment names and that cannot be read: it is
    /// not there, its folder is not, the tool may not open it, or its path is too long.  The driver's own text is
    /// not passed on: it quotes the path.
    /// </summary>
    public const string UnreadableFile = "a file that the connection string or the environment names cannot be read";

    /// <summary>
    /// What is said of a file that the connection string or the environment names and that the driver reads but
    /// cannot use: a certificate or a key that is not in a form it knows or whose password is wrong, a password
    /// file with a line that is not written as it wants.  The driver's own text is not passed on.
    /// </summary>
    public const string UnusableFile =
        "a file that the connection string or the environment names is not what Npgsql expects: "
        + "it is not in the right form, or its password is wrong";

    /// <summary>
    /// What is said of settings of the connection string that the driver cannot honour together, such as a
    /// <c>Target Session Attributes</c> other than <c>Any</c> for one host.
    /// </summary>
    public const string UnsupportedSettings =
        "Npgsql does not support these settings of the connection string together";

    /// <summary>
    /// What is said of a server that asks for GSS authentication on a machine where the operating system's GSS
    /// library cannot be loaded.
    /// </summary>
    public const string GssUnavailable = "GSS authentication is not available on this machine";

    // The type of the runtime whose static constructor loads the GSS library: Interop+NetSecurityNative+GssInitializer.
    private const string GssInitializer = "GssInitializer";

    private NpgsqlDataSource? _source;
    private NpgsqlConnection? _connection;
    private ILoggerFactory? _driverLog;

    /// <summary>The version of the driver, as <c>major.minor.patch</c>.</summary>
    public static string? DriverVersion => typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString(3);

    /// <summary>The session's connection, opened by the first call.</summary>
    /// <remarks>
    /// <para>
    /// What the driver (Npgsql 10.0.3, the tag <c>v10.0.3</c> of <c>npgsql/npgsql</c>, <c>src/Npgsql/</c>) throws
    /// for a keyword that names a file or a service, and whether it is an <see cref="NpgsqlException" />.  The
    /// driver's text quotes the path, so each type that a user can cause and that is none becomes an
    /// <see cref="NpgsqlException" /> with a fixed message, without the exception.  Each but the GSS case was provoked
    /// against a server of a test's own words, on <c>net8.0</c> and <c>net10.0</c>.  The environment names the
    /// same files (<c>PGSSLCERT</c>, <c>PGSSLKEY</c>, <c>PGPASSFILE</c>) and a default one is read when it exists,
    /// so a message says "the connection string or the environment".
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// <c>SslCertificate</c>, <c>SslKey</c>, <c>SslPassword</c>: <c>NpgsqlConnector.NegotiateEncryption</c> loads
    /// the client certificate once the server has answered <c>S</c> to the TLS request, before the handshake and
    /// outside any <c>try</c>.  A file that is not there is a <see cref="FileNotFoundException" />, a folder that
    /// is not, a <see cref="DirectoryNotFoundException" />, a folder where a file is read or a file that may not
    /// be read, an <see cref="UnauthorizedAccessException" />, and a file that holds no certificate or key, or a
    /// wrong <c>SslPassword</c>, a <see cref="CryptographicException" /> (a platform subclass of it for a
    /// damaged <c>.pfx</c> on macOS).  None is wrapped.  The container's PostgreSQL has no TLS, so only a server
    /// that agrees to it reaches them, and the unit tests have one.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>Passfile</c>: <c>NpgsqlConnector.GetPassword</c> reads the file when the server asks for a password and
    /// nothing else gave one: <see cref="FileNotFoundException" />, <see cref="DirectoryNotFoundException" />,
    /// <see cref="UnauthorizedAccessException" /> and <see cref="PathTooLongException" /> as above, and for a line
    /// that is not <c>host:port:database:user:password</c> a <see cref="FormatException" />
    /// (<c>PgPassFile.Entry.Parse</c>).  None is wrapped.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>Target Session Attributes</c> other than <c>Any</c> for one host, <c>Replication Mode</c> for several:
    /// the builder's <c>Build</c> throws a <see cref="NotSupportedException" />
    /// (<c>NpgsqlConnectionStringBuilder.PostProcessAndValidate</c>,
    /// <c>NpgsqlSlimDataSourceBuilder.ValidateMultiHost</c>).  <c>Multiplexing=true</c> is refused before
    /// that.  Other invalid settings are an <see cref="ArgumentException" />.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>RootCertificate</c>: read in the validation callback of the TLS handshake, and
    /// <c>NegotiateEncryption</c> wraps whatever it throws in an <see cref="NpgsqlException" /> whose message is
    /// <c>Exception while performing SSL handshake</c>, with the path in its inner exception alone.  It passes
    /// as it is.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>KerberosServiceName</c> and <c>GSS Encryption Mode</c>: <c>GSSEncrypt</c> wraps its failures in an
    /// <see cref="NpgsqlException" />, and <c>AuthenticateGSS</c> throws an <see cref="NpgsqlException" /> for a
    /// status other than success; neither text quotes the connection string.  What neither wraps is the
    /// <see cref="TypeInitializationException" /> of <c>Interop.NetSecurityNative.GssInitializer</c> when the
    /// operating system's GSS library cannot be loaded, which Npgsql's own comment in <c>GSSEncrypt</c> names and
    /// <c>AuthenticateGSS</c> does not catch: it is mapped by that type's name alone.  It is read and not
    /// provoked, since the machine that wrote this has GSS.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>Host</c> (a Unix socket or a name) and the other connect failures are wrapped by <c>Connect</c> in an
    /// <see cref="NpgsqlException" />.  A server that asks for an authentication method the driver does not know
    /// is a <see cref="NotSupportedException" /> that no setting causes, and it stays one.
    /// </description>
    /// </item>
    /// </list>
    /// <para>
    /// Broad types are not caught: an <see cref="IOException" /> that is none of these, or an
    /// <see cref="InvalidOperationException" />, is a bug and surfaces as one.  An <see cref="NpgsqlException" /> that
    /// quotes a path passes as it is: <c>docs/tech-debt/TD-0034</c>.
    /// </para>
    /// </remarks>
    /// <exception cref="NpgsqlException">
    /// The server could not be reached or refused, the driver cannot read the connection string, a file it names
    /// cannot be read or used, its settings are not supported together, the string sets <c>Multiplexing=true</c>, or
    /// the connection was open and is so no longer.
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
        catch (Exception exception)
            when (exception
                    is FileNotFoundException
                        or DirectoryNotFoundException
                        or UnauthorizedAccessException
                        or PathTooLongException
            )
        {
            // A certificate, a key or a password file that the string or the environment names.  The text quotes
            // the path, so the exception is not kept.
            throw new NpgsqlException(UnreadableFile);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            // The same files, read and not understood: a certificate or key in no form the driver knows, a wrong
            // SslPassword, a line of a password file that is no entry.
            throw new NpgsqlException(UnusableFile);
        }
        catch (TypeInitializationException exception)
            when (exception.TypeName.EndsWith(GssInitializer, StringComparison.Ordinal))
        {
            // The operating system's GSS library cannot be loaded, and the server asked for it.
            throw new NpgsqlException(GssUnavailable);
        }
    }

    // The builder reads the string, so its ArgumentException for one it cannot read is thrown here, inside the
    // caller's try.  Multiplexing is refused before anything is built or connected, whatever the keyword's case.
    private NpgsqlDataSource BuildSource()
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        if (builder.ConnectionStringBuilder.Multiplexing)
        {
            throw new NpgsqlException(MultiplexingRefused);
        }

        var driverLog = NpgsqlRunLogger.For(log);
        if (driverLog is not null)
        {
            _ = builder.UseLoggerFactory(driverLog);
        }

        // The factory is the connection's to dispose once it has a data source, and not before: a build that
        // throws leaves the field as it was, and the next call makes a factory of its own.
        NpgsqlDataSource source;
        try
        {
            source = builder.Build();
        }
        catch (NotSupportedException)
        {
            // Settings that the driver cannot honour together, which it finds when it builds.  The only
            // NotSupportedException of a build that no setting of the string causes is for a provider of passwords
            // or a callback, and the tool sets none.
            throw new NpgsqlException(UnsupportedSettings);
        }

        _driverLog = driverLog;
        return source;
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
