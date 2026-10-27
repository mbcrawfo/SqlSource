using System;
using System.Linq;
using System.Text.RegularExpressions;
using Shouldly;
using SqlSource.Settings.TypeOverrides;
using Xunit;

namespace SqlSource.Tests.Settings.TypeOverrides;

public sealed partial class TypeKeyTextTests
{
    [Theory]
    [InlineData("U&\"a\\000Ab\"", "a\nb")]
    [InlineData("u&\"\\\\\"", "\\")]
    [InlineData("U&\"\\+01F600\"", "\U0001F600")]
    [InlineData("U&\"\\D83D\\DE00\"", "\U0001F600")]
    [InlineData("U&\"\\D83D\\+00DE00\"", "\U0001F600")]
    [InlineData("U&\"a\"\"b\"", "a\"b")]
    [InlineData("U&\"plain\"", "plain")]
    [InlineData("U&\"\\00e9\"", "\u00E9")]
    [InlineData("U&\"\\+00D83D\\DE00\"", "\U0001F600")]
    public void TextOf_EscapedPart_IsItsTextDecoded(string part, string expected) =>
        TypeKeyText.TextOf(part.AsSpan()).ToString().ShouldBe(expected);

    // TextOf is given only parts TrySplit read, but nothing a user writes may make the generator throw, so a \ that
    // is not an escape is a character.
    [Theory]
    [InlineData("U&\"\\\"", "\\")]
    [InlineData("U&\"\\+12\"", "\\+12")]
    [InlineData("U&\"\\D800\"", "\\D800")]
    public void TextOf_EscapedPartThatIsNotWellFormed_KeepsTheBackslashAsACharacter(string part, string expected) =>
        TypeKeyText.TextOf(part.AsSpan()).ToString().ShouldBe(expected);

    [Theory]
    [InlineData("\"a.b\".c[]", "\"a.b\"", "c", true)]
    [InlineData("U&\"a.b\".c[]", "U&\"a.b\"", "c", true)]
    [InlineData("int4", "", "int4", false)]
    [InlineData("s.\"x[]\"", "s", "\"x[]\"", false)]
    [InlineData("s.\"\"\"\"", "s", "\"\"\"\"", false)]
    public void TrySplit_WrittenKey_GivesItsParts(string key, string schema, string name, bool isArray)
    {
        TypeKeyText
            .TrySplit(key.AsSpan(), out var actualSchema, out var actualName, out var actualIsArray)
            .ShouldBeTrue();

        actualSchema.ToString().ShouldBe(schema);
        actualName.ToString().ShouldBe(name);
        actualIsArray.ShouldBe(isArray);
    }

    [Theory]
    [InlineData("")]
    [InlineData("a.b.c")]
    [InlineData("\"a\"b")]
    [InlineData("a[][]")]
    public void TrySplit_AnythingElse_IsFalse(string key) =>
        TypeKeyText.TrySplit(key.AsSpan(), out _, out _, out _).ShouldBeFalse();

    [Theory]
    [InlineData("\"say \"\"hi\"\"\"", "say \"hi\"")]
    [InlineData("\"my schema\"", "my schema")]
    [InlineData("plain", "plain")]
    [InlineData("\"\"\"\"", "\"")]
    public void TextOf_QuotedPart_IsItsTextUndoubled(string part, string expected) =>
        TypeKeyText.TextOf(part.AsSpan()).ToString().ShouldBe(expected);

    [Theory]
    [InlineData("\"x\"", true)]
    [InlineData("U&\"x\"", true)]
    [InlineData("u&\"x\"", true)]
    [InlineData("x", false)]
    public void IsQuoted_Part_IsWhetherItHasQuotes(string part, bool expected) =>
        TypeKeyText.IsQuoted(part.AsSpan()).ShouldBe(expected);

    [Theory]
    [InlineData("status", "status")]
    [InlineData("user_status$1", "user_status$1")]
    [InlineData("Status", "\"Status\"")]
    [InlineData("my schema", "\"my schema\"")]
    [InlineData("a\"b", "\"a\"\"b\"")]
    [InlineData("", "\"\"")]
    [InlineData("a\nb", "U&\"a\\000Ab\"")]
    [InlineData("a\r\nb", "U&\"a\\000D\\000Ab\"")]
    [InlineData("a\tb", "U&\"a\\0009b\"")]
    [InlineData("a  b", "U&\"a \\0020b\"")]
    [InlineData("a\u00A0b", "U&\"a\\00A0b\"")]
    [InlineData("a;b#c", "U&\"a\\003Bb\\0023c\"")]
    [InlineData("a\u0001b", "U&\"a\\0001b\"")]
    [InlineData("a\0\nb", "U&\"a\0\\000Ab\"")]
    [InlineData("say \"hi\"\n\\", "U&\"say \"\"hi\"\"\\000A\\\\\"")]
    [InlineData("a\\b", "\"a\\b\"")]
    [InlineData("a b", "\"a b\"")]
    public void WritePart_Text_IsQuotedWhenItNeedsIt(string text, string expected) =>
        TypeKeyText.WritePart(text).ShouldBe(expected);

    [Theory]
    [InlineData("\"a.b\".c[]")]
    [InlineData("s.\"say \"\"hi\"\"\"")]
    [InlineData("s.U&\"a\\000Ab\"")]
    [InlineData("s.U&\"x \\0020y;\"")]
    public void WritePart_PartOfAKey_ReadsBackAsItsText(string key)
    {
        TypeKeyText.TrySplit(key.AsSpan(), out var schema, out var name, out _).ShouldBeTrue();

        var text = TypeKeyText.TextOf(schema.IsEmpty ? name : schema).ToString();

        TypeKeyText.TextOf(TypeKeyText.WritePart(text).AsSpan()).ToString().ShouldBe(text);
    }

    // What the trim and a line would change, and what the compiler's cut of an MSBuild value did, is not in the
    // written part.
    [Theory]
    [InlineData("a\nb")]
    [InlineData("a\tb")]
    [InlineData("a  b")]
    [InlineData("a;b")]
    [InlineData("x\u2028y")]
    [InlineData("\uD800\n")]
    public void WritePart_TextThatNeedsTheEscape_SurvivesTheTrimAndALine(string text)
    {
        var part = TypeKeyText.WritePart(text);

        WhitespaceRunPattern().Replace(part, " ").ShouldBe(part);
        part.Split(';')[0].Split('#')[0].ShouldBe(part);
        part.Where(char.IsWhiteSpace).ShouldAllBe(character => character == ' ');
        part.ShouldNotContain("  ");
        part.ShouldNotContain(";");
        part.ShouldNotContain("#");
        part.Any(char.IsControl).ShouldBeFalse();
        TypeKeyText.TextOf(part.AsSpan()).ToString().ShouldBe(text);
    }

    // A NUL stays in the written part, which the escaped form cannot write, and is read back.
    [Fact]
    public void WritePart_TextWithANul_IsReadBackAsItsText() =>
        TypeKeyText.TextOf(TypeKeyText.WritePart("a\0\nb").AsSpan()).ToString().ShouldBe("a\0\nb");

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRunPattern();
}
