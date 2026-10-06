using Microsoft.CodeAnalysis.CSharp;
using Shouldly;
using SqlSource.Generation;
using Xunit;

namespace SqlSource.Tests.Generation;

public class LanguageSupportTests
{
    [Theory]
    [InlineData(LanguageVersion.CSharp7_3, "7.3")]
    [InlineData(LanguageVersion.CSharp8, "8.0")]
    [InlineData(LanguageVersion.CSharp9, "9.0")]
    [InlineData(LanguageVersion.CSharp10, "10.0")]
    [InlineData(LanguageVersion.CSharp11, "11.0")]
    public void FindUnsupportedVersion_OlderThanCSharp12_IsTheVersionAsLangVersionWritesIt(
        LanguageVersion version,
        string expected
    ) => LanguageSupport.FindUnsupportedVersion(new CSharpParseOptions(version)).ShouldBe(expected);

    // Default, LatestMajor and Latest are what a project writes; the compiler turns each into the version it stands
    // for, which is newer than C# 12 on the compiler these tests run on.
    [Theory]
    [InlineData(LanguageVersion.CSharp12)]
    [InlineData(LanguageVersion.CSharp13)]
    [InlineData(LanguageVersion.Preview)]
    [InlineData(LanguageVersion.Default)]
    [InlineData(LanguageVersion.LatestMajor)]
    [InlineData(LanguageVersion.Latest)]
    public void FindUnsupportedVersion_CSharp12OrLater_IsNull(LanguageVersion version) =>
        LanguageSupport.FindUnsupportedVersion(new CSharpParseOptions(version)).ShouldBeNull();
}
