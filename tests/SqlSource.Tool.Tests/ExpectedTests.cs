using System.Linq;
using Shouldly;
using SqlSource.Diagnostics;
using Xunit;

namespace SqlSource.Tool.Tests;

// Expected stands in for what the tool writes.  This holds it to the descriptors.
public sealed class ExpectedTests
{
    [Fact]
    public void See_EveryDiagnostic_IsTheLineThatNamesItsSection()
    {
        foreach (var descriptor in SqlDiagnostics.All.Concat(ToolDiagnostics.All))
        {
            Expected.See(descriptor.Id).ShouldBe($"    see: {descriptor.HelpLinkUri}\n");
        }
    }
}
