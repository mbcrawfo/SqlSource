using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;
using SqlSource.Tool.Logging;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// The one thing of the describer that holds the connection's value, and the one place that makes a connection and a
/// command.  Nothing here runs until a <c>live</c> function asks for it, so a replayed session touches no driver.
/// </summary>
/// <remarks>
/// <para>
/// The session has one <see cref="SqlConnection" />, opened by the first call that needs it.  A call that finds it no
/// longer open, which SqlClient does after an error that ends the connection, makes that call and every later one
/// answer <see cref="ConnectionErrors.LostConnection" />: it is not opened again.
/// </para>
/// <para>
/// A command's text is not a constant here, which is what <c>CA2100</c> flags: <c>.editorconfig</c> turns the rule
/// off for this file and says why.
/// </para>
/// </remarks>
/// <param name="connectionString">The connection's value.  It goes to the driver and to nothing else.</param>
/// <param name="log">
/// The describer's log.  When it keeps anything, SqlClient's own events go there from when this is made, before the
/// first call opens the connection, until it is disposed: for the session's life, or for that of a session that
/// failed to open (<see cref="SqlClientRunListener" />).
/// </param>
internal sealed class SqlServerConnection(string connectionString, IRunLog log) : IAsyncDisposable
{
    private readonly SqlClientRunListener? _listener = SqlClientRunListener.For(log);
    private SqlConnection? _connection;

    /// <summary>
    /// The password of a connection string, as SqlClient reads it, or none when it gives none or cannot read it.
    /// </summary>
    public static IReadOnlyList<string> SecretsOf(string connection) =>
        Read(connection)?.Password is { Length: > 0 } password ? [password] : [];

    /// <summary>The version of the driver, its informational version without the commit after <c>+</c>.</summary>
    public static string? DriverVersion { get; } =
        typeof(SqlConnection).Assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            is { } version
            ? version.Split('+')[0]
            : null;

    /// <summary>A text command on the session's connection.  Its caller disposes it.</summary>
    /// <exception cref="SqlException">The server could not be reached or refused.</exception>
    /// <exception cref="UnusableException">
    /// The connection string is one the describer refuses (<see cref="OpenAsync" />), or the connection was open and
    /// is so no longer.
    /// </exception>
    public async Task<SqlCommand> CommandAsync(string text, CancellationToken cancellationToken) =>
        new(text, await OpenAsync(cancellationToken));

    /// <summary>A command on the session's connection that calls a stored procedure.  Its caller disposes it.</summary>
    /// <exception cref="SqlException">The server could not be reached or refused.</exception>
    /// <exception cref="UnusableException">
    /// The connection string is one the describer refuses (<see cref="OpenAsync" />), or the connection was open and
    /// is so no longer.
    /// </exception>
    public async Task<SqlCommand> ProcedureAsync(string name, CancellationToken cancellationToken) =>
        new(name, await OpenAsync(cancellationToken)) { CommandType = CommandType.StoredProcedure };

