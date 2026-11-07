// Shared: tests/SqlSource.Tests.RoslynFloor compiles this file again, on Roslyn 4.14.0.  See AGENTS.md beside it.

using Shouldly;
using SqlSource.Testing.Generator;
using Xunit;

namespace SqlSource.Tests.Generator;

// The generator parameter list-padding: carried to the query's method as QueryMethod.PadsLists, true unless it is off.
public sealed class ListPaddingParameterTests
{
    [Theory]
    [InlineData(null, true)]
    [InlineData("list-padding", true)]
    [InlineData("list-padding=default", true)]
    [InlineData("list-padding=off", false)]
    [InlineData("LIST-PADDING=OFF", false)]
    public void Run_ListPadding_ReachesTheMethodAsPadsLists(string? parameter, bool expected)
    {
        var sql =
            (parameter is null ? string.Empty : "-- generator: " + parameter + "\n")
            + "-- name: ListUsers\nSELECT id, name FROM users;\n";

        var run = MethodRuns.ListUsers([new SqlFile(GeneratorHarness.UsersPath, sql)], null);

        run.ShouldBeClean();
        MethodRuns.SingleMethod(run).PadsLists.ShouldBe(expected);
    }
}
