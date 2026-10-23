using Microsoft.CodeAnalysis.Text;
using Shouldly;
using Xunit;

namespace SqlSource.Tests.Parsing;

public sealed class TextSpansTests
{
    [Fact]
    public void Of_ValueTwice_IsTheFirst() => TextSpans.Of("ab ab", "ab").ShouldBe(new TextSpan(0, 2));

    [Fact]
    public void Of_ValueTwiceAndLast_IsTheLast() =>
        TextSpans.Of("ab ab", "ab", last: true).ShouldBe(new TextSpan(3, 2));
}