    /// <summary>The session's connection, opened by the first call.</summary>
    /// <remarks>
    /// <para>
    /// The value is read here, so that what a user can write wrong is the answer of the call that needed the
    /// connection, with the describer's own text and never SqlClient's, which quotes a keyword and for some keywords
    /// the value.  What SqlClient 7.1.1 throws for what a user can write, and what the describer makes of it, each
    /// provoked on <c>net10.0</c> unless it says otherwise:
    /// </para>
    /// <list type="bullet">
    /// <item>
    /// <description>
    /// A value that the <see cref="SqlConnectionStringBuilder" />'s constructor refuses.  It throws an
    /// <see cref="ArgumentException" /> for a keyword it does not know, a value not of its keyword's kind or out of its
    /// range, and a text that is no list of keywords and values; an <see cref="OverflowException" /> for a number too
    /// large for an <see cref="int" /> (<c>Connect Timeout=99999999999</c>); an
    /// <see cref="InvalidOperationException" /> for <c>Context Connection=true</c>; and a
    /// <see cref="NotSupportedException" /> for <c>Network Library</c>.  The constructor reads the text and nothing
    /// else, so any exception it throws is taken the same way, by <see cref="SecretsOf" /> too, which never throws.
    /// The answer is <see cref="ConnectionErrors.UnreadableConnectionString" />.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// Settings that the builder reads one by one and SqlClient does not accept together, such as an
    /// <c>Authentication</c> of <c>Active Directory Integrated</c> with a <c>Password</c>, or
    /// <c>Integrated Security</c> with any <c>Authentication</c>: the <see cref="SqlConnection" />'s constructor
    /// throws an <see cref="ArgumentException" /> as it reads the value, before anything is opened.  The answer is
    /// <see cref="ConnectionErrors.ConflictingSettings" />.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// A value that sets nothing, <c>;</c> or white space: the builder reads it, and <c>SqlConnection.OpenAsync</c>
    /// throws an <see cref="InvalidOperationException" />, "The ConnectionString property has not been initialized"
    /// (<c>SqlConnection.PermissionDemand</c>).  It is refused before anything is opened, as
    /// <see cref="ConnectionErrors.EmptyConnectionString" />.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// An <c>Authentication</c> that signs in with a user and a password, <c>Sql Password</c>,
    /// <c>Active Directory Password</c> or <c>Active Directory Service Principal</c>, in a value that lacks a
    /// <c>User ID</c> or a <c>Password</c>, or gives one empty: <c>SqlConnection.OpenAsync</c> throws an
    /// <see cref="InvalidOperationException" />, "Either Credential or both 'User ID' and 'Password' ... must be
    /// specified", before it reaches the server.  It is refused before anything is opened
    /// (<see cref="LacksCredentials" />), as <see cref="ConnectionErrors.MissingCredentials" />.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// A server that SqlClient cannot connect to on this operating system: a LocalDB instance,
    /// <c>Data Source=(localdb)\MSSQLLocalDB</c>, anywhere but on Windows, for which <c>SqlConnection.OpenAsync</c>
    /// throws a <see cref="PlatformNotSupportedException" />.  Only <c>OpenAsync</c>'s is caught, by its type, and the
    /// answer is <see cref="ConnectionErrors.UnsupportedOnThisPlatform" />.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// An <c>Authentication</c> of Microsoft Entra ID (<c>Active Directory ...</c>) without the package
    /// <c>Microsoft.Data.SqlClient.Extensions.Azure</c>, which the tool does not carry: when the server asks for a
    /// federated token, <c>SqlConnectionInternal.GetFedAuthToken</c> finds no provider and throws the
    /// <see cref="ArgumentException" /> of <c>SQL.CannotFindAuthProvider</c>, inside <c>OpenAsync</c>.  Only Azure asks
    /// for one, so this was read in the assembly and not provoked; a SQL Server that does not answers the login as
    /// <c>18456</c>, which was.  The exception is caught only when the value names such a method and
    /// <see cref="SqlAuthenticationProvider.GetProvider" /> has none for it, and the answer is
    /// <see cref="ConnectionErrors.NoAuthenticationProvider" />.  Any other <see cref="ArgumentException" /> of
    /// <c>OpenAsync</c> is a bug and stays one.
    /// </description>
    /// </item>
    /// <item>
    /// <description>
    /// A server that cannot be reached, a login or a database it refuses, a timeout, and a certificate it presents
    /// that is not trusted are each a <see cref="SqlException" />, which <see cref="ConnectionErrors.AtOpen" />
    /// answers.  Anything else that <c>OpenAsync</c> throws, an <see cref="InvalidOperationException" /> other than
    /// that of the missing credentials above, which is refused before it can be thrown, is a bug: <c>SQLSRC200</c>.
    /// </description>
    /// </item>
    /// </list>
    /// </remarks>
    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection.State == ConnectionState.Open
                ? _connection
                : throw new UnusableException(ConnectionErrors.LostConnection);
        }

        var builder =
            Read(connectionString) ?? throw new UnusableException(ConnectionErrors.UnreadableConnectionString);
        if (builder.ConnectionString.Length == 0)
        {
            throw new UnusableException(ConnectionErrors.EmptyConnectionString);
        }

        SqlConnection connection;
        try
        {
            connection = new SqlConnection(builder.ConnectionString);
        }
        catch (ArgumentException)
        {
            throw new UnusableException(ConnectionErrors.ConflictingSettings);
        }

        // Checked after the constructor, so that settings SqlClient refuses together are reported first, as it does.
        if (LacksCredentials(builder))
        {
            await connection.DisposeAsync();
            throw new UnusableException(ConnectionErrors.MissingCredentials);
        }

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch (Exception exception) when (OwnTextAtOpen(exception, builder) is { } text)
        {
            await connection.DisposeAsync();
            throw new UnusableException(text);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        _connection = connection;
        return connection;
    }

    /// <summary>
    /// The describer's own text for what <c>SqlConnection.OpenAsync</c> threw for a value it read, or null for
    /// anything else: an <see cref="ArgumentException" /> for a value whose <c>Authentication</c> needs a provider that
    /// SqlClient does not have, and a <see cref="PlatformNotSupportedException" />.  See <see cref="OpenAsync" />.
    /// </summary>
    internal static string? OwnTextAtOpen(Exception exception, SqlConnectionStringBuilder builder) =>
        exception switch
        {
            PlatformNotSupportedException => ConnectionErrors.UnsupportedOnThisPlatform,
            ArgumentException
                when builder.Authentication
                    is not (SqlAuthenticationMethod.NotSpecified or SqlAuthenticationMethod.SqlPassword)
                    && SqlAuthenticationProvider.GetProvider(builder.Authentication) is null =>
                ConnectionErrors.NoAuthenticationProvider,
            _ => null,
        };

    /// <summary>
    /// Whether the value's <c>Authentication</c> signs in with a user and a password and the value lacks either, which
    /// <c>SqlConnection.OpenAsync</c> refuses with an <see cref="InvalidOperationException" /> that names the method.
    /// See <see cref="OpenAsync" />.
    /// </summary>
    internal static bool LacksCredentials(SqlConnectionStringBuilder builder) =>
