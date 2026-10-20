using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Security.Authentication;
using Npgsql;
using Shouldly;
using SqlSource.Tool.Describing.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres;

// What the driver threw, as the answer of a call holds it: at "open" and at a later call.  Every server error here
// names the host and the user in its message, detail and hint, as a server's error of a connection does, and every
// driver's message names the host and the port, so that what is kept of each is seen to hold neither.
public sealed class ConnectionErrorsTests
{
    private const string Host = "10.0.0.5";
    private const string User = "sqlsrc_wombat";

    public static TheoryData<string> OwnTexts => [.. PostgresConnection.OwnTexts];

    [Theory]
    [InlineData("28P01", "the server refused the password")]
    [InlineData("28000", "the server refused to authenticate the user")]
    [InlineData("28P99", "the server refused to authenticate the user")]
    [InlineData("3D000", "the server has no such database")]
    [InlineData("42501", "the user may not connect to the database")]
    [InlineData("53300", "the server has too many connections")]
    [InlineData("57P03", "the server is not accepting connections now")]
    [InlineData("08006", "the server refused the connection")]
    [InlineData("08P01", "the server refused the connection")]
    [InlineData("57P01", "the server refused to open a session")]
    [InlineData("XX000", "the server refused to open a session")]
    public void AtOpen_ServerError_KeepsTheSqlStateAndPutsAFixedText(string sqlState, string message) =>
        ConnectionErrors
            .AtOpen(Server(sqlState))
            .ShouldBe(new PostgresServerError(sqlState, message, null, null, null));

    [Fact]
    public void AtOpen_NoPasswordGiven_IsTheFixedText() =>
        ConnectionErrors
            .AtOpen(new NpgsqlException("No password has been provided but the backend requires one (in SASL)"))
            .ShouldBe(
                new PostgresServerError(
                    null,
                    "the server asks for a password and the connection string gives none",
                    null,
                    null,
                    null
                )
            );

    // The timeout is looked for anywhere in the chain, and before a socket's error.
    [Fact]
    public void AtOpen_TimeoutInTheChain_IsTheFixedText() =>
        ConnectionErrors
            .AtOpen(
                new NpgsqlException(
                    $"Failed to connect to {Host}:5432",
                    new IOException(
                        "timed out",
                        new TimeoutException($"{Host}:5432", new SocketException((int)SocketError.TimedOut))
                    )
                )
            )
            .ShouldBe(new PostgresServerError(null, "the connection to the server timed out", null, null, null));

    [Fact]
    public void AtOpen_SocketErrorInTheChain_IsTheFixedTextWithTheErrorsName() =>
        ConnectionErrors
            .AtOpen(
                new NpgsqlException(
                    $"Failed to connect to {Host}:5432",
                    new SocketException((int)SocketError.ConnectionRefused)
                )
            )
            .ShouldBe(
                new PostgresServerError(null, "Npgsql could not reach the server: ConnectionRefused", null, null, null)
            );

    [Fact]
    public void AtOpen_SocketErrorFurtherIn_IsTheFixedTextWithTheErrorsName() =>
        ConnectionErrors
            .AtOpen(
                new NpgsqlException(
                    $"Failed to connect to {Host}:5432",
                    new IOException(Host, new SocketException((int)SocketError.HostNotFound))
                )
            )
            .Message.ShouldBe("Npgsql could not reach the server: HostNotFound");

    [Fact]
    public void AtOpen_OtherDriverError_NamesTheTypeOfTheInnermostException() =>
        ConnectionErrors
            .AtOpen(
                new NpgsqlException(
                    "Exception while performing SSL handshake",
                    new IOException(Host, new AuthenticationException($"the certificate of {Host} is not trusted"))
                )
            )
            .ShouldBe(
                new PostgresServerError(
                    null,
                    "Npgsql could not open a connection to the server (AuthenticationException)",
                    null,
                    null,
                    null
                )
            );

    [Fact]
    public void AtOpen_OtherDriverErrorWithNoInnerException_NamesItsOwnType() =>
        ConnectionErrors
            .AtOpen(new NpgsqlException($"the server {Host} said something"))
            .Message.ShouldBe("Npgsql could not open a connection to the server (NpgsqlException)");

