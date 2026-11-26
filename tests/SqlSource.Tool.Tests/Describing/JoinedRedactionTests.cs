using System.Collections.Generic;
using Shouldly;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// The strings of an event searched joined: a range is found in the original texts and marked once.
public sealed class JoinedRedactionTests
{
    private static readonly ConnectionSecret Password = new("Host=db;Password=hunter22", ["hunter22"]);

    [Fact]
    public void Of_NothingFound_IsTheSameTexts() =>
        JoinedRedaction.Of(["a", "latest_hunt", "er22x", ""], Password).ShouldBe(["a", "latest_hunt", "er22x", ""]);

    [Theory]
    [InlineData("refused Host=db;Password=pw00 for 'hunter22' at latest_hunter22")]
    [InlineData("Host=db;Password=pw00hunter22 x")]
    [InlineData("a hunter22Host=db;Password=pw00 b")]
    [InlineData("hunter22 refused")]
    public void Of_OneTextWithNothingSplit_IsWhatOfIs(string text)
    {
        var secret = new ConnectionSecret("Host=db;Password=pw00", ["hunter22"]);

        JoinedRedaction.Of([text], secret).ShouldBe([Redaction.Of(text, secret)]);
    }

    // The password is no token against the value until the value is a mark; it is then, and is not left.
    [Fact]
    public void Of_PartGluedToAWhole_IsMarkedToo()
    {
        var secret = new ConnectionSecret("Host=db;Password=pw00", ["hunter22"]);

        var cleaned = JoinedRedaction.Of(["Host=db;Password=pw00hunter22 x"], secret);

        cleaned.ShouldBe(["****** x"]);
        cleaned[0].ShouldNotContain("hunter22");
    }

    // The password alone in the second text would have been cleaned first, and the whole value gone with it.
    [Fact]
    public void Of_WholeSplitOverTwoTextsWhoseSecondHoldsAPart_IsTheMarkInBoth() =>
        JoinedRedaction.Of(["Host=db;Pass", "word=hunter22"], Password).ShouldBe(["***", "***"]);

    // A whole and a part that overlap in a text are one mark, and nothing of either is left.
    [Fact]
    public void Of_WholeAndPartThatOverlapInAText_AreOneMark()
    {
        var secret = new ConnectionSecret("Host=db;Pass", ["Passwords"]);

        JoinedRedaction.Of(["x Host=db;Passwords y"], secret).ShouldBe(["x *** y"]);
    }

    [Fact]
    public void Of_WholeAndPartThatTouchInAText_AreOneMark()
    {
        var secret = new ConnectionSecret("Host=db;", ["hunter22"]);

        JoinedRedaction.Of(["x Host=db;hunter22 y"], secret).ShouldBe(["x *** y"]);
    }

    [Fact]
    public void Of_WholeOverThreeTexts_IsTheMarkInEach() =>
        JoinedRedaction.Of(["a Host=db", ";Password", "=hunter22 b"], Password).ShouldBe(["a ***", "***", "*** b"]);

    [Fact]
    public void Of_EmptyTextInTheMiddleOfAWhole_IsLeftEmpty() =>
        JoinedRedaction.Of(["Host=db;Pass", "", "word=hunter22"], Password).ShouldBe(["***", "", "***"]);

    [Fact]
    public void Of_SubsetThatSkipsTextsBetween_IsSearchedWithoutThem()
    {
        string[] texts = ["Host=db;Pass", "between", "word=hunter22"];

        JoinedRedaction.Of(texts, Password).ShouldBe(["Host=db;Pass", "between", "word=***"]);
        IReadOnlyList<int> withoutTheMiddle = [0, 2];
        JoinedRedaction.Of(texts, Password, withoutTheMiddle).ShouldBe(["***", "between", "***"]);
    }

    [Fact]
    public void Of_KeyedPartWhoseKeyIsInTheTextBefore_IsTheMark()
    {
        var secret = new ConnectionSecret("Host=db;Password=pg", ["pg"]);

        JoinedRedaction
            .Of(["refused Password=", "pg", " here"], secret)
            .ShouldBe(["refused Password=", "***", " here"]);
    }

