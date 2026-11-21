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
    [InlineData("ba", "ab")]
    [InlineData("--cnonectoin", "--connection")]
    [InlineData("badc", "abcd")]
    // The distance is 2 and no more.
    [InlineData("--forxx", "--force")]
    // Names that differ in length by exactly the distance are still compared.
    [InlineData("--for", "--force")]
    public void IsNear_NameWithinTheDistance_IsTrue(string given, string known) =>
        NearName.IsNear(given, known, ignoreCase: false).ShouldBeTrue();

    [Theory]
    [InlineData("-pS3cret", "--project")]
    [InlineData("-s3cret", "-h")]
    [InlineData("--xxxce", "--force")]
    // Every character counts, from the start of the known name to the end of the given one.
    [InlineData("-x", "-abc")]
    [InlineData("--forxxx", "--force")]
    // Two characters that match only one way round are not a swap.
    [InlineData("--oxre", "--force")]
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
        NearName.IsNear("Host", "user", ignoreCase: true).ShouldBeFalse();
}
