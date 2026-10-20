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
            "Password=pw",
            ["pw", null, "", "  ", "Password=pw", "hunter2", "pw", "key"]
        ).Parts.ShouldBe(["hunter2", "key", "pw"]);

    [Fact]
    public void Parts_NoneGiven_IsEmpty() => new ConnectionSecret("Password=pw").Parts.ShouldBeEmpty();

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

    // A record prints its members, and these are secrets.
    [Fact]
    public void ToString_OfASecret_HoldsNeitherTheValueNorAPart()
    {
        var text = new ConnectionSecret("Host=db;Password=hunter2", ["hunter2"]).ToString();

        text.ShouldNotContain("Host=db");
        text.ShouldNotContain("hunter2");
    }
}