    // Every pair: a whole or a part over two texts with another between them, or in the reverse order, is in neither
    // joined search of Of.
    [Theory]
    [InlineData("Host=db;Pass|between|word=hunter22", "***|between|***")]
    [InlineData("word=hunter22 b|a Host=db;Pass", "*** b|a ***")]
    [InlineData("x hun|between|ter22", "x ***|between|***")]
    [InlineData("ter22 x|hun", "*** x|***")]
    [InlineData("unter22 b|a h", "*** b|a ***")]
    [InlineData("2 b|a hunter2", "*** b|a ***")]
    public void OfEveryPair_WholeOrPartOverTwoTextsOutOfOrder_IsTheMarkInBoth(string texts, string expected)
    {
        var split = texts.Split('|');

        JoinedRedaction.OfEveryPair(split, Password).ShouldBe(expected.Split('|'));
        JoinedRedaction.Of(split, Password).ShouldNotBe(expected.Split('|'));
    }

    // The halves of a part joined must stand as a token, and one text is no pair with itself.
    [Theory]
    [InlineData("xhun|ter22")]
    [InlineData("ter22|hunx")]
    [InlineData("hun|ter22x")]
    [InlineData("ter22 hun")]
    [InlineData("ter22hun")]
    public void OfEveryPair_HalvesThatMakeNoTokenOrAreOneText_AreLeft(string texts) =>
        JoinedRedaction.OfEveryPair(texts.Split('|'), Password).ShouldBe(texts.Split('|'));

    // An end of a part that is no letter, digit or "_" is not judged against the text beside it.
    [Fact]
    public void OfEveryPair_PartThatStartsWithASymbolAfterALetter_IsTheMarkInBoth()
    {
        var secret = new ConnectionSecret("Host=db;Password=!hunter22", ["!hunter22"]);

        JoinedRedaction.OfEveryPair(["ter22 y", "x!hun"], secret).ShouldBe(["*** y", "x***"]);
    }

    // A whole is marked against any character beside it, as Redaction.Of marks it in one text.
    [Fact]
    public void OfEveryPair_WholeOverTwoTextsOutOfOrderBetweenLetters_IsTheMarkInBoth() =>
        JoinedRedaction.OfEveryPair(["word=hunter22x", "aHost=db;Pass"], Password).ShouldBe(["***x", "a***"]);

    // Every text that holds a half is marked, whichever other text holds the rest.
    [Fact]
    public void OfEveryPair_HalfInSeveralTexts_IsTheMarkInEach() =>
        JoinedRedaction.OfEveryPair(["a hun", "b hun", "ter22 c"], Password).ShouldBe(["a ***", "b ***", "*** c"]);

    // A form that holds a line break is cut there, as the lines of a list hold it.
    [Fact]
    public void OfEveryPair_FormWithABreakOverTwoLinesInReverse_IsTheMarkInBoth()
    {
        var secret = new ConnectionSecret("Host=db;\nPassword=pw");

        JoinedRedaction.OfEveryPair(["Password=pw b", "a Host=db;"], secret).ShouldBe(["*** b", "a ***"]);
    }

    // Halves that make the form less a character other than its break are no cut of it.
    [Fact]
    public void OfEveryPair_FormWithABreakOverTwoTextsLessACharacter_IsLeft()
    {
        var secret = new ConnectionSecret("Host=db;\nPassword=pw");
        string[] texts = ["\nPassword=pw b", "a Host=db"];

        JoinedRedaction.OfEveryPair(texts, secret).ShouldBe(texts);
    }

    [Fact]
    public void Of_BreakInTheFormAsItIs_IsFoundOverTexts()
    {
        var secret = new ConnectionSecret("host=db;password=pw", forms: ["Host=db;\r\nPassword=pw"]);

        JoinedRedaction.Of(["a Host=db;\r", "Password=pw b"], secret).ShouldBe(["a ***", "*** b"]);
    }
}
