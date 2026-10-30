using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public sealed class SqlServerNamesTests
{
    [Theory]
    [InlineData("User", "[User]")]
    [InlineData("my type", "[my type]")]
    [InlineData("a]b", "[a]]b]")]
    [InlineData("a]]b", "[a]]]]b]")]
    [InlineData("", "[]")]
    public void Bracketed_Name_IsInBracketsWithEachBracketDoubled(string name, string expected) =>
        SqlServerNames.Bracketed(name).ShouldBe(expected);

    // A keyword is all letters, so a UdtTypeName's part keeps it bare: SqlClient only splits the name at its dots.
    [Theory]
    [InlineData("Point")]
    [InlineData("my_type1")]
    [InlineData("User")]
    public void UdtPart_LettersDigitsAndUnderscores_IsAsItIs(string part) =>
        SqlServerNames.UdtPart(part).ShouldBe(part);

    [Theory]
    [InlineData("my type", "[my type]")]
    [InlineData("a.b", "[a.b]")]
    [InlineData("a]b", "[a]]b]")]
    [InlineData("", "")]
    public void UdtPart_OtherCharacters_AreInBrackets(string part, string expected) =>
        SqlServerNames.UdtPart(part).ShouldBe(expected);

    [Fact]
    public void ClrSystemTypes_AreTheThreeOfSys() =>
        SqlServerNames.ClrSystemTypes.ShouldBe(["hierarchyid", "geography", "geometry"]);

    [Theory]
    [InlineData("hierarchyid", true)]
    [InlineData("geography", true)]
    [InlineData("geometry", true)]
    [InlineData("GEOMETRY", true)]
    [InlineData("int", false)]
    [InlineData("geometryx", false)]
    [InlineData("", false)]
    public void IsClrSystemType_Name_IsOneOfTheThreeIgnoringCase(string name, bool expected) =>
        SqlServerNames.IsClrSystemType(name).ShouldBe(expected);
}
