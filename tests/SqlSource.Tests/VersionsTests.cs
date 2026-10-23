using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public sealed class VersionsTests
{
    [Theory]
    [InlineData("18.4", 18)]
    [InlineData("13.12", 13)]
    [InlineData("9.6.24", 9)]
    [InlineData("18beta1", 18)]
    [InlineData("16.0.1000.6", 16)]
    [InlineData("7.0.0-preview4", 7)]
    [InlineData("6", 6)]
    [InlineData("devel", null)]
    [InlineData("preview", null)]
    [InlineData("", null)]
    [InlineData("  14", null)]
    [InlineData("99999999999.0.0", null)]
    [InlineData(null, null)]
    public void LeadingMajor_Version_IsItsLeadingDigits(string? version, int? expected) =>
        Versions.LeadingMajor(version).ShouldBe(expected);
}
