using Shouldly;
using SqlSource.Tool.Describing.Postgres.Walk.Expressions;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.Postgres.Walk.Expressions;

public sealed class PlanTokensTests
{
    [Theory]
    [InlineData("id", "id")]
    [InlineData("\"Odd \"\"x\"\"\"", "Odd \"x\"")]
    [InlineData("(", null)]
    [InlineData("'id'", null)]
    public void NameOf_OfAWordAQuotedNameAndASymbol_IsItsNameOrNull(string entry, string? expected)
    {
        var tokens = PlanTokens.Read(entry + (entry == "(" ? ")" : string.Empty));

        PlanTokens.NameOf(entry + (entry == "(" ? ")" : string.Empty), tokens![0]).ShouldBe(expected);
    }
}
