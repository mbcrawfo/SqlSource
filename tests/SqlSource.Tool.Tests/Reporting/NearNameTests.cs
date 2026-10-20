using Shouldly;
using SqlSource.Tool.Reporting;
using Xunit;

namespace SqlSource.Tool.Tests.Reporting;

public sealed class NearNameTests
{
    [Theory]
    [InlineData("--conection", "--connection")]
    [InlineData("--force", "--force")]
    [InlineData("--forse", "--force")]
    // One transposition of neighbours counts as one edit.
    [InlineData("--conncetion", "--connection")]
    public void IsNear_NameWithinTheDistance_IsTrue(string given, string known) =>
        NearName.IsNear(given, known, ignoreCase: false).ShouldBeTrue();

    [Theory]
    [InlineData("-pS3cret", "--project")]
    [InlineData("-s3cret", "-h")]
    // Two names that differ by more than the distance in length are far apart without being compared.
    [InlineData("--connection", "-c")]
    [InlineData("-", "--project")]
    public void IsNear_NameBeyondTheDistance_IsFalse(string given, string known) =>
        NearName.IsNear(given, known, ignoreCase: false).ShouldBeFalse();

    [Fact]
    public void IsNear_DifferenceOfCase_IsNearOnlyWhenCaseIsIgnored()
    {
        NearName.IsNear("BILING", "billing", ignoreCase: true).ShouldBeTrue();
        NearName.IsNear("BILING", "billing", ignoreCase: false).ShouldBeFalse();
    }

    [Fact]
    public void IsNear_UnrelatedName_IsFalseWhenCaseIsIgnored() =>
        NearName.IsNear("Host", "postgres", ignoreCase: true).ShouldBeFalse();
}
