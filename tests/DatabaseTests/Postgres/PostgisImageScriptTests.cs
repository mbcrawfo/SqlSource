using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness;
using SqlSource.Repository;
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

    // A checkout with CRLF line endings, which git may give on Windows, names the image that the harness names.  The
    // script takes the repository's root from its own place, so a copy of it runs on copies of the files it reads.
    [Fact]
    public async Task Name_DockerfileWithCrlf_IsTheHarnesss()
    {
        using var copy = new TempFolder();
        string[] harness = ["tests", "DatabaseTests", "TestHarness"];
        var script = copy.WriteFile(
            "tools/build-postgis-image.sh",
            await File.ReadAllTextAsync(
                RepositoryRoot.Path("tools", "build-postgis-image.sh"),
                TestContext.Current.CancellationToken
            )
        );
        foreach (var file in new[] { "DefaultImages.cs", "PostgisContainer.cs" })
        {
            _ = copy.WriteFile(
                $"tests/DatabaseTests/TestHarness/{file}",
                await File.ReadAllTextAsync(
                    RepositoryRoot.Path([.. harness, file]),
                    TestContext.Current.CancellationToken
                )
            );
        }

        var dockerfile = await File.ReadAllTextAsync(
            RepositoryRoot.Path([.. harness, "Postgis", "Dockerfile"]),
            TestContext.Current.CancellationToken
        );
        var crlf = dockerfile
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace("\n", "\r\n", StringComparison.Ordinal);
        crlf.ShouldContain("\r\n");
        _ = copy.WriteFile("tests/DatabaseTests/TestHarness/Postgis/Dockerfile", crlf);

        (await NameAsync(script, copy.Path)).ShouldBe(PostgisContainer.ImageOf(DefaultImages.Postgres));
    }

    // The name that the script prints for --name and the arguments given, which builds nothing.
    private static async Task<string> NameAsync(params string[] arguments) =>
        Succeeded(await PostgisImageScript.RunAsync(["--name", .. arguments]));

    private static async Task<string> NameAsync(string script, string folder) =>
        Succeeded(await PostgisImageScript.RunCopyAsync(script, folder, "--name"));

    private static string Succeeded(ProgramResult result)
    {
        result.ExitCode.ShouldBe(0, result.Error);
        return result.Output.Trim();
    }
}
