using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// "describe --manifest" runs on the project that a manifest file describes, and asks MSBuild nothing: the build that
// calls the tool has already evaluated the project and wrote the manifest.
public sealed class ManifestRunTests : IDisposable
{
    private const string Invoices = "-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private readonly DescribeScene _scene = new();
    private readonly string _invoices;

    public ManifestRunTests()
    {
        _scene.Run.Environment.Clear();
        _invoices = _scene.Project.AddSql("Invoices.sql", Invoices);
    }

    public void Dispose() => _scene.Dispose();

    // In a folder of its own, so that the manifest's folder is not the project's.
    private string WriteManifest(string text) => _scene.Run.Folder.WriteFile("obj/App.csproj.manifest", text);

    [Fact]
    public async Task Run_Manifest_DescribesItsProjectStartsNoProcessAndWritesTheSidecar()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        var manifest = WriteManifest(_scene.Project.ManifestText());

        var result = await _scene.DescribeAsync("--manifest", manifest);

        result.ShouldBe(new CliResult(0, "billing (postgres): 1 described, 0 skipped, 0 failed\n", ""));
        _scene.Run.Processes.Requests.ShouldBeEmpty();
        File.Exists(SidecarFormat.PathFor(_invoices)).ShouldBeTrue();
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.Connection);
    }

    // The run's working directory is the test's folder, so a relative path resolves against it.
    [Fact]
    public async Task Run_ManifestGivenRelativeToTheWorkingDirectory_IsRead()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        _ = WriteManifest(_scene.Project.ManifestText());

        var result = await _scene.DescribeAsync("--manifest", "obj/App.csproj.manifest");

        result.ShouldBe(new CliResult(0, "billing (postgres): 1 described, 0 skipped, 0 failed\n", ""));
        _scene.Run.Processes.Requests.ShouldBeEmpty();
    }

    // The .env file is searched from the folder of the project the manifest names, not from the manifest's folder.
    [Fact]
    public async Task Run_ManifestAndAConnectionOnlyInTheDotEnvFileOfTheProject_UsesIt()
    {
        _ = _scene.Run.Folder.WriteFile(".env", "SQLSOURCE_CONNECTION_BILLING=Host=from-the-file\n");
        _ = _scene.Run.Folder.WriteFile("obj/.env", "SQLSOURCE_CONNECTION_BILLING=Host=from-the-manifest-folder\n");
        var manifest = WriteManifest(_scene.Project.ManifestText());

        var result = await _scene.DescribeAsync("--manifest", manifest);

        result.ShouldBe(new CliResult(0, "billing (postgres): 1 described, 0 skipped, 0 failed\n", ""));
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=from-the-file");
    }

    [Fact]
    public async Task Run_ManifestThatDoesNotExist_IsSqlsrc206AtItsPath()
    {
        var manifest = _scene.Run.Folder.PathOf("obj/missing.manifest");

        var result = await _scene.DescribeAsync("--manifest", manifest);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe("");
        result.Error.ShouldStartWith(
            $"{manifest} : error SQLSRC206: The project manifest of '{manifest}' cannot be read: "
        );
        _scene.Run.Processes.Requests.ShouldBeEmpty();
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ManifestOfAnotherFormat_IsSqlsrc206AtItsPath()
    {
        var manifest = WriteManifest("SqlSourceManifest=1\nProject=/x/App.csproj\n");

        var result = await _scene.DescribeAsync("--manifest", manifest);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{manifest} : error SQLSRC206: The project manifest of '{manifest}' cannot be read: "
        );
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ManifestThatIsAFolder_IsSqlsrc206AtItsPath()
    {
        var manifest = _scene.Run.Folder.CreateFolder("obj");

        var result = await _scene.DescribeAsync("--manifest", manifest);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{manifest} : error SQLSRC206: The project manifest of '{manifest}' cannot be read: "
        );
    }

    [Fact]
    public async Task Run_ManifestThatCannotBeOpened_IsSqlsrc206AtItsPath()
    {
        var manifest = WriteManifest(_scene.Project.ManifestText());
        _scene.Run.ReadTextFile = _ => throw new IOException("the disk is gone");

        var result = await _scene.DescribeAsync("--manifest", manifest);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{manifest} : error SQLSRC206: The project manifest of '{manifest}' cannot be read: "
        );
        result.Error.ShouldContain("the disk is gone");
    }
}
