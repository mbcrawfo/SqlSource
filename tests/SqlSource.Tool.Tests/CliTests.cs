using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

public class CliTests
{
    [Fact]
    public void Run_NoArguments_ReturnsZero() => Cli.Run([]).ShouldBe(0);
}
