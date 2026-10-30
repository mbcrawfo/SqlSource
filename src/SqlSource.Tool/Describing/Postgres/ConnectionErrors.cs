using System;
using System.Linq;
using System.Net.Sockets;
using Npgsql;
using SqlSource.Tool.Describing.Describers;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What the driver threw, as the answer of a call holds it: <see cref="AtOpen" /> for <c>open</c>, and
/// <see cref="Of" /> for every other call; and the describer's own errors, <see cref="Own" />.
/// </summary>
/// <remarks>
/// <para>
/// A session that could not be opened, and one that the server ended, keep the SQLSTATE and get a fixed text of the
/// describer's own, with no detail, no hint and no position.  The driver's or the server's text would name the host,
/// the port, the user or the database's own name, and it reaches the output, the log and a capture.  The name of a
/// <see cref="SocketError" /> and of an exception's type hold nothing of a connection.
/// </para>
/// <para>
/// The describer's own texts are thrown as a <see cref="PostgresConnection.UnusableException" />, which
/// <c>PostgresLive.AnswerAsync</c> answers through <see cref="Own" /> before it reaches either.  Any other error of a
/// later call is the server's, as it said it.
/// </para>
/// </remarks>
internal static class ConnectionErrors
{
    /// <summary>A server error <c>28P01</c> at <c>open</c>.</summary>
    public const string PasswordRefused = "the server refused the password";

    /// <summary>A server error of class <c>28</c> other than <c>28P01</c> at <c>open</c>.</summary>
    public const string AuthenticationRefused = "the server refused to authenticate the user";

    /// <summary>A server error <c>3D000</c> at <c>open</c>.</summary>
    public const string NoSuchDatabase = "the server has no such database";

    /// <summary>A server error <c>42501</c> at <c>open</c>.</summary>
    public const string ConnectNotAllowed = "the user may not connect to the database";

    /// <summary>A server error <c>53300</c> at <c>open</c>.</summary>
    public const string TooManyConnections = "the server has too many connections";

    /// <summary>A server error <c>57P03</c> at <c>open</c>.</summary>
    public const string NotAcceptingConnections = "the server is not accepting connections now";

    /// <summary>A server error of class <c>08</c> at <c>open</c>.</summary>
    public const string ConnectionRefused = "the server refused the connection";

    /// <summary>The driver's refusal at <c>open</c> of a server that asks for a password when none is given.</summary>
    public const string NoPassword = "the server asks for a password and the connection string gives none";

    /// <summary>
    /// A driver's error at <c>open</c> with a <see cref="SocketException" /> in its chain, as
    /// <see cref="ConnectionTexts.Unreachable" /> writes it.
    /// </summary>
    public const string UnreachablePrefix = "Npgsql could not reach the server";

    /// <summary>
    /// Any other driver's error at <c>open</c>, followed by the type of the innermost exception in parentheses.
    /// </summary>
    public const string NotOpenedPrefix = "Npgsql could not open a connection to the server";

    /// <summary>
    /// A server error at a later call that ends the session: of class <c>08</c>, or <c>57P01</c>, <c>57P02</c>,
    /// <c>57P04</c>, <c>57P05</c> or <c>25P03</c>.
    /// </summary>
    public const string SessionEnded = "the server ended the session";

    /// <summary>
    /// What is said of a connection string the driver cannot read.  The driver's own text is not passed on: its
    /// inner exception quotes the value.
    /// </summary>
    public const string UnreadableConnectionString = "Npgsql cannot read the connection string";

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

    /// <summary>
    /// What is said of a session whose answer to <c>open</c> Npgsql cannot read as text: the driver loaded the
    /// database's types on open with SQL that the search path changes, and an operator <c>=</c> of a user's on two
    /// <c>oid</c> or two <c>"char"</c> before <c>pg_catalog</c> leaves it without them.
    /// </summary>
    public const string UnreadableTypes = "Npgsql cannot read the database's types";

    // How Npgsql 10.0.3 begins its message when the server asks for a password and nothing gave one.  The rest of it
    // names the method of authentication, and it has no SQLSTATE and no inner exception.
    private const string NpgsqlNoPassword = "No password has been provided";

    // The SQLSTATEs of a later call, other than class 08, that say the session is over: an administrator, a crash
    // of another backend, a server that restarts, an idle-session timeout and an idle-in-transaction timeout.
    private static readonly string[] SessionEnding = ["57P01", "57P02", "57P04", "57P05", "25P03"];

    /// <summary>What <c>open</c> answers for what the driver threw.</summary>
    public static PostgresServerError AtOpen(NpgsqlException exception)
    {
        if (exception is PostgresException server)
        {
            return Fixed(server.SqlState, AtOpenOf(server.SqlState));
        }

        if (exception.Message.StartsWith(NpgsqlNoPassword, StringComparison.Ordinal))
        {
            return Fixed(null, NoPassword);
        }

        if (ExceptionChain.Find<TimeoutException>(exception) is not null)
        {
            return Fixed(null, ConnectionTexts.ConnectTimedOut);
        }

        return ExceptionChain.Find<SocketException>(exception) is { } socket
            ? Fixed(null, ConnectionTexts.Unreachable(UnreachablePrefix, socket))
            : Fixed(null, NotOpenedPrefix + " (" + Innermost(exception).GetType().Name + ")");
    }

    /// <summary>What a call after <c>open</c> answers for what the driver threw.</summary>
    public static PostgresServerError Of(NpgsqlException exception)
    {
        if (exception is not PostgresException server)
        {
            return Fixed(
                null,
                ExceptionChain.Find<TimeoutException>(exception) is not null
                    ? ConnectionTexts.NoAnswerInTime
                    : ConnectionTexts.LostConnection
            );
        }

        if (EndsTheSession(server.SqlState))
        {
            return Fixed(server.SqlState, SessionEnded);
        }

        int? position = server.Position > 0 ? server.Position : null;
        return new PostgresServerError(server.SqlState, server.MessageText, server.Detail, server.Hint, position);
    }

    /// <summary>An error of the describer's own: the text, with no SQLSTATE, detail, hint or position.</summary>
    public static PostgresServerError Own(string text) => Fixed(null, text);

    private static string AtOpenOf(string sqlState) =>
        sqlState switch
        {
            "28P01" => PasswordRefused,
            _ when sqlState.StartsWith("28", StringComparison.Ordinal) => AuthenticationRefused,
            "3D000" => NoSuchDatabase,
            "42501" => ConnectNotAllowed,
            "53300" => TooManyConnections,
            "57P03" => NotAcceptingConnections,
            _ when sqlState.StartsWith("08", StringComparison.Ordinal) => ConnectionRefused,
            _ => ConnectionTexts.SessionRefused,
        };

    private static bool EndsTheSession(string sqlState) =>
        sqlState.StartsWith("08", StringComparison.Ordinal) || SessionEnding.Contains(sqlState, StringComparer.Ordinal);

    private static Exception Innermost(Exception exception)
    {
        var current = exception;
        while (current.InnerException is { } inner)
        {
            current = inner;
        }

        return current;
    }

    private static PostgresServerError Fixed(string? sqlState, string message) =>
        new(sqlState, message, null, null, null);
}
