using System.Linq;
using Shouldly;
using Xunit;
using static SqlSource.Tool.Testing.DescribeRequests;

namespace SqlSource.Tool.Tests;

// The request a test of a describer builds: its parameters as the plan of a run lists them, or as the test gives them.
public sealed class DescribeRequestsTests
{
    [Fact]
    public void Of_SqlWithAParameterTwice_ListsItOnceInTheOrderItFirstAppears()
    {
        var request = Of("SELECT @b, @a, @B");

        request.Name.ShouldBe("Q");
        request.Parameters.Select(parameter => parameter.Name).ShouldBe(["b", "a"]);
        request.Parameters.ShouldAllBe(parameter => !parameter.IsDeclared);
    }

    [Fact]
    public void Of_DeclaredParameterTheSqlLacks_IsListedAfterTheSqlsOwn()
    {
        var request = Of("SELECT @a", ("z", "int4"));

        request.Parameters.Select(parameter => parameter.Name).ShouldBe(["a", "z"]);
        request.Parameters[1].ShouldBe(Declared("z", "int4"));
    }

    [Fact]
    public void Of_DeclaredParameterTheSqlHolds_IsDeclaredInItsPlace()
    {
        var request = Of("SELECT @a", ("A", "text"));

        request.Parameters.ShouldHaveSingleItem().ShouldBe(Declared("a", "text"));
    }

    [Fact]
    public void Listed_Parameters_AreKeptAsGiven()
    {
        var request = Listed("SELECT 1", Declared("x", "int4"), Inferred("y"));

        request.Parameters.Select(parameter => parameter.Name).ShouldBe(["x", "y"]);
    }
}
