using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public sealed class QuoteReaderTests
{
    // What SQL Server takes in a name, found against 2017 and 2025: a letter or a digit of any script, and "_", "@",
    // "#" and "$".  It refuses a currency sign, a superscript digit, a circled digit and a middle dot.
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
    public void IsNameCharacterOfSqlServer_Character_IsALetterADigitOrOneOfFour(char value) =>
        QuoteReader.IsNameCharacterOfSqlServer(value).ShouldBeTrue();

    [Theory]
    [InlineData(' ')]
    [InlineData('.')]
    [InlineData('€')]
    [InlineData('²')]
    [InlineData('·')]
    [InlineData('[')]
    public void IsNameCharacterOfSqlServer_Character_IsNotAPartOfAName(char value) =>
        QuoteReader.IsNameCharacterOfSqlServer(value).ShouldBeFalse();
}
