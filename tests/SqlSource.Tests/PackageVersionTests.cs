using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public sealed class PackageVersionTests
{
    [Fact]
    public void Prefix_IsThreeNumbers() => PackageVersion.Prefix.ShouldMatch(@"^[0-9]+\.[0-9]+\.[0-9]+$");

    // The assembly's version is VersionPrefix and the run number, so no suffix can reach the value.
    [Fact]
    public void Prefix_IsTheStartOfTheAssemblysVersion() =>
        typeof(SqlSourceGenerator)
            .Assembly.GetName()
            .Version.ShouldNotBeNull()
            .ToString()
            .ShouldStartWith(PackageVersion.Prefix + ".");

    [Theory]
    [InlineData("0.1.0-dev")]
    [InlineData("0.1.0-pr-42")]
    [InlineData("0.1.0+abc")]
    [InlineData("0.1.0")]
    public void PrefixOf_Version_IsTheTextBeforeTheFirstDashOrPlus(string version) =>
        PackageVersion.PrefixOf(version).ShouldBe("0.1.0");
}
