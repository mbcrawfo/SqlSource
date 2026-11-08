using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public sealed class QuoteReaderTests
{
    // What SQL Server takes in a name after its first character, found against 2017 and 2025: a letter or a digit of
    // any script, a combining mark, a letter number, a format character, a connector, and "_", "@", "#" and "$".  Of
    // what it refuses, the rows hold a currency sign, a superscript digit, a circled digit and a middle dot.
    [Theory]
    [InlineData('a')]
    [InlineData('Z')]
    [InlineData('1')]
    [InlineData('é')]
    [InlineData('名')]
    [InlineData('٣')]
    [InlineData('_')]
    [InlineData('@')]
    [InlineData('#')]
    [InlineData('$')]
    [InlineData('\u0301')]
    [InlineData('\u0903')]
    [InlineData('Ⅰ')]
    [InlineData('〇')]
    [InlineData('\u200C')]
    [InlineData('‿')]
    [InlineData('＿')]
    public void IsNameCharacterOfSqlServer_Character_IsAPartOfAName(char value) =>
        QuoteReader.IsNameCharacterOfSqlServer(value).ShouldBeTrue();

    [Theory]
    [InlineData(' ')]
    [InlineData('.')]
    [InlineData('€')]
    [InlineData('²')]
    [InlineData('①')]
    [InlineData('·')]
    [InlineData('[')]
    public void IsNameCharacterOfSqlServer_Character_IsNotAPartOfAName(char value) =>
        QuoteReader.IsNameCharacterOfSqlServer(value).ShouldBeFalse();

    // The server reads a name by Unicode 3.2's tables, and the rule by .NET's: a letter added since is taken by the
    // rule, and the server refuses it with error 102.
    [Theory]
    [InlineData('ꞔ')]
    public void IsNameCharacterOfSqlServer_LetterNewerThanTheServer_IsTaken(char value) =>
        QuoteReader.IsNameCharacterOfSqlServer(value).ShouldBeTrue();
}