#pragma warning disable CS0618 // SqlClient marks ActiveDirectoryPassword obsolete, and still reads and refuses it.
        builder.Authentication
            is SqlAuthenticationMethod.SqlPassword
                or SqlAuthenticationMethod.ActiveDirectoryPassword
                or SqlAuthenticationMethod.ActiveDirectoryServicePrincipal
#pragma warning restore CS0618
        && (builder.UserID.Length == 0 || builder.Password.Length == 0);

    // The value as SqlClient reads it, or null when SqlClient cannot read it.  The builder's constructor reads the text
    // and nothing else, so whatever it throws is about the value, as the remarks of OpenAsync list, but for an
    // OutOfMemoryException, which is the process's.
    private static SqlConnectionStringBuilder? Read(string connection)
    {
        try
        {
            return [with(connection)];
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            return null;
        }
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
            _listener?.Dispose();
        }
    }

    /// <summary>The connection without its value.</summary>
    public override string ToString() => nameof(SqlServerConnection);

    /// <summary>
    /// Why the connection cannot be used, in the describer's own words: one of the texts of
    /// <see cref="ConnectionErrors" />, which a <c>live</c> function answers through
    /// <see cref="ConnectionErrors.Own" />.  SqlClient's <see cref="SqlException" /> has no public constructor, so the
    /// describer's own refusals are this.
    /// </summary>
    /// <remarks>
    /// It derives from <see cref="InvalidOperationException" /> and has the standard constructors for the reasons
    /// <c>DescriberFaultException</c> gives: S3871, CA1515 and CA1032.  It holds no inner exception.
    /// </remarks>
    internal sealed class UnusableException : InvalidOperationException
    {
        public UnusableException()
            : base(ConnectionErrors.LostConnection) { }

        public UnusableException(string message)
            : base(message) { }

        public UnusableException(string message, Exception innerException)
            : base(message)
        {
            // Dropped, as the class says: SqlClient's text may quote the value.
            _ = innerException;
        }
    }
}
