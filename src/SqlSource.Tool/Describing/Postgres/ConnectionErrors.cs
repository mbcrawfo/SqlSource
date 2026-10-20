using System;
using System.Linq;
using System.Net.Sockets;
using Npgsql;

namespace SqlSource.Tool.Describing.Postgres;

/// <summary>
/// What the driver threw, as the answer of a call holds it: <see cref="AtOpen" /> for <c>open</c>, and
/// <see cref="Of" /> for every other call.
/// </summary>
/// <remarks>
/// <para>
/// A session that could not be opened, and one that the server ended, keep the SQLSTATE and get a fixed text of the
/// describer's own, with no detail, no hint and no position.  The driver's or the server's text would name the host,
/// the port, the user or the database's own name, and it reaches the output, the log and a capture.  The name of a
/// <see cref="SocketError" /> and of an exception's type hold nothing of a connection.
/// </para>
/// <para>
/// The describer's own texts, <see cref="PostgresConnection.OwnTexts" />, pass as they are: an
/// <see cref="NpgsqlException" /> that is no <see cref="PostgresException" />, has no inner exception, and whose
/// message is one of them.  Any other error of a later call is the server's, as it said it.
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

    /// <summary>Any other server error at <c>open</c>.</summary>
    public const string SessionRefused = "the server refused to open a session";

    /// <summary>The driver's refusal at <c>open</c> of a server that asks for a password when none is given.</summary>
    public const string NoPassword = "the server asks for a password and the connection string gives none";

    /// <summary>A driver's error at <c>open</c> with a <see cref="TimeoutException" /> in its chain.</summary>
    public const string ConnectTimedOut = "the connection to the server timed out";

    /// <summary>
    /// A driver's error at <c>open</c> with a <see cref="SocketException" /> in its chain, followed by <c>: </c>
    /// and the name of its <see cref="SocketError" />.
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

    /// <summary>A driver's error at a later call with a <see cref="TimeoutException" /> in its chain.</summary>
    public const string NoAnswerInTime = "the server did not answer in time";

    // How Npgsql 10.0.3 begins its message when the server asks for a password and nothing gave one.  The rest of it
    // names the method of authentication, and it has no SQLSTATE and no inner exception.
    private const string NpgsqlNoPassword = "No password has been provided";

    // The SQLSTATEs of a later call, other than class 08, that say the session is over: an administrator, a crash
    // of another backend, a server that restarts, an idle-session timeout and an idle-in-transaction timeout.
    private static readonly string[] SessionEnding = ["57P01", "57P02", "57P04", "57P05", "25P03"];

    /// <summary>What <c>open</c> answers for what the driver threw.</summary>
    public static PostgresServerError AtOpen(NpgsqlException exception)
    {
        if (IsOwn(exception))
        {
            return Fixed(null, exception.Message);
        }

        if (exception is PostgresException server)
        {
            return Fixed(server.SqlState, AtOpenOf(server.SqlState));
        }

        if (exception.Message.StartsWith(NpgsqlNoPassword, StringComparison.Ordinal))
        {
            return Fixed(null, NoPassword);
        }

        if (InChain<TimeoutException>(exception) is not null)
        {
            return Fixed(null, ConnectTimedOut);
        }

        return InChain<SocketException>(exception) is { } socket
            ? Fixed(null, UnreachablePrefix + ": " + socket.SocketErrorCode.ToString())
            : Fixed(null, NotOpenedPrefix + " (" + Innermost(exception).GetType().Name + ")");
    }

    /// <summary>What a call after <c>open</c> answers for what the driver threw.</summary>
    public static PostgresServerError Of(NpgsqlException exception)
    {
        if (IsOwn(exception))
        {
            return Fixed(null, exception.Message);
        }

        if (exception is not PostgresException server)
        {
            return Fixed(
                null,
                InChain<TimeoutException>(exception) is not null ? NoAnswerInTime : PostgresConnection.LostConnection
            );
        }

        if (EndsTheSession(server.SqlState))
        {
            return Fixed(server.SqlState, SessionEnded);
        }

        int? position = server.Position > 0 ? server.Position : null;
        return new PostgresServerError(server.SqlState, server.MessageText, server.Detail, server.Hint, position);
    }

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
            _ => SessionRefused,
        };

    private static bool EndsTheSession(string sqlState) =>
        sqlState.StartsWith("08", StringComparison.Ordinal) || SessionEnding.Contains(sqlState, StringComparer.Ordinal);

    // A text the describer threw itself: PostgresConnection makes each with no inner exception.
    private static bool IsOwn(NpgsqlException exception) =>
        exception is not PostgresException
        && exception.InnerException is null
        && PostgresConnection.OwnTexts.Contains(exception.Message, StringComparer.Ordinal);

    private static T? InChain<T>(Exception exception)
        where T : Exception
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is T found)
            {
                return found;
            }
        }

        return null;
    }

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
