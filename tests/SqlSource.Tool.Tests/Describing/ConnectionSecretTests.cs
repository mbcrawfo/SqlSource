using Shouldly;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests.Describing;

// The secret of a connection: its whole value, found anywhere, and the parts a describer gave, found as tokens.
public sealed class ConnectionSecretTests
{
    [Fact]
    public void Parts_BlankRepeatedAndTheValueItself_AreDroppedAndTheRestLongestFirst() =>
        new ConnectionSecret(
            "Password=pass",
            ["pass", null, "", "    ", "Password=pass", "hunter2", "pass", "keys"]
        ).Parts.ShouldBe(["hunter2", "pass", "keys"]);

    [Fact]
    public void Parts_NoneGiven_IsEmpty() => new ConnectionSecret("Password=pass").Parts.ShouldBeEmpty();

    // A shorter part would match too much ordinary text.
    [Fact]
    public void Parts_ShorterThanTheMinimum_AreDroppedAndOneOfTheMinimumIsKept() =>
        new ConnectionSecret("Host=db;Password=abc", ["abc", "abcd"]).Parts.ShouldBe(["abcd"]);

    // The run prints the database's name and the engine's whatever the describer does.
    [Fact]
    public void Parts_ThatAreAWordTheRunWrites_AreDroppedComparedOrdinally() =>
        new ConnectionSecret(
            "Host=db;Password=postgres",
            ["postgres", "billing", "Billing"],
            ["billing", "postgres"]
        ).Parts.ShouldBe(["Billing"]);

    [Fact]
    public void IsIn_ValueInsideAWord_IsTrue() => new ConnectionSecret("test").IsIn("latest_orders").ShouldBeTrue();

    [Fact]
    public void IsIn_PartAsAToken_IsTrue() =>
        new ConnectionSecret("Host=db;Password=test", ["test"]).IsIn("a test of it").ShouldBeTrue();

    [Fact]
    public void IsIn_PartInsideAWord_IsFalse() =>
        new ConnectionSecret("Host=db;Password=test", ["test"]).IsIn("latest_orders").ShouldBeFalse();

    [Theory]
    [InlineData("hunter2", "Password=hunter2;", true)]
    [InlineData("p@ss!", "'p@ss!'", true)]
    [InlineData("p@ss!", "xp@ss!", false)]
    public void IsIn_PartBesideOtherCharacters_IsWhetherItStandsAsAToken(string part, string text, bool expected) =>
        new ConnectionSecret("Host=db;Password=" + part, [part]).IsIn(text).ShouldBe(expected);

    // The edges of the rule, which holds for a part of any length: the text's own ends count as no letter, and an
    // edge of the part that is no letter, digit or "_" is not checked.
    [Theory]
    [InlineData("pw", "pw", true)]
    [InlineData("pw x", "pw", true)]
    [InlineData("x pw", "pw", true)]
    [InlineData("x!pw", "!pw", true)]
    [InlineData("aaa", "aa", false)]
    public void HoldsToken_PartAtAnEdge_IsWhetherItStandsAsAToken(string text, string part, bool expected) =>
        Redaction.HoldsToken(text, part).ShouldBe(expected);

    // A part that holds another as a token is replaced first.
    [Fact]
    public void Of_PartThatHoldsAShorterOneAsAToken_IsReplacedWhole() =>
        Redaction.Of("pass-word", new ConnectionSecret("Host=db", ["pass", "pass-word"])).ShouldBe("***");

    [Fact]
    public void Of_TwoPartsOneInsideTheOther_AreEachTheMark() =>
        Redaction.Of("hunter2 hunter", new ConnectionSecret("Host=db", ["hunter", "hunter2"])).ShouldBe("*** ***");

    // A record prints its members, and these are secrets.
    [Fact]
    public void ToString_OfASecret_HoldsNeitherTheValueNorAPart()
    {
        var text = new ConnectionSecret("Host=db;Password=hunter2", ["hunter2"]).ToString();

        text.ShouldNotContain("Host=db");
        text.ShouldNotContain("hunter2");
    }
}
