using Microsoft.CodeAnalysis.Text;
using Shouldly;
using SqlSource.Parsing;
using Xunit;

namespace SqlSource.Tests.Parsing;

public sealed class SqlTextTests
{
    [Theory]
    [InlineData("  a b  ", 0, 7, 2, 5)]
    [InlineData("a", 0, 1, 0, 1)]
    [InlineData("\t\r\n x\u00A0", 0, 6, 4, 5)]
    // What is outside the bounds is not looked at.
    [InlineData("xx  a  xx", 2, 7, 4, 5)]
    // Nothing but white space: empty, at the end.
    [InlineData("   ", 0, 3, 3, 3)]
    [InlineData("abc", 1, 1, 1, 1)]
    public void Trim_Bounds_AreMovedInPastTheWhiteSpace(
        string text,
        int start,
        int end,
        int expectedStart,
        int expectedEnd
    ) => SqlText.Trim(text, start, end).ShouldBe(TextSpan.FromBounds(expectedStart, expectedEnd));

    [Theory]
    [InlineData("ab\ncd", 0, 5, 2)]
    [InlineData("ab\r\ncd", 0, 6, 2)]
    [InlineData("ab\rcd", 0, 5, 2)]
    [InlineData("abcd", 0, 4, 4)]
    [InlineData("ab\ncd", 3, 5, 5)]
    [InlineData("ab\ncd\nef", 3, 8, 5)]
    // A terminator at the end of the range, or past it, is outside it.
    [InlineData("ab\ncd", 0, 2, 2)]
    [InlineData("ab\ncd", 0, 1, 1)]
    [InlineData("ab\ncd", 2, 2, 2)]
    [InlineData("ab", 2, 2, 2)]
    // U+2028 ends a line of C#, not a line of SQL.
    [InlineData("ab\u2028cd", 0, 5, 5)]
    public void FindLineEnd_Range_GivesItsFirstTerminatorOrItsEnd(string text, int start, int end, int expected) =>
        SqlText.FindLineEnd(text, start, end).ShouldBe(expected);

    [Theory]
    [InlineData("ab", 0, 'a')]
    [InlineData("ab", 1, 'b')]
    [InlineData("ab", 2, '\0')]
    [InlineData("ab", 9, '\0')]
    [InlineData("", 0, '\0')]
    public void CharAt_Index_GivesTheCharacterOrNulPastTheEnd(string text, int index, char expected) =>
        SqlText.CharAt(text, index).ShouldBe(expected);
}
