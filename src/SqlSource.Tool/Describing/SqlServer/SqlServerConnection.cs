using System;
using System.Collections.Generic;
using System.Data;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Data.SqlClient;

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
internal sealed class SqlServerConnection(string connectionString) : IAsyncDisposable
{
    private SqlConnection? _connection;

    /// <summary>
    /// The password of a connection string, as SqlClient reads it, or none when it gives none or cannot read it.
    /// </summary>
    public static IReadOnlyList<string> SecretsOf(string connection)
    {
        SqlConnectionStringBuilder builder;
        try
        {
            builder = [with(connection)];
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            // SqlClient's word for a string it cannot read, which opening the session reports in its own place.
            return [];
        }

        return builder.Password is { Length: > 0 } password ? [password] : [];
    }

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
    /// A keyword it does not know, a value not of its keyword's kind, a text that is no list of keywords and values:
    /// the <see cref="SqlConnectionStringBuilder" />'s constructor throws an <see cref="ArgumentException" />.  A
    /// <see cref="FormatException" /> and a <see cref="KeyNotFoundException" /> are taken the same way.  The answer
    /// is <see cref="ConnectionErrors.UnreadableConnectionString" />.
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
    /// answers.  Anything else, an <see cref="InvalidOperationException" /> included, is a bug: <c>SQLSRC200</c>.
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

        SqlConnectionStringBuilder builder;
        try
        {
            builder = [with(connectionString)];
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            throw new UnusableException(ConnectionErrors.UnreadableConnectionString);
        }

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
    /// SqlClient does not have.  See <see cref="OpenAsync" />.
    /// </summary>
    internal static string? OwnTextAtOpen(Exception exception, SqlConnectionStringBuilder builder) =>
        exception is ArgumentException
        && builder.Authentication is not (SqlAuthenticationMethod.NotSpecified or SqlAuthenticationMethod.SqlPassword)
        && SqlAuthenticationProvider.GetProvider(builder.Authentication) is null
            ? ConnectionErrors.NoAuthenticationProvider
            : null;

    // What SqlClient throws for a connection string it cannot read: a keyword it does not know, a value that is not
    // of the keyword's kind, a string that is no list of keywords and values.
    private static bool IsUnreadable(Exception exception) =>
        exception is ArgumentException or FormatException or KeyNotFoundException;

    public async ValueTask DisposeAsync()
    {
        if (_connection is not null)
        {
            await _connection.DisposeAsync();
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
