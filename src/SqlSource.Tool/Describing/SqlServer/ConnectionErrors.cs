using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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

    /// <summary>
    /// Whether the error ended the session: one of its errors is of <see cref="FatalClass" /> or above, or it has none,
    /// which <see cref="Of" /> answers as the lost connection too.
    /// </summary>
    public static bool EndsTheSession(SqlException exception) =>
        exception.Errors.Count == 0 || exception.Errors.Cast<SqlError>().Any(static error => error.Class >= FatalClass);

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
                    // WAIT_TIMEOUT, which SqlClient gives for a connect that ran out of time, and for a port where
                    // nothing listens on Windows and on some attempts on macOS, since it tries again until the timeout
                    // ends there.
                    Timeout or WaitTimeout => ConnectionTexts.ConnectTimedOut,
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
    // holds no socket's error, so the order of the two is for clarity alone.  On Windows SqlClient's native network
    // layer throws neither, and its numbers are read first.
    private static string OtherAtOpen(SqlException exception)
    {
        if (
            OperatingSystem.IsWindows()
            && NativeLayerText(exception.Number, ExceptionChain.Find<Win32Exception>(exception)?.NativeErrorCode)
                is { } native
        )
        {
            return native;
        }

        if (ExceptionChain.Find<AuthenticationException>(exception) is not null)
        {
            return CertificateRefused;
        }

        return ExceptionChain.Find<SocketException>(exception) is { } socket
            ? ConnectionTexts.Unreachable(UnreachablePrefix, socket)
            : ConnectionTexts.SessionRefused;
    }

    /// <summary>
    /// The text at <c>open</c> of an error of SqlClient's native network layer, which it uses on Windows, from the
    /// error's number and the code of the <see cref="Win32Exception" /> in its chain: the text the managed layer of
    /// Linux and macOS gets for the same failure, or null for an error the layer does not give.
    /// </summary>
    /// <remarks>
    /// The codes are those of <c>winerror.h</c>.  <c>WAIT_TIMEOUT</c> is a connect that ran out of time, and also a
    /// port where nothing listens, since the native layer retries a refused connection until the connect timeout ends:
    /// a probe of SqlClient 7.1.1 gave it for both.  A Winsock code is the <see cref="SocketError" /> of the same
    /// number.
    /// </remarks>
    internal static string? NativeLayerText(int errorNumber, int? win32Code)
    {
        if (errorNumber == WaitTimeout || win32Code == WaitTimeout)
        {
            return ConnectionTexts.ConnectTimedOut;
        }

        if (
            HandshakeFailures.Contains(errorNumber)
            || (win32Code is { } handshake && HandshakeFailures.Contains(handshake))
        )
        {
            return CertificateRefused;
        }

        return win32Code is >= FirstWinsockCode and <= LastWinsockCode && Enum.IsDefined((SocketError)win32Code.Value)
            ? UnreachablePrefix + ": " + (SocketError)win32Code.Value
            : null;
    }

    // WAIT_TIMEOUT, and the range of the Winsock codes, WSABASEERR to the last of the WSA errors.
    private const int WaitTimeout = 258;
    private const int FirstWinsockCode = 10000;
    private const int LastWinsockCode = 11999;

    // The SSPI and certificate codes of a TLS handshake that failed, each by its name in winerror.h.  Not the whole
    // facility of SSPI, whose codes of a login that failed (SEC_E_LOGON_DENIED, SEC_E_TARGET_UNKNOWN) are no
    // handshake's.
    private static readonly HashSet<int> HandshakeFailures =
    [
        unchecked((int)0x80090325), // SEC_E_UNTRUSTED_ROOT
        unchecked((int)0x80090322), // SEC_E_WRONG_PRINCIPAL
        unchecked((int)0x80090328), // SEC_E_CERT_EXPIRED
        unchecked((int)0x80090326), // SEC_E_ILLEGAL_MESSAGE
        unchecked((int)0x80090308), // SEC_E_INVALID_TOKEN: an answer that is no TLS, as from a service of another kind
        unchecked((int)0x80090331), // SEC_E_ALGORITHM_MISMATCH: no version or cipher both ends accept
        unchecked((int)0x80090327), // SEC_E_CERT_UNKNOWN
        unchecked((int)0x800B0109), // CERT_E_UNTRUSTEDROOT
        unchecked((int)0x800B0101), // CERT_E_EXPIRED
        unchecked((int)0x800B010F), // CERT_E_CN_NO_MATCH
        unchecked((int)0x800B010A), // CERT_E_CHAINING
        unchecked((int)0x800B0112), // CERT_E_UNTRUSTEDCA
        unchecked((int)0x800B010E), // CERT_E_REVOCATION_FAILURE
        unchecked((int)0x80092010), // CRYPT_E_REVOKED
        unchecked((int)0x80092013), // CRYPT_E_REVOCATION_OFFLINE
    ];

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
