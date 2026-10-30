using System.Net.Sockets;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Describers;

// The text of a server that could not be reached: the engine's prefix, and the socket's reason when it names one.
public sealed class ConnectionTextsTests
{
    private const string Prefix = "the driver could not reach the server";

    [Fact]
    public void Unreachable_SocketErrorWithAReason_IsThePrefixAndItsName() =>
        ConnectionTexts
            .Unreachable(Prefix, new SocketException((int)SocketError.ConnectionRefused))
            .ShouldBe(Prefix + ": ConnectionRefused");

    [Fact]
    public void Unreachable_SocketErrorOfSuccess_IsThePrefixAlone() =>
        ConnectionTexts.Unreachable(Prefix, new SocketException((int)SocketError.Success)).ShouldBe(Prefix);
}
