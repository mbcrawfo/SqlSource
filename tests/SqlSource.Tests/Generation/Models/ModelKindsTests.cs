using Shouldly;
using SqlSource.Generation.Models;
using SqlSource.Settings;
using Xunit;

namespace SqlSource.Tests.Generation.Models;

public sealed class ModelKindsTests
{
    // ModelKind is internal, so a row names it by its number, which the attribute's enum keeps.
    [Theory]
    [InlineData(0, "record")]
    [InlineData(1, "sealed record")]
    [InlineData(2, "class")]
    [InlineData(3, "sealed class")]
    public void Keyword_EachKind_IsItsCSharpKeyword(int kind, string expected) =>
        ModelKinds.Keyword((ModelKind)kind).ShouldBe(expected);
}
