using Shouldly;
using SqlSource.Generation.MSBuild;
using Xunit;

namespace SqlSource.Tests.Generation.MSBuild;

public sealed class InvalidSettingTests
{
    [Fact]
    public void Help_TypeOverrideWithAKeyInBrackets_ShowsTheFormsThatAreRead() =>
        new InvalidSetting(MSBuildSettings.TypeOverridesName, "[dbo].[Email]=MyApp.Email").Help.ShouldBe(
            ".  A key is not written in brackets: write dbo.Email, or \"dbo\".\"Email\" to match its case exactly"
        );

    [Theory]
    [InlineData("SqlSourceTypeOverrides", "timestampz=int")]
    [InlineData("SqlSourceOutput", "[dbo].[Email]=MyApp.Email")]
    [InlineData("SqlSourceGeneratorParameters", "[x]")]
    public void Help_AnyOtherValue_IsEmpty(string name, string value) =>
        new InvalidSetting(name, value).Help.ShouldBeEmpty();

    // The help follows from the value, so it is no part of what makes two settings one.
    [Fact]
    public void Equals_SameNameAndValue_IsTrue() =>
        new InvalidSetting(MSBuildSettings.TypeOverridesName, "[a]=b").ShouldBe(
            new InvalidSetting(MSBuildSettings.TypeOverridesName, "[a]=b")
        );
}
