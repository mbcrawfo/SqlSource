using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class UsageValueTests
{
    [Theory]
    [InlineData("App.csproj", false)]
    [InlineData("", false)]
    [InlineData("Host=db", true)]
    [InlineData("a;b", true)]
    [InlineData("=", true)]
    public void HoldsConnectionText_Token_IsTrueOnlyWithAnEqualsSignOrASemicolon(string text, bool expected) =>
        new UsageValue(null, text, 2).HoldsConnectionText.ShouldBe(expected);

    // A value that nothing judged is held back.
    [Fact]
    public void MayBePrintedAsPath_ValueNotJudged_IsFalse() =>
        new UsageValue(null, "App.csproj", 2).MayBePrintedAsPath.ShouldBeFalse();

    [Fact]
    public void NamedAsPath_FullPathWithALineBreak_IsOnOneLine() =>
        new UsageValue(null, "App", 2) { MayBePrintedAsPath = true }
            .NamedAsPath("/work/a\r\nb/App")
            .ShouldBe("'/work/a b/App'");

    [Fact]
    public void NamedAsPath_TokenThatMayNotBePrinted_IsItsPositionAndNothingOfThePath() =>
        new UsageValue(null, "S3cret", 7).NamedAsPath("/work/S3cret").ShouldBe("The path at position 7");

    // The token decides, so a folder of the working directory that holds "=" is printed.
    [Fact]
    public void NamedAsPath_PrintableTokenInAFolderWithAnEqualsSign_IsTheFullPathInQuotes() =>
        new UsageValue(null, "App", 2) { MayBePrintedAsPath = true }
            .NamedAsPath("/work/a=b/App")
            .ShouldBe("'/work/a=b/App'");
}
