using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public class SqlIdentifierTests
{
    [Theory]
    [InlineData('a', true)]
    [InlineData('Z', true)]
    [InlineData('_', true)]
    [InlineData('7', true)]
    [InlineData('ß', true)]
    [InlineData(' ', false)]
    [InlineData('-', false)]
    [InlineData('.', false)]
    [InlineData('​', false)]
    public void IsPartCharacter_CharacterThatCanFollowTheFirstOfAnIdentifier_IsOne(char character, bool expected) =>
        SqlIdentifier.IsPartCharacter(character).ShouldBe(expected);

    [Theory]
    [InlineData("name", true)]
    [InlineData("_1", true)]
    [InlineData("class", true)]
    [InlineData("", false)]
    [InlineData("1st", false)]
    [InlineData("a b", false)]
    [InlineData("a​b", false)]
    public void IsValid_FormOfAnIdentifier_IsValid(string value, bool expected) =>
        SqlIdentifier.IsValid(value).ShouldBe(expected);

    [Theory]
    [InlineData("name", true)]
    [InlineData("where", true)]
    [InlineData("class", false)]
    [InlineData("a​b", false)]
    public void IsUsableName_IdentifierThatIsNotAReservedKeyword_IsUsable(string value, bool expected) =>
        SqlIdentifier.IsUsableName(value).ShouldBe(expected);
}
