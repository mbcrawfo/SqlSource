using System.Diagnostics;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Repository;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres;

// tools/build-postgis-image.sh, which CI runs before the tests, builds the image under the name that PostgisContainer
// looks for.  Were the two names to differ, the tests would build the image again themselves, and a failed build would
// show as their errors again, not as the step's.  The script starts no container, so this is in no collection.
public sealed class PostgisImageScriptTests
{
    [Theory]
    [InlineData("postgres:18.6")]
    [InlineData("postgres:14.24")]
    public async Task Name_PostgresImage_IsTheHarnesss(string postgres) =>
        (await NameAsync(postgres)).ShouldBe(PostgisContainer.ImageOf(postgres));

    [Fact]
    public async Task Name_NoImage_IsTheHarnesssOnTheDefaultImage() =>
        (await NameAsync()).ShouldBe(PostgisContainer.ImageOf(DefaultImages.Postgres));

    // The name that the script prints for --name and the arguments given, which builds nothing.
    private static async Task<string> NameAsync(params string[] arguments)
    {
        var start = new ProcessStartInfo(
            "bash",
            [RepositoryRoot.Path("tools", "build-postgis-image.sh"), "--name", .. arguments]
        )
        {
            WorkingDirectory = RepositoryRoot.WorkingFolder.Path,
        };
        var result = await Programs.RunAsync(start, Eventually.Patience);
        result.ExitCode.ShouldBe(0, result.Error);
        return result.Output.Trim();
    }
}
