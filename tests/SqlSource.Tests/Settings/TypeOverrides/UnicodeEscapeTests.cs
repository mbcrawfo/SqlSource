using System;
using Shouldly;
using SqlSource.Settings.TypeOverrides;
using Xunit;

namespace SqlSource.Tests.Settings.TypeOverrides;

public sealed class UnicodeEscapeTests
{
    [Theory]
    [InlineData("\\000A", '\n', 5)]
    [InlineData("\\\\", '\\', 2)]
    [InlineData("\\00e9", 'é', 5)]
    [InlineData("\\+000041", 'A', 8)]
    [InlineData("\\0041b", 'A', 5)]
    [InlineData("\\0001", '\u0001', 5)]
    [InlineData("\\FFFF", '\uFFFF', 5)]
    [InlineData("\\00ff", 'ÿ', 5)]
    public void TryRead_EscapeOfACharacterOfTheBmp_IsThatCharacterAlone(string text, char expected, int next)
    {
        UnicodeEscape.TryRead(text.AsSpan(), 0, out var first, out var second, out var after).ShouldBeTrue();

        first.ShouldBe(expected);
        second.ShouldBe('\0');
        after.ShouldBe(next);
    }

    // A character outside the BMP is one escape or the escapes of its two surrogates, a high one first.
    [Theory]
    [InlineData("\\+01F600", '\uD83D', '\uDE00', 8)]
    [InlineData("\\D83D\\DE00", '\uD83D', '\uDE00', 10)]
    [InlineData("\\D83D\\+00DE00", '\uD83D', '\uDE00', 13)]
    [InlineData("\\+00D83D\\DE00", '\uD83D', '\uDE00', 13)]
    [InlineData("\\+010000", '\uD800', '\uDC00', 8)]
    [InlineData("\\D800\\DC00", '\uD800', '\uDC00', 10)]
    [InlineData("\\+10FFFF", '\uDBFF', '\uDFFF', 8)]
    [InlineData("\\DBFF\\DFFF", '\uDBFF', '\uDFFF', 10)]
    public void TryRead_EscapeOfACharacterOutsideTheBmp_IsItsSurrogates(string text, char high, char low, int next)
    {
        UnicodeEscape.TryRead(text.AsSpan(), 0, out var first, out var second, out var after).ShouldBeTrue();

        first.ShouldBe(high);
        second.ShouldBe(low);
        after.ShouldBe(next);
    }

    [Fact]
    public void TryRead_EscapeAfterTheStart_IsReadFromWhereItIs()
    {
        UnicodeEscape.TryRead("ab\\0041".AsSpan(), 2, out var first, out _, out var next).ShouldBeTrue();

        first.ShouldBe('A');
        next.ShouldBe(7);
    }

    [Theory]
    [InlineData("x")]
    [InlineData("\\")]
    [InlineData("\\0000")]
    [InlineData("\\00")]
    [InlineData("\\00G1")]
    [InlineData("\\x")]
    [InlineData("\\+11FFFF")]
    [InlineData("\\+00006")]
    [InlineData("\\D800")]
    [InlineData("\\DC00")]
    [InlineData("\\DFFF")]
    [InlineData("\\DE00\\D83D")]
    [InlineData("\\D83Dx")]
    [InlineData("\\D83D\\D83D")]
    public void TryRead_AnythingElse_IsFalse(string text) =>
        UnicodeEscape.TryRead(text.AsSpan(), 0, out _, out _, out _).ShouldBeFalse();
}
