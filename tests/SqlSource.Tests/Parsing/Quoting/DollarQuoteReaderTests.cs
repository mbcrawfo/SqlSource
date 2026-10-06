using Shouldly;
using SqlSource.Parsing.Quoting;
using Xunit;

namespace SqlSource.Tests.Parsing.Quoting;

public class DollarQuoteReaderTests
{
    private static readonly DollarQuoteReader Reader = new();

    [Theory]
    [InlineData("$$ -- x $$ y", "$$ -- x $$")]
    [InlineData("$$$$ y", "$$$$")]
    [InlineData("$fn$ /* x */ $fn$ y", "$fn$ /* x */ $fn$")]
    [InlineData("$a$ $b$ -- c $a$ y", "$a$ $b$ -- c $a$")]
    [InlineData("$_t1$ ' $_t1$ y", "$_t1$ ' $_t1$")]
    [InlineData("$größe$ -- x $größe$ y", "$größe$ -- x $größe$")]
    [InlineData("x = $$a$$ y", "$$a$$")]
    [InlineData("$a$ $A$ $a$ y", "$a$ $A$ $a$")]
    [InlineData("$$ $a$ $$ y", "$$ $a$ $$")]
    public void FindEnd_OpenerWithItsCloser_EndsAfterTheSameTag(string text, string expected) =>
        Region.Read(Reader, text, '$').ShouldBe(expected);

    [Theory]
    // After an identifier character.
    [InlineData("v$session$ x $session$")]
    [InlineData("a$$ b $$")]
    [InlineData("_$$ b $$")]
    [InlineData("1$$ b $$")]
    // Not the form of an opener.
    [InlineData("$1 x")]
    [InlineData("$5.00 x")]
    [InlineData("$1$ x $1$")]
    [InlineData("$a-b$ x $a-b$")]
    [InlineData("$")]
    [InlineData("$a")]
    // An opener with no closer.
    [InlineData("$$ x")]
    [InlineData("$a$ x")]
    [InlineData("$a$ $b$ x")]
    [InlineData("$a$ x $A$")]
    [InlineData("$$")]
    public void FindEnd_DollarThatOpensNoQuote_IsNotAQuote(string text) =>
        Region.Read(Reader, text, '$').ShouldBe(Region.NotAQuote);
}
