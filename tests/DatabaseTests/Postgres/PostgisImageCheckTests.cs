using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using Xunit;

namespace SqlSource.DatabaseTests.Postgres;

// The check that each CI job runs after its tests: tools/build-postgis-image.sh --id gives the ID of the PostGIS image,
// which the job keeps after its build step, and --unchanged fails when the image has another ID, as it has when the
// tests built it again.  It is in UsesPostgis, whose PostgisDatabases has the image of the run's postgres image.
[Collection(UsesPostgis.Name)]
public sealed class PostgisImageCheckTests
{
    private static string Image => EngineImage.Postgres.Image;

    [Fact]
    public async Task Unchanged_IdThatIdGives_Succeeds()
    {
        var id = await PostgisImageScript.RunAsync("--id", Image);
        id.ExitCode.ShouldBe(0, id.Error);
        id.Output.Trim().ShouldStartWith("sha256:");

        var unchanged = await PostgisImageScript.RunAsync("--unchanged", id.Output.Trim(), Image);

        unchanged.ExitCode.ShouldBe(0, unchanged.Error);
    }

    [Fact]
    public async Task Unchanged_AnotherId_FailsSayingTheTestsBuiltTheImage()
    {
        var unchanged = await PostgisImageScript.RunAsync("--unchanged", "sha256:0", Image);

        unchanged.ExitCode.ShouldBe(1);
        unchanged.Error.ShouldContain("built the PostGIS image again");
    }

    [Fact]
    public async Task Unchanged_ImageThatDoesNotExist_Fails()
    {
        var unchanged = await PostgisImageScript.RunAsync("--unchanged", "sha256:0", "postgres:0.0");

        unchanged.ExitCode.ShouldBe(1);
        unchanged.Error.ShouldContain("has no image");
    }
}
