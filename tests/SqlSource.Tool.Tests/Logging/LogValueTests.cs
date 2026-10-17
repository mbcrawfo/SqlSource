using Shouldly;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests.Logging;

// A value of the log made from a value that may be missing.
public sealed class LogValueTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Of_Flag_IsTheFlag(bool flag) => LogValue.Of(flag).ShouldBe(new LogValue.Flag(flag));

    [Fact]
    public void Of_NoFlag_IsNull() => LogValue.Of((bool?)null).ShouldBe(LogValue.Null);
}
