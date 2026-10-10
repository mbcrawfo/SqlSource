using System.IO;
using Shouldly;
using SqlSource.Tool.Logging;
using Xunit;

namespace SqlSource.Tool.Tests;

// What a run says under "--verbose": a connection's value is removed first, and then the line is made one.
public sealed class RunTraceTests
{
    // A value that spans lines is replaced whole.  Cut into lines first, no line would hold all of it.
    [Fact]
    public void Say_ValueThatSpansLines_IsRemovedWholeAndTheLineIsOne()
    {
        using var output = new StringWriter { NewLine = "\n" };
        var trace = new RunTrace(NullRunLog.Instance, output).Without("first\nsecond");

        trace.Say("x first\nsecond y");

        output.ToString().ShouldBe("sqlsource: x *** y\n");
    }
}
