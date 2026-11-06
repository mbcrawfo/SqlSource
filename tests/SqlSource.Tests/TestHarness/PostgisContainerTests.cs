using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Repository;
using Xunit;

namespace SqlSource.Tests.TestHarness;

// The name of the PostGIS image, which tools/describe-end-to-end.sh builds under the same name, the Dockerfile's
// default package, which no build reads but which a reader of the file takes for the one the tests run, and the image
// and package of the PostgreSQL example's Dockerfile, which are the tests' default image and package; and the lock that
// makes one build of the image wait for another's.
public sealed class PostgisContainerTests : IDisposable
{
    private readonly string _lock = Path.Combine(Path.GetTempPath(), $"sqlsource-test-{Guid.NewGuid():N}.lock");

    public void Dispose() => File.Delete(_lock);

    [Theory]
    [InlineData("postgres:18.6", "sqlsource-postgis:18.6-3.6.4")]
    [InlineData("postgres:14.24", "sqlsource-postgis:14.24-3.6.4")]
    public void ImageOf_PostgresImage_NamesItsTagAndPostgisVersion(string postgres, string image) =>
        PostgisContainer.ImageOf(postgres).ShouldBe(image);

    [Fact]
    public void Dockerfile_DefaultPackage_IsThePackage() =>
        File.ReadAllLines(RepositoryRoot.Path("tests", "DatabaseTests", "TestHarness", "Postgis", "Dockerfile"))
            .ShouldContain($"ARG POSTGIS={PostgisContainer.Package}");

    [Fact]
    public void ExampleDockerfile_ImageAndPackage_AreTheTests() =>
        File.ReadAllLines(RepositoryRoot.Path("examples", "PostgresExamples", "Postgis", "Dockerfile"))
            .ShouldSatisfyAllConditions(
                lines => lines.ShouldContain($"FROM {DefaultImages.Postgres}"),
                lines => lines.ShouldContain($"ARG POSTGIS={PostgisContainer.Package}")
            );

    // A lock that another handle holds is waited for, and taken once it is let go.  The first try is made before the
    // call returns, so the wait has begun when the other handle lets go.
    [Fact]
    public async Task LockBuildsAsync_LockThatAnotherHandleHolds_IsTakenOnceLetGo()
    {
        var other = new FileStream(_lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var waiting = PostgisContainer.LockBuildsAsync(_lock, TimeSpan.FromMinutes(1));
        waiting.IsCompleted.ShouldBeFalse();
        await other.DisposeAsync();

        await using var taken = await waiting;
        taken.CanWrite.ShouldBeTrue();
    }

    [Fact]
    public async Task LockBuildsAsync_LockHeldForLongerThanThePatience_ThrowsTheTimeout()
    {
        await using var other = new FileStream(_lock, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);

        var thrown = await Should.ThrowAsync<TimeoutException>(() =>
            PostgisContainer.LockBuildsAsync(_lock, TimeSpan.FromMilliseconds(200))
        );

        thrown.Message.ShouldContain(_lock);
    }
}
