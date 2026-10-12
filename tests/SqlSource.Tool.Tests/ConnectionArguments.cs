using Shouldly;
using SqlSource.Tool.Describing;

namespace SqlSource.Tool.Tests;

// A value of "--connection" as the tool reads it, for a test that has the text.
internal static class ConnectionArguments
{
    public static ConnectionArgument Parse(string text)
    {
        ConnectionArgument.TryParse(text, out var argument).ShouldBeTrue();
        return argument.ShouldNotBeNull();
    }
}
