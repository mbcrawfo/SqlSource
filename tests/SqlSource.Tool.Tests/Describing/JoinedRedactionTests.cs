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

    [Fact]
    public void Of_OneTextWithNothingSplit_IsWhatOfIs()
    {
        const string Text = "refused Host=db;Password=hunter22 for 'hunter22' at latest_hunter22";

        JoinedRedaction.Of([Text], Password).ShouldBe([Redaction.Of(Text, Password)]);
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
    public void Of_BreakInTheFormAsItIs_IsFoundOverTexts()
    {
        var secret = new ConnectionSecret("host=db;password=pw", forms: ["Host=db;\r\nPassword=pw"]);

        JoinedRedaction.Of(["a Host=db;\r", "Password=pw b"], secret).ShouldBe(["a ***", "*** b"]);
    }
}
