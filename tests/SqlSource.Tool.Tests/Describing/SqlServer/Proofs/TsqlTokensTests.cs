using System.Linq;
using Shouldly;
using SqlSource.Tool.Describing.SqlServer.Proofs;
using Xunit;

namespace SqlSource.Tool.Tests.Describing.SqlServer.Proofs;

// The tokens the proofs read a query by: the lexer's quoted regions and parameters, and its text split into words,
// numbers and symbols, with comments and hints dropped.
public sealed class TsqlTokensTests
{
    [Fact]
    public void Read_EveryKind_IsReadInOrder()
    {
        const string Sql = """SELECT a.b, N'x''y', 0x1F, 1.5e-3, @p, [q]]r], "s" FROM t""";

        var tokens = TsqlTokens.Read(Sql);

        tokens
            .Select(static token => (token.Kind, token.Text))
            .ShouldBe([
                (TsqlTokenKind.Word, "SELECT"),
                (TsqlTokenKind.Word, "a"),
                (TsqlTokenKind.Symbol, "."),
                (TsqlTokenKind.Word, "b"),
                (TsqlTokenKind.Symbol, ","),
                (TsqlTokenKind.String, "N'x''y'"),
                (TsqlTokenKind.Symbol, ","),
                (TsqlTokenKind.Number, "0x1F"),
                (TsqlTokenKind.Symbol, ","),
                (TsqlTokenKind.Number, "1.5e-3"),
                (TsqlTokenKind.Symbol, ","),
                (TsqlTokenKind.Parameter, "@p"),
                (TsqlTokenKind.Symbol, ","),
                (TsqlTokenKind.QuotedName, "[q]]r]"),
                (TsqlTokenKind.Symbol, ","),
                (TsqlTokenKind.QuotedName, "\"s\""),
                (TsqlTokenKind.Word, "FROM"),
                (TsqlTokenKind.Word, "t"),
            ]);
    }

    [Fact]
    public void Read_Token_HoldsItsOffsetsInTheSql()
    {
        const string Sql = "SELECT  N'x' AS y";

        var tokens = TsqlTokens.Read(Sql);

        tokens.Select(static token => (token.Start, token.End)).ShouldBe([(0, 6), (8, 12), (13, 15), (16, 17)]);
        tokens.Select(static token => Sql[token.Start..token.End]).ShouldBe(["SELECT", "N'x'", "AS", "y"]);
    }

    [Fact]
    public void Read_CommentsAndHints_AreDropped()
    {
        var tokens = TsqlTokens.Read("SELECT /* c */ 1 /*+ h */ -- d\n, 2");

        tokens.Select(static token => token.Text).ShouldBe(["SELECT", "1", ",", "2"]);
    }

    [Theory]
    [InlineData("<=")]
    [InlineData(">=")]
    [InlineData("<>")]
    [InlineData("!=")]
    [InlineData("!<")]
    [InlineData("!>")]
    public void Read_TwoCharacterOperator_IsOneSymbol(string operation)
    {
        var tokens = TsqlTokens.Read($"a {operation} b");

        tokens
            .Select(static token => (token.Kind, token.Text))
            .ShouldBe([(TsqlTokenKind.Word, "a"), (TsqlTokenKind.Symbol, operation), (TsqlTokenKind.Word, "b")]);
    }

    [Fact]
    public void Read_OtherSymbols_AreOneCharacterEach() =>
        TsqlTokens
            .Read("(a+-b)*=")
            .Select(static token => token.Text)
            .ShouldBe(["(", "a", "+", "-", "b", ")", "*", "="]);

    [Theory]
    [InlineData("@@ROWCOUNT")]
    [InlineData("#t")]
    [InlineData("##t")]
    [InlineData("_a$1#@")]
    [InlineData("Größe")]
    public void Read_Name_IsOneWord(string name) =>
        TsqlTokens.Read(name).ShouldHaveSingleItem().ShouldBe(new TsqlToken(TsqlTokenKind.Word, name, 0, name.Length));

    [Theory]
    [InlineData("1")]
    [InlineData("1.5")]
    [InlineData("1.")]
    [InlineData("1e5")]
    [InlineData("1E+5")]
    [InlineData("0x")]
    [InlineData("0Xab09")]
    public void Read_Number_IsOneNumber(string number) =>
        TsqlTokens.Read(number).ShouldHaveSingleItem().Kind.ShouldBe(TsqlTokenKind.Number);

    // T-SQL reads "1x" as 1 aliased x, so the number ends where its digits do.
    [Fact]
    public void Read_NumberThenLetters_IsANumberThenAWord() =>
        TsqlTokens
            .Read("1x")
            .Select(static token => (token.Kind, token.Text))
            .ShouldBe([(TsqlTokenKind.Number, "1"), (TsqlTokenKind.Word, "x")]);

    [Fact]
    public void Read_NWithAGapBeforeAString_IsAWordThenAString() =>
        TsqlTokens
            .Read("N 'x'")
            .Select(static token => (token.Kind, token.Text))
            .ShouldBe([(TsqlTokenKind.Word, "N"), (TsqlTokenKind.String, "'x'")]);

    [Fact]
    public void Read_LowerCaseN_JoinsTheString() =>
        TsqlTokens.Read("n'x'").ShouldHaveSingleItem().ShouldBe(new TsqlToken(TsqlTokenKind.String, "n'x'", 0, 4));

    // Only a word that is N alone joins: "xN" is a name of its own.
    [Fact]
    public void Read_WordEndingInNBeforeAString_IsNotJoined() =>
        TsqlTokens
            .Read("xN'y'")
            .Select(static token => (token.Kind, token.Text))
            .ShouldBe([(TsqlTokenKind.Word, "xN"), (TsqlTokenKind.String, "'y'")]);

    [Fact]
    public void Read_EmptyText_IsNoToken() => TsqlTokens.Read("  -- only a comment").ShouldBeEmpty();

    [Fact]
    public void Is_WordOfAnyCase_IsTheWord()
    {
        var token = new TsqlToken(TsqlTokenKind.Word, "Select", 0, 6);

        token.Is("SELECT").ShouldBeTrue();
        token.Is("FROM").ShouldBeFalse();
    }

    [Fact]
    public void Is_QuotedName_IsNoWord() =>
        new TsqlToken(TsqlTokenKind.QuotedName, "[SELECT]", 0, 8).Is("[SELECT]").ShouldBeFalse();
}
