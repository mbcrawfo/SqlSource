using Shouldly;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using Xunit;

namespace SqlSource.Tool.Tests.Bundles.Renaming.Postgres;

public sealed class PostgresPlannerNamesTests
{
    [Theory]
    [InlineData("*SELECT*", true)]
    [InlineData("*SELECT* 1", true)]
    [InlineData("*SELECT* 12", true)]
    [InlineData("*VALUES*", true)]
    [InlineData("*VALUES*_2", true)]
    [InlineData("*RESULT*", true)]
    [InlineData("*ANTI*", true)]
    [InlineData("*MERGE*", true)]
    [InlineData("*GROUP*", true)]
    [InlineData("column1", true)]
    [InlineData("col2", true)]
    [InlineData("w1", true)]
    [InlineData("w10", true)]
    [InlineData("subplan", true)]
    [InlineData("initplan", true)]
    [InlineData("hashed", true)]
    [InlineData("cte", true)]
    [InlineData("unnamed_subquery", true)]
    [InlineData("any_subquery", true)]
    [InlineData("unnamed_join", true)]
    [InlineData("excluded", true)]
    [InlineData("worktable", true)]
    [InlineData("old", true)]
    [InlineData("new", true)]
    [InlineData("*SELECT* ", false)]
    [InlineData("*SELECT*x", false)]
    [InlineData("*SELECT* 1a", false)]
    [InlineData("*VALUES*_", false)]
    // A form whose letters are upper case is compared as the plan prints it, quoted.
    [InlineData("*select* 1", false)]
    [InlineData("*values*", false)]
    [InlineData("column", false)]
    [InlineData("col", false)]
    [InlineData("w", false)]
    [InlineData("wx1", false)]
    [InlineData("subplans", false)]
    [InlineData("SubPlan", false)]
    public void Is_Form_IsThePlannersAlone(string folded, bool expected) =>
        PostgresPlannerNames.Is(folded).ShouldBe(expected);
}
