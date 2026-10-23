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
    public void Wholes_ValueWithSpaceAndAForm_AreAllThreeLongestFirst() =>
        new ConnectionSecret(" Host=db;Password=pw ", forms: ["Host=db;Password=pw;Port=5432"]).Wholes.ShouldBe([
            "Host=db;Password=pw;Port=5432",
            " Host=db;Password=pw ",
            "Host=db;Password=pw",
        ]);

    [Fact]
    public void Wholes_FormEmptyOrEqualToTheValue_IsDropped() =>
        new ConnectionSecret("v=1", forms: [null, "", " ", "v=1"]).Wholes.ShouldBe(["v=1"]);

    [Fact]
    public void IsIn_FormAnywhere_IsTrue() =>
        new ConnectionSecret("Host=db", forms: ["Host=db;Password=pw;Port=5432"])
            .IsIn("xHost=db;Password=pw;Port=5432x")
            .ShouldBeTrue();

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
