using Shouldly;
using SqlSource.Tool.Describing.Capture;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Capture;

// A capture's miss names the call it has no answer to, so that replay can say which.
public sealed class CaptureMissExceptionTests
{
    [Fact]
    public void NoAnswer_Method_IsKept() => CaptureMissException.NoAnswer("columns", "{}").Method.ShouldBe("columns");

    [Fact]
    public void Unreadable_Method_IsKept() =>
        CaptureMissException.Unreadable("open", typeof(string), "it is null").Method.ShouldBe("open");

    [Fact]
    public void Method_OfAStandardConstructor_IsNull() => new CaptureMissException("lost").Method.ShouldBeNull();
}
