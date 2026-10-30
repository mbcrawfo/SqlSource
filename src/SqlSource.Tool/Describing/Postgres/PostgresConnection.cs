using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Npgsql;
using SqlSource.Tool.Describing.Describers;
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
    // The type of the runtime whose static constructor loads the GSS library: Interop+NetSecurityNative+GssInitializer.
    // It is initialized by the static constructor of Interop+NetSecurityNative, so the exception that comes out is for
    // that type, with this one as its inner exception.
    private const string GssInitializer = "GssInitializer";

    /// <summary>The savepoint that a describe's transaction takes at its start.</summary>
    private const string DescribeSavepoint = "sqlsource_describe";

    private NpgsqlDataSource? _source;
    private NpgsqlConnection? _connection;
    private NpgsqlTransaction? _describe;
    private ILoggerFactory? _driverLog;

    /// <summary>
    /// The passwords of a connection string as the driver reads it, <c>Password</c> and <c>SSL Password</c>, and the
    /// one it would use when the string has no <c>Password</c>, or none when it cannot read the string.
    /// </summary>
    /// <remarks>
    /// Npgsql 10.0.3 (<c>NpgsqlConnector.GetPassword</c>) takes the password of the data source, which is the
    /// string's <c>Password</c> or that of a provider set with <c>NpgsqlDataSourceBuilder.UsePasswordProvider</c> or
    /// <c>UsePeriodicPasswordProvider</c>; then the connection's <c>ProvidePasswordCallback</c>; then
    /// <c>PGPASSWORD</c>; then the entry of a password file that matches the host, port, database and user.  The tool
    /// sets no provider and no callback.
    /// The file is the string's <c>Passfile</c>, else <c>PGPASSFILE</c>, else the default one, read only when it
    /// exists.  So <c>PGPASSWORD</c> is given when the string has no <c>Password</c>, and when it has neither the
    /// password of every entry of each file it could read is (<see cref="PasswordFile" />): which entry matches is
    /// the driver's to say, and a password too many only removes a word that nothing else prints.  A file that cannot
    /// be read for an I/O or access reason gives none, since the driver would fail to open the session in its own
    /// place; anything else it throws is a bug, which the run's guard turns into <c>SQLSRC200</c>.  The environment is
    /// the host's.
    /// </remarks>
    public static IReadOnlyList<string> SecretsOf(string connection, Func<string, string?> getEnvironmentVariable)
    {
        NpgsqlConnectionStringBuilder builder;
        try
        {
            builder = [with(connection)];
        }
        catch (ArgumentException)
        {
            // The driver's word for a string it cannot read, which opening the session reports in its own place.
            return [];
        }

        var secrets = new List<string>(new[] { builder.Password, builder.SslPassword }.OfType<string>());
        if (builder.Password is not null)
        {
            return secrets;
        }

        if (getEnvironmentVariable("PGPASSWORD") is { } fromEnvironment)
        {
            secrets.Add(fromEnvironment);
            return secrets;
        }

        foreach (var path in PasswordFile.PathsOf(builder.Passfile, getEnvironmentVariable))
        {
            secrets.AddRange(PasswordsIn(path));
        }

        return secrets;
    }

    /// <summary>
    /// The connection string as the driver writes it again, or null when it cannot read the string.  A text in an
    /// error of the driver's may be this one and not the string that was given.
    /// </summary>
    public static string? RewriteOf(string connection)
    {
        try
        {
            return new NpgsqlConnectionStringBuilder(connection).ConnectionString;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    // The password file is read as the driver reads it, whole, and a file it cannot read gives none.  So is one that
    // can be read only once, a named pipe or the pipe of a shell's process substitution, which is read ahead of the
    // driver here and would leave it nothing: an empty length, which such a pipe has until a reader opens it, is not
    // opened, and a stream that cannot seek is closed unread.
    private static IReadOnlyList<string> PasswordsIn(string path)
    {
        try
        {
            if (new FileInfo(path) is not { Exists: true, Length: > 0 })
            {
                return [];
            }

            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            if (!stream.CanSeek)
            {
                return [];
            }

            using var reader = new StreamReader(stream);
            return PasswordFile.Passwords(reader.ReadToEnd());
        }
        catch (Exception exception)
            when (exception is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return [];
        }
    }

    /// <summary>The version of the driver, as <c>major.minor.patch</c>.</summary>
    public static string? DriverVersion => typeof(NpgsqlConnection).Assembly.GetName().Version?.ToString(3);

    /// <summary>The session's connection, opened by the first call.</summary>
    /// <remarks>
    /// <para>
    /// What the driver (Npgsql 10.0.3, the tag <c>v10.0.3</c> of <c>npgsql/npgsql</c>, <c>src/Npgsql/</c>) throws for a
    /// keyword that names a file or a service, and whether it is an <see cref="NpgsqlException" />.  The driver's text
    /// quotes the path, so each type that a user can cause and that is none becomes an <see cref="UnusableException" />
    /// with a fixed text of <see cref="ConnectionErrors" />, without the exception.  Each but the GSS case was provoked
    /// against a server of a test's own words, on <c>net8.0</c> and <c>net10.0</c>.  The environment names the same
    /// files (<c>PGSSLCERT</c>, <c>PGSSLKEY</c>, <c>PGPASSFILE</c>) and a default one is read when it exists, so a
    /// message says "the connection string or the environment".
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
    /// <c>Exception while performing SSL handshake</c>, with the path in its inner exception alone.  It is thrown
    /// as it is, and <see cref="ConnectionErrors.AtOpen" /> answers it by the type of its innermost exception.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// <c>KerberosServiceName</c> and <c>GSS Encryption Mode</c>: <c>GSSEncrypt</c> wraps its failures in an
    /// <see cref="NpgsqlException" />, and <c>AuthenticateGSS</c> throws an <see cref="NpgsqlException" /> for a
    /// status other than success; neither text quotes the connection string.  What neither wraps is the
    /// <see cref="TypeInitializationException" /> that comes out when the operating system's GSS library cannot be
    /// loaded, which Npgsql's own comment in <c>GSSEncrypt</c> names and <c>AuthenticateGSS</c> does not catch.  In
    /// dotnet/runtime (<c>v8.0.0</c> and <c>v10.0.0</c> alike) <c>Interop.NetSecurityNative.GssInitializer</c> is run
    /// only by <c>NetSecurityNative</c>'s own static constructor
    /// (<c>Interop.NetSecurityNative.IsNtlmInstalled.cs</c>), so the exception is for <c>NetSecurityNative</c> with
    /// the one for <c>GssInitializer</c> inside it: <see cref="IsGssUnavailable" /> walks that chain.  It is read
    /// and not provoked, since the machine that wrote this has GSS; a unit test builds the chain.
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
    /// <see cref="InvalidOperationException" />, is a bug and surfaces as one.  An <see cref="NpgsqlException" /> of
    /// the driver's is thrown as it is, whatever it quotes, and no answer keeps its text
    /// (<see cref="ConnectionErrors" />).  A server that misbehaves can also reach the
    /// catch of <see cref="FormatException" />, through Npgsql's decode of a SCRAM salt or its parse of an OID while
    /// it loads the types, and is then reported with the file's message, <see cref="ConnectionErrors.UnusableFile" />.
    /// </para>
    /// </remarks>
    /// <exception cref="NpgsqlException">The server could not be reached or refused.</exception>
    /// <exception cref="UnusableException">
    /// The driver cannot read the connection string, a file it names cannot be read or used, its settings are not
    /// supported together, the string sets <c>Multiplexing=true</c>, the operating system's GSS library cannot be
    /// loaded, or the connection was open and is so no longer.
    /// </exception>
    public async Task<NpgsqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            // The driver closes a connection on which a call found that the server had gone, and for a command on
            // a closed one it throws what is no NpgsqlException.  A server causes it, so it is an answer's error
            // like any other.  The session is not opened again: what it had prepared is gone with its backend.
            return _connection.State == ConnectionState.Open
                ? _connection
                : throw new UnusableException(ConnectionTexts.LostConnection);
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
            throw new UnusableException(ConnectionErrors.UnreadableConnectionString);
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
            throw new UnusableException(ConnectionErrors.UnreadableFile);
        }
        catch (Exception exception) when (exception is CryptographicException or FormatException)
        {
            // The same files, read and not understood: a certificate or key in no form the driver knows, a wrong
            // SslPassword, a line of a password file that is no entry.
            throw new UnusableException(ConnectionErrors.UnusableFile);
        }
        catch (TypeInitializationException exception) when (IsGssUnavailable(exception))
        {
            // The operating system's GSS library cannot be loaded, and the server asked for it.
            throw new UnusableException(ConnectionErrors.GssUnavailable);
        }
    }

    /// <summary>
    /// Whether a type initializer failed because the operating system's GSS library cannot be loaded: the failed
    /// type, or one further in, is <c>Interop+NetSecurityNative+GssInitializer</c>.  The runtime runs it from the
    /// static constructor of <c>Interop+NetSecurityNative</c>, so the first call into that type throws a
    /// <see cref="TypeInitializationException" /> for <c>NetSecurityNative</c> whose inner exception is the one for
    /// <c>GssInitializer</c>; a later call rethrows the same.
    /// </summary>
    internal static bool IsGssUnavailable(TypeInitializationException exception)
    {
        for (
            Exception? current = exception;
            current is TypeInitializationException failed;
            current = failed.InnerException
        )
        {
            if (failed.TypeName.EndsWith(GssInitializer, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    // The builder reads the string, so its ArgumentException for one it cannot read is thrown here, inside the
    // caller's try.  Multiplexing is refused before anything is built or connected, whatever the keyword's case.
    private NpgsqlDataSource BuildSource()
    {
        var builder = new NpgsqlDataSourceBuilder(connectionString);
        if (builder.ConnectionStringBuilder.Multiplexing)
        {
            throw new UnusableException(ConnectionErrors.MultiplexingRefused);
        }

        // Npgsql logs the values of a command's parameters when the string asks it to, whatever the data source's own
        // setting; the driver's log holds each command's text and none of its parameters.
        if (builder.ConnectionStringBuilder.LogParameters)
        {
            builder.ConnectionStringBuilder.LogParameters = false;
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
            throw new UnusableException(ConnectionErrors.UnsupportedSettings);
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

    /// <summary>Whether a describe's transaction is held: from <see cref="BeginDescribeAsync" /> to its end.</summary>
    public bool InDescribe => _describe is not null;

    /// <summary>
    /// Begins the transaction of a describe, which every call of the describe runs in, and takes the savepoint
    /// <see cref="DescribeSavepoint" /> at its start.
    /// </summary>
    /// <remarks>
    /// <para>
    /// A pooler in transaction mode keeps a transaction on one backend, so every call of the describe reaches the
    /// statement it prepared.  A prepared statement is session state and survives a rollback, so <c>deallocate</c>
    /// frees the statement inside the transaction, on its backend, before the end.
    /// </para>
    /// <para>
    /// A rollback to a savepoint gives up the locks taken since it, so <see cref="KeepPreparedAsync" /> moves the
    /// savepoint after the <c>PREPARE</c>: the transaction then keeps the locks that preparing took until it ends, and
    /// another session's <c>DROP</c> or <c>ALTER</c> of a relation the query reads waits for the describe.  Npgsql
    /// sends the <c>BEGIN</c> with the <c>SAVEPOINT</c>, in one round trip, here and not with the <c>PREPARE</c>, so
    /// that a server that refuses a transaction, as PgBouncer in statement mode does, refuses it at this call.  When
    /// either fails, the transaction is disposed and none is held.
    /// </para>
    /// </remarks>
    /// <exception cref="InvalidOperationException">A describe's transaction is already held.</exception>
    /// <exception cref="NpgsqlException">The server refused the transaction, or the connection is lost.</exception>
    public async Task BeginDescribeAsync(CancellationToken cancellationToken)
    {
        if (_describe is not null)
        {
            throw new InvalidOperationException("A describe's transaction is already held.");
        }

        var connection = await OpenAsync(cancellationToken);
        var transaction = await connection.BeginTransactionAsync(cancellationToken);
        try
        {
            // A command of its own, and not NpgsqlTransaction.SaveAsync, which Npgsql only sends with the next
            // command: a server that refuses the transaction refuses it here, before the PREPARE.
            await using var save = new NpgsqlCommand($"SAVEPOINT {DescribeSavepoint}", connection);
            _ = await save.ExecuteNonQueryAsync(cancellationToken);
        }
        catch
        {
            await transaction.DisposeAsync();
            throw;
        }

        _describe = transaction;
    }

    /// <summary>
    /// Moves the describe's savepoint after the <c>PREPARE</c>: <c>RELEASE SAVEPOINT</c>, which gives the locks taken
    /// since it to the transaction, then <c>SAVEPOINT</c> again, in one round trip.  A later rollback to the savepoint
    /// keeps the locks that preparing took.
    /// </summary>
    /// <exception cref="InvalidOperationException">No describe's transaction is held.</exception>
    /// <exception cref="NpgsqlException">The connection is lost.</exception>
    public async Task KeepPreparedAsync(CancellationToken cancellationToken)
    {
        if (_describe is null)
        {
            throw new InvalidOperationException("No describe's transaction is held to keep the PREPARE in.");
        }

        await using var move = await CommandAsync(
            $"RELEASE SAVEPOINT {DescribeSavepoint}; SAVEPOINT {DescribeSavepoint}",
            cancellationToken
        );
        _ = await move.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// Rolls the describe's transaction back to its savepoint: a transaction that an error aborted takes commands
    /// again, and every <c>SET LOCAL</c> since the savepoint ends.  The statement the describe prepared stays.
    /// </summary>
    /// <exception cref="InvalidOperationException">No describe's transaction is held.</exception>
    /// <exception cref="NpgsqlException">The connection is lost.</exception>
    public async Task RestoreDescribeAsync(CancellationToken cancellationToken)
    {
        var transaction =
            _describe ?? throw new InvalidOperationException("No describe's transaction is held to restore.");

        // Npgsql disposes the transaction of a connection that broke: the lost connection is the answer.
        _ = await OpenAsync(cancellationToken);
        await transaction.RollbackAsync(DescribeSavepoint, cancellationToken);
    }

    /// <summary>
    /// Ends the describe's transaction, when one is held, by disposing it: Npgsql rolls it back, and on a connection
    /// that was lost it sends nothing and throws nothing.  Afterwards none is held.
    /// </summary>
    public async ValueTask EndDescribeAsync()
    {
        if (_describe is { } transaction)
        {
            _describe = null;
            await transaction.DisposeAsync();
        }
    }

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

    /// <summary>
    /// Why the connection cannot be used, in the describer's own words: one of the texts of
    /// <see cref="ConnectionErrors" /> or <see cref="ConnectionTexts" />, which <c>PostgresLive.AnswerAsync</c> answers
    /// as it is, through <see cref="ConnectionErrors.Own" />.  Every error the describer throws of its own is this.
    /// </summary>
    /// <remarks>
    /// It derives from <see cref="NpgsqlException" /> so that every catch of the driver's errors, which ends or
    /// restores the describe's transaction, takes it too.  It has the standard constructors for the reasons
    /// <c>DescriberFaultException</c> gives: S3871, CA1515 and CA1032.  It holds no inner exception.
    /// </remarks>
    internal sealed class UnusableException : NpgsqlException
    {
        public UnusableException()
            : base(ConnectionTexts.LostConnection) { }

        public UnusableException(string message)
            : base(message) { }

        public UnusableException(string message, Exception innerException)
            : base(message)
        {
            // Dropped, as the class says: the driver's text may quote the value or a path.
            _ = innerException;
        }
    }
}
