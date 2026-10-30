using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public sealed class TextLinesTests
{
    // Each of Roslyn's line breaks, a span at the start, at a line's start, across a break and inside a two-character
    // break.
    [Theory]
    [InlineData("a\nb", 0, 1)]
    [InlineData("a\nb", 2, 1)]
    [InlineData("a\nb", 0, 3)]
    [InlineData("a\nb", 1, 1)]
    [InlineData("a\r\nb", 0, 4)]
    [InlineData("a\r\nb", 3, 1)]
    [InlineData("a\r\nb", 2, 0)]
    [InlineData("a\r\nb", 1, 3)]
    [InlineData("a\rb", 0, 3)]
    [InlineData("a\rb", 2, 1)]
    [InlineData("a\u0085b", 0, 3)]
    [InlineData("a\u0085b", 2, 1)]
    [InlineData("a\u2028b", 0, 3)]
    [InlineData("a\u2028b", 2, 1)]
    [InlineData("a\u2029b", 0, 3)]
    [InlineData("a\u2029b", 2, 1)]
    [InlineData("a\n\r\n\rb\n", 0, 7)]
    [InlineData("a\n\r\n\rb\n", 7, 0)]
    [InlineData("x", 0, 1)]
    [InlineData("x", 1, 0)]
    [InlineData("", 0, 0)]
    public void LineSpanOf_IsTheSourceTextsLineSpan(string text, int start, int length)
    {
        var span = new TextSpan(start, length);

        TextLines.LineSpanOf(text, span).ShouldBe(SourceText.From(text).Lines.GetLinePositionSpan(span));
    }
}
