using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Security.Authentication;
using Microsoft.Data.SqlClient;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.SqlServer;

/// <summary>
/// What SqlClient threw, as the answer of a call holds it: <see cref="AtOpen" /> for <c>open</c>, and
/// <see cref="Of" /> for every other call; and the describer's own errors, <see cref="Own" />.
/// </summary>
/// <remarks>
/// A session that could not be opened, and a connection that was lost or timed out, keep each error's number, class
/// and state and get a fixed text of the describer's own.  SqlClient's and the server's texts name the host, the port,
/// the login or the database, and they reach the output, the log and a capture.  The name of a
/// <see cref="SocketError" /> holds nothing of a connection.
/// </remarks>
internal static class ConnectionErrors
{
    /// <summary>The error <c>18456</c> at <c>open</c>: a login or a password that the server refused.</summary>
    public const string LoginRefused = "the server refused the login";

    /// <summary>
    /// The errors <c>4060</c> and <c>4064</c> at <c>open</c>: a database that is not there, or that the login may not
    /// open.
    /// </summary>
    public const string DatabaseRefused = "the server cannot open the database for the login";

    /// <summary>
    /// An error at <c>open</c> with a <see cref="SocketException" /> in its chain, as
    /// <see cref="ConnectionTexts.Unreachable" /> writes it.
    /// </summary>
    public const string UnreachablePrefix = "SqlClient could not reach the server";

    /// <summary>
    /// A connection string that SqlClient cannot read.  SqlClient's own text is not passed on: it quotes a keyword,
    /// and for some keywords the value.
    /// </summary>
    public const string UnreadableConnectionString = "SqlClient cannot read the connection string";

    /// <summary>
    /// A connection string whose settings SqlClient reads one by one and does not accept together.  SqlClient's own
    /// text names the keywords, and is not passed on all the same.
    /// </summary>
    public const string ConflictingSettings =
        "SqlClient does not accept these settings of the connection string together";

    /// <summary>A connection string that sets nothing, such as <c>;</c>.</summary>
    public const string EmptyConnectionString = "the connection string is empty";

    /// <summary>
    /// A connection string whose <c>Authentication</c> is a method of Microsoft Entra ID, for which SqlClient has no
    /// provider: the tool does not carry <c>Microsoft.Data.SqlClient.Extensions.Azure</c>.
    /// </summary>
    public const string NoAuthenticationProvider =
        "SqlClient has no provider for the Authentication of the connection string";

    /// <summary>
    /// A connection string whose <c>Authentication</c> signs in with a user and a password, <c>Sql Password</c>,
    /// <c>Active Directory Password</c> or <c>Active Directory Service Principal</c>, and that lacks a
    /// <c>User ID</c> or a <c>Password</c>.
    /// </summary>
    public const string MissingCredentials =
        "the Authentication of the connection string needs both a User ID and a Password";

    /// <summary>
    /// A connection string that names a server SqlClient cannot connect to on this operating system, such as a LocalDB
    /// instance anywhere but on Windows.
    /// </summary>
    public const string UnsupportedOnThisPlatform = "SqlClient does not support this server on this platform";

    /// <summary>
    /// An error at <c>open</c> with an <see cref="AuthenticationException" /> in its chain: the TLS handshake failed,
    /// most often because the server's certificate is not trusted.
    /// </summary>
    public const string CertificateRefused = "the server's certificate is not trusted, or the TLS handshake failed";

    /// <summary>SqlClient's number of a connect or a command that timed out.</summary>
    public const int Timeout = -2;

    /// <summary>The class from which an error ends the connection.</summary>
    public const int FatalClass = 20;

    // The numbers of the other errors that open reads.
    private const int LoginFailed = 18456;
    private const int CannotOpenDatabase = 4060;
    private const int CannotOpenDefaultDatabase = 4064;

    /// <summary>What <c>open</c> answers for what SqlClient threw: each error's numbers, with a fixed text.</summary>
    public static SqlServerServerError AtOpen(SqlException exception)
    {
        var timedOut = ExceptionChain.Find<TimeoutException>(exception) is not null;
        var other = OtherAtOpen(exception);
        return RowsOf(
            exception,
            error =>
                error.Number switch
                {
                    LoginFailed => LoginRefused,
                    CannotOpenDatabase or CannotOpenDefaultDatabase => DatabaseRefused,
                    Timeout => ConnectionTexts.ConnectTimedOut,
                    _ when timedOut => ConnectionTexts.ConnectTimedOut,
                    _ => other,
                },
            ConnectionTexts.SessionRefused
        );
    }

    /// <summary>
    /// What a call after <c>open</c> answers for what SqlClient threw: a timeout and an error that ends the
    /// connection with a fixed text and their numbers, and every other error as the server said it.
    /// </summary>
    public static SqlServerServerError Of(SqlException exception) =>
        RowsOf(
            exception,
            static error =>
                error switch
                {
                    { Number: Timeout } => ConnectionTexts.NoAnswerInTime,
                    { Class: >= FatalClass } => ConnectionTexts.LostConnection,
                    _ => error.Message,
                },
            ConnectionTexts.LostConnection
        );

    // The text at open of an error that is no refused login, database or timeout: a TLS handshake that failed, a server
    // that could not be reached, or anything else.  An AuthenticationException comes inside a SqlException whose chain
    // holds no socket's error, so the order of the two is for clarity alone.
    private static string OtherAtOpen(SqlException exception)
    {
        if (ExceptionChain.Find<AuthenticationException>(exception) is not null)
        {
            return CertificateRefused;
        }

        return ExceptionChain.Find<SocketException>(exception) is { } socket
            ? ConnectionTexts.Unreachable(UnreachablePrefix, socket)
            : ConnectionTexts.SessionRefused;
    }

    /// <summary>An error of the describer's own: the text, and 0 for each number.</summary>
    public static SqlServerServerError Own(string text) => new([new SqlServerErrorRow(0, 0, 0, text)]);

    // Every error of the exception in its order, each with the text given for it.  An exception that holds no error,
    // which SqlClient does not throw, is the describer's own text.
    private static SqlServerServerError RowsOf(SqlException exception, Func<SqlError, string> textOf, string none)
    {
        var rows = new List<SqlServerErrorRow>(exception.Errors.Count);
        foreach (SqlError error in exception.Errors)
        {
            rows.Add(new SqlServerErrorRow(error.Number, error.Class, error.State, textOf(error)));
        }

        return rows.Count == 0 ? Own(none) : new SqlServerServerError(rows);
    }
}
