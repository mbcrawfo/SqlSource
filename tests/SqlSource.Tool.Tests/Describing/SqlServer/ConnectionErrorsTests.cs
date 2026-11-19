using Shouldly;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.SqlServer;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer;

// The describer's own errors, and how every error is printed as a server line.  SqlException has no public
// constructor, so what AtOpen and Of make of one is tested against the container.
public sealed class ConnectionErrorsTests
{
    [Fact]
    public void Own_Text_IsOneRowOfNumberClassAndStateZero() =>
        ConnectionErrors
            .Own(ConnectionErrors.UnreadableConnectionString)
            .Errors.ShouldBe([new SqlServerErrorRow(0, 0, 0, "SqlClient cannot read the connection string")]);

    [Fact]
    public void ServerLines_ServerErrors_AreEachMsgLevelAndStateInOrder() =>
        SqlServerErrors
            .ServerLines(
                new SqlServerServerError([
                    new SqlServerErrorRow(4060, 11, 1, "the server cannot open the database for the login"),
                    new SqlServerErrorRow(18456, 14, 1, "the server refused the login"),
                ])
            )
            .ShouldBe([
                "Msg 4060, Level 11, State 1: the server cannot open the database for the login",
                "Msg 18456, Level 14, State 1: the server refused the login",
            ]);

    // SqlClient's native network layer, which it uses on Windows, puts a Win32 code where the managed layer of Linux
    // and macOS puts a SocketException or an AuthenticationException, and each gets the text the managed layer's gets.
    [Theory]
    [InlineData(258, 258, "the connection to the server timed out")]
    [InlineData(11001, 11001, "SqlClient could not reach the server: HostNotFound")]
    [InlineData(10061, 10061, "SqlClient could not reach the server: ConnectionRefused")]
    [InlineData(-2146893019, null, "the server's certificate is not trusted, or the TLS handshake failed")]
    [InlineData(-2146893048, -2146893048, "the server's certificate is not trusted, or the TLS handshake failed")]
    [InlineData(-2146893007, null, "the server's certificate is not trusted, or the TLS handshake failed")]
    [InlineData(-2146893017, null, "the server's certificate is not trusted, or the TLS handshake failed")]
    [InlineData(-2146762486, null, "the server's certificate is not trusted, or the TLS handshake failed")]
    [InlineData(-2146762478, null, "the server's certificate is not trusted, or the TLS handshake failed")]
    [InlineData(-2146762482, null, "the server's certificate is not trusted, or the TLS handshake failed")]
    [InlineData(-2146885616, null, "the server's certificate is not trusted, or the TLS handshake failed")]
    [InlineData(-2146885613, null, "the server's certificate is not trusted, or the TLS handshake failed")]
    public void NativeLayerText_NativeError_IsTheTextLinuxGives(int number, int? win32, string text) =>
        ConnectionErrors.NativeLayerText(number, win32).ShouldBe(text);

    [Theory]
    [InlineData(18456, null)]
    [InlineData(4060, null)]
    [InlineData(0, 5)]
    public void NativeLayerText_OtherError_IsNull(int number, int? win32) =>
        ConnectionErrors.NativeLayerText(number, win32).ShouldBeNull();

    // An error of the describer's own has no number, and its line is the text alone.
    [Fact]
    public void ServerLines_OwnError_IsTheTextAlone() =>
        SqlServerErrors
            .ServerLines(ConnectionErrors.Own(ConnectionTexts.LostConnection))
            .ShouldBe(["the connection to the server was lost"]);
}
