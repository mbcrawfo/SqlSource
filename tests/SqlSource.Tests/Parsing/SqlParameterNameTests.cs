using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public sealed class SqlParameterNameTests
{
    [Fact]
    public void Of_ParameterSpan_IsTheSpanLessThePrefix() =>
        SqlParameterName.Of(new TextSpan(4, 3)).ShouldBe(new TextSpan(5, 2));

    [Theory]
    [InlineData("@id", "@", "id")]
    [InlineData(":id", "@:", "id")]
    [InlineData("@id", "@:", "id")]
    public void WithoutPrefix_NameWithAPrefix_IsTheBareName(string name, string prefixes, string expected) =>
        SqlParameterName.WithoutPrefix(name, prefixes).ShouldBe(expected);

    [Theory]
    [InlineData("id", "@", "id")]
    [InlineData(":id", "@", ":id")]
    [InlineData("", "@", "")]
    public void WithoutPrefix_NameWithoutAPrefix_IsTheName(string name, string prefixes, string expected) =>
        SqlParameterName.WithoutPrefix(name, prefixes).ShouldBe(expected);
}
