using System;
using System.Net.Sockets;
using Shouldly;
using SqlSource.Tool.Describing.Describers;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Describers;

// The first exception of a type in a chain of inner exceptions, the exception itself first.
public sealed class ExceptionChainTests
{
    [Fact]
    public void Find_TypeOfAnInnerException_IsThatException()
    {
        var socket = new SocketException();
        var exception = new InvalidOperationException("outer", new TimeoutException("middle", socket));

        ExceptionChain.Find<SocketException>(exception).ShouldBeSameAs(socket);
    }

    [Fact]
    public void Find_TypeOfTheException_IsTheException()
    {
        var timeout = new TimeoutException("outer", new TimeoutException("inner"));

        ExceptionChain.Find<TimeoutException>(timeout).ShouldBeSameAs(timeout);
    }

    [Fact]
    public void Find_TypeOfNone_IsNull() =>
        ExceptionChain
            .Find<SocketException>(new InvalidOperationException("outer", new TimeoutException("inner")))
            .ShouldBeNull();
}
