using Shouldly;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Tests;

public class MSBuildPropertyTests
{
    [Theory]
    [InlineData("/work/App", "/work/App")]
    [InlineData("C:\\work\\App\\", "C:\\work\\App\\")]
    [InlineData("/work/Acme, Inc/", "/work/Acme%2C Inc/")]
    [InlineData("/work/a;b/", "/work/a%3Bb/")]
    [InlineData("/work/100%/", "/work/100%25/")]
    // The percent sign first: "%3B" as a user typed it is three characters, and must come back as three.
    [InlineData("/work/%3B;,/", "/work/%253B%3B%2C/")]
    [InlineData("", "")]
    public void Escape_Value_IsWhatMSBuildReadsBackWhole(string value, string expected) =>
        MSBuildProperty.Escape(value).ShouldBe(expected);

    [Fact]
    public void Switch_NameAndValue_IsOneArgument() =>
        MSBuildProperty.Switch("SolutionDir", "/work/Acme, Inc/").ShouldBe("-p:SolutionDir=/work/Acme%2C Inc/");
}
