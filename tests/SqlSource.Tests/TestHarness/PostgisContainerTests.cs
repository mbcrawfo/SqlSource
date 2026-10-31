using System.IO;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Testing;
using Xunit;

namespace SqlSource.Tests.TestHarness;

// The name of the PostGIS image, which tools/describe-end-to-end.sh builds under the same name, and the Dockerfile's
// default package, which no build reads but which a reader of the file takes for the one the tests run.
public sealed class PostgisContainerTests
{
    [Theory]
    [InlineData("postgres:18.6", "sqlsource-postgis:18.6-3.6.4")]
    [InlineData("postgres:14.24", "sqlsource-postgis:14.24-3.6.4")]
    public void ImageOf_PostgresImage_NamesItsTagAndPostgisVersion(string postgres, string image) =>
        PostgisContainer.ImageOf(postgres).ShouldBe(image);

    [Fact]
    public void Dockerfile_DefaultPackage_IsThePackage() =>
        File.ReadAllLines(RepositoryRoot.Path("tests", "DatabaseTests", "TestHarness", "Postgis", "Dockerfile"))
            .ShouldContain($"ARG POSTGIS={PostgisContainer.Package}");
}
