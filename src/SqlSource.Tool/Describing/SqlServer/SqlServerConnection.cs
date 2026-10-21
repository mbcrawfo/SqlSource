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
    /// SqlClient cannot read the connection string, or the connection was open and is so no longer.
    /// </exception>
    public async Task<SqlCommand> CommandAsync(string text, CancellationToken cancellationToken) =>
        new(text, await OpenAsync(cancellationToken));

    /// <summary>A command on the session's connection that calls a stored procedure.  Its caller disposes it.</summary>
    /// <exception cref="SqlException">The server could not be reached or refused.</exception>
    /// <exception cref="UnusableException">
    /// SqlClient cannot read the connection string, or the connection was open and is so no longer.
    /// </exception>
    public async Task<SqlCommand> ProcedureAsync(string name, CancellationToken cancellationToken) =>
        new(name, await OpenAsync(cancellationToken)) { CommandType = CommandType.StoredProcedure };

    // The session's connection, opened by the first call.  The value is read here, so that a string SqlClient cannot
    // read is the answer of the call that needed it, with the describer's own text: SqlClient's quotes the keyword, and
    // for some keywords the value.
    private async Task<SqlConnection> OpenAsync(CancellationToken cancellationToken)
    {
        if (_connection is not null)
        {
            return _connection.State == ConnectionState.Open
                ? _connection
                : throw new UnusableException(ConnectionErrors.LostConnection);
        }

        SqlConnection connection;
        try
        {
            connection = new SqlConnection(new SqlConnectionStringBuilder(connectionString).ConnectionString);
        }
        catch (Exception exception) when (IsUnreadable(exception))
        {
            throw new UnusableException(ConnectionErrors.UnreadableConnectionString);
        }

        try
        {
            await connection.OpenAsync(cancellationToken);
        }
        catch
        {
            await connection.DisposeAsync();
            throw;
        }

        _connection = connection;
        return connection;
    }

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