    // A message of the describer's own with an inner exception is the driver's: the describer never gives one.
    [Fact]
    public void AtOpen_OwnTextWithAnInnerException_IsNotPassedOn() =>
        ConnectionErrors
            .AtOpen(new NpgsqlException(PostgresConnection.LostConnection, new IOException(Host)))
            .Message.ShouldBe("Npgsql could not open a connection to the server (IOException)");

    [Theory]
    [MemberData(nameof(OwnTexts))]
    public void AtOpen_OwnText_PassesUnchanged(string text) =>
        ConnectionErrors
            .AtOpen(new NpgsqlException(text))
            .ShouldBe(new PostgresServerError(null, text, null, null, null));

    [Theory]
    [MemberData(nameof(OwnTexts))]
    public void Of_OwnText_PassesUnchanged(string text) =>
        ConnectionErrors.Of(new NpgsqlException(text)).ShouldBe(new PostgresServerError(null, text, null, null, null));

    // The set is kept beside the constants: one that it leaves out would be answered as the driver's.
    [Fact]
    public void OwnTexts_EveryPublicTextOfTheConnection_IsListed() =>
        PostgresConnection.OwnTexts.ShouldBe(
            typeof(PostgresConnection)
                .GetFields(BindingFlags.Public | BindingFlags.Static)
                .Where(static field => field.IsLiteral && field.FieldType == typeof(string))
                .Select(static field => (string)field.GetRawConstantValue()!),
            ignoreOrder: true
        );

    [Theory]
    [InlineData("08006")]
    [InlineData("08003")]
    [InlineData("57P01")]
    [InlineData("57P02")]
    [InlineData("57P04")]
    [InlineData("57P05")]
    [InlineData("25P03")]
    public void Of_ErrorThatEndsTheSession_KeepsTheSqlStateAndPutsAFixedText(string sqlState) =>
        ConnectionErrors
            .Of(Server(sqlState))
            .ShouldBe(new PostgresServerError(sqlState, "the server ended the session", null, null, null));

    [Fact]
    public void Of_TimeoutInTheChain_IsTheFixedText() =>
        ConnectionErrors
            .Of(new NpgsqlException("Exception while reading from stream", new TimeoutException($"{Host}:5432")))
            .ShouldBe(new PostgresServerError(null, "the server did not answer in time", null, null, null));

    [Fact]
    public void Of_OtherDriverError_IsALostConnection() =>
        ConnectionErrors
            .Of(
                new NpgsqlException(
                    "Exception while reading from stream",
                    new IOException(Host, new SocketException((int)SocketError.ConnectionReset))
                )
            )
            .ShouldBe(new PostgresServerError(null, PostgresConnection.LostConnection, null, null, null));

    [Fact]
    public void Of_OtherServerError_KeepsItsMessageDetailHintAndPosition() =>
        ConnectionErrors
            .Of(Server("42P01", "relation \"users\" does not exist", "a detail", "a hint", position: 49))
            .ShouldBe(new PostgresServerError("42P01", "relation \"users\" does not exist", "a detail", "a hint", 49));

    // The server sends no position as zero.
    [Fact]
    public void Of_OtherServerErrorWithPositionZero_HasNoPosition() =>
        ConnectionErrors.Of(Server("42P01", position: 0)).Position.ShouldBeNull();

    [Fact]
    public void Of_OtherServerErrorWithPositionOne_KeepsIt() =>
        ConnectionErrors.Of(Server("42P01", position: 1)).Position.ShouldBe(1);

    private static PostgresException Server(
        string sqlState,
        string message = $"password authentication failed for user \"{User}\"",
        string detail = $"Connection matched pg_hba.conf line 99: \"host all {User} {Host}/32 scram-sha-256\"",
        string hint = $"the role {User} on {Host}",
        int position = 7
    ) => new(message, "FATAL", "FATAL", sqlState, detail: detail, hint: hint, position: position);
}
