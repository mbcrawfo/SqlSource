using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class UsageValueTests
{
    [Theory]
    [InlineData("App.csproj", true)]
    [InlineData("src/My App/App.csproj", true)]
    [InlineData("", true)]
    [InlineData("Host=db", false)]
    [InlineData("a;b", false)]
    [InlineData("=", false)]
    public void MayBePrintedAsPath_Token_IsTrueOnlyWithoutAnEqualsSignOrASemicolon(string text, bool expected) =>
        new UsageValue(null, text, 2).MayBePrintedAsPath.ShouldBe(expected);

    [Fact]
    public void NamedAsPath_PrintableToken_IsTheFullPathInQuotes() =>
        new UsageValue(null, "App", 2).NamedAsPath("/work/App").ShouldBe("'/work/App'");

    [Fact]
    public void NamedAsPath_FullPathWithALineBreak_IsOnOneLine() =>
        new UsageValue(null, "App", 2).NamedAsPath("/work/a\r\nb/App").ShouldBe("'/work/a b/App'");

    [Fact]
    public void NamedAsPath_TokenWithAnEqualsSign_IsItsPositionAndNothingOfThePath() =>
        new UsageValue(null, "a=b", 7).NamedAsPath("/work/a=b").ShouldBe("The path at position 7");

    // The token decides, so a folder of the working directory that holds "=" is printed.
    [Fact]
    public void NamedAsPath_PrintableTokenInAFolderWithAnEqualsSign_IsTheFullPathInQuotes() =>
        new UsageValue(null, "App", 2).NamedAsPath("/work/a=b/App").ShouldBe("'/work/a=b/App'");
}
