using Shouldly;
using Xunit;

namespace SqlSource.Tests;

public class PackageVersionTests
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
}
