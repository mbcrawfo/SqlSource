using System.Net.Sockets;

namespace SqlSource.Tool.Describing.Describers;

/// <summary>
/// The fixed texts of a connection that both describers give in the driver's or the server's place, and the text of
/// a server that could not be reached.  Each engine's <c>ConnectionErrors</c> says when it gives each.
/// </summary>
internal static class ConnectionTexts
{
    /// <summary>An error at <c>open</c> that no text of the engine's own names.</summary>
    public const string SessionRefused = "the server refused to open a session";

    /// <summary>An error at <c>open</c> for a connection that timed out.</summary>
    public const string ConnectTimedOut = "the connection to the server timed out";

    /// <summary>An error at a later call for a command that timed out.</summary>
    public const string NoAnswerInTime = "the server did not answer in time";

    /// <summary>
    /// A call on a connection that the server or the driver has ended, and every call after it: the connection is not
    /// opened again.
    /// </summary>
    public const string LostConnection = "the connection to the server was lost";

    /// <summary>
    /// The text of a server that could not be reached: <paramref name="prefix" />, then <c>: </c> and the name of the
    /// socket's <see cref="SocketError" /> when that names a reason.  <see cref="SocketError.Success" /> names none, as
    /// SqlClient 7.1.1 on Linux gives it, so the prefix then stands alone.
    /// </summary>
    /// <param name="prefix">The engine's text, which names its driver.</param>
    /// <param name="socket">The socket's exception that the driver's chain holds.</param>
    public static string Unreachable(string prefix, SocketException socket) =>
        socket.SocketErrorCode == SocketError.Success ? prefix : prefix + ": " + socket.SocketErrorCode.ToString();
}
