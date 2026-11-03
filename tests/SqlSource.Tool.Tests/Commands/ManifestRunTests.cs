using System;
using System.IO;
using System.Linq;
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
            $"{manifest} : error SQLSRC206: The project manifest '{manifest}' cannot be read: "
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
            $"{manifest} : error SQLSRC206: The project manifest '{manifest}' cannot be read: "
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
            $"{manifest} : error SQLSRC206: The project manifest '{manifest}' cannot be read: "
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
            $"{manifest} : error SQLSRC206: The project manifest '{manifest}' cannot be read: "
        );
        result.Error.ShouldContain("the disk is gone");
    }

    // The build passes paths of its own obj folder, where a project's name, or the folder, can put "=" or ";".
    // Without --online such a path is refused, since it may be a connection string typed after the option.
    [Theory]
    [InlineData("obj/a;b.csproj.SqlSource.manifest")]
    [InlineData("obj/x=y.csproj.SqlSource.manifest")]
    public async Task Run_OnlineWithAManifestPathThatHoldsASemicolonOrAnEqualsSign_ReadsIt(string relative)
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        var manifest = _scene.Run.Folder.WriteFile(relative, _scene.Project.ManifestText());

        var result = await _scene.DescribeAsync("--manifest", manifest, "--online", "auto");

        result.ShouldBe(
            new CliResult(0, "sqlsource: summary: billing (postgres): 1 described, 0 skipped, 0 failed\n", "")
        );
        File.Exists(SidecarFormat.PathFor(_invoices)).ShouldBeTrue();
    }

    [Fact]
    public async Task Run_OnlineWithALogPathThatHoldsASemicolon_WritesTheLogAndLeavesThePathOutOfIt()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        var manifest = WriteManifest(_scene.Project.ManifestText());
        var log = _scene.Run.Folder.PathOf("obj/a;b.csproj.SqlSource.log");

        var result = await _scene.DescribeAsync("--manifest", manifest, "--online", "auto", "--log", log);

        result.ShouldBe(
            new CliResult(0, "sqlsource: summary: billing (postgres): 1 described, 0 skipped, 0 failed\n", "")
        );
        LogReader
            .Read(log)[0]
            .GetProperty("arguments")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["describe", "--manifest", manifest, "--online", "auto", "--log", "***"]);
    }

    [Theory]
    [InlineData("--manifest", "obj/a;" + Secrets.Word + ".manifest")]
    [InlineData("--manifest", "obj/a=" + Secrets.Word + ".manifest")]
    [InlineData("--log", "obj/a;" + Secrets.Word + ".log")]
    public async Task Run_PathThatHoldsASemicolonOrAnEqualsSignWithoutOnline_IsRefusedByItsPosition(
        string option,
        string path
    )
    {
        var manifest = WriteManifest(_scene.Project.ManifestText());
        string[] args = option == "--log" ? ["--manifest", manifest, "--log", path] : ["--manifest", path];

        var result = await _scene.DescribeAsync(args);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"sqlsource: the value of option '{option}' at position {args.Length + 1} holds");
        result.Error.ShouldNotContain(Secrets.Word);
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // A path that holds "=" or ";" may still be a secret typed by hand, so a message names it by its position, and
    // leaves out the system's reason, which quotes it.
    [Fact]
    public async Task Run_OnlineWithAManifestThatHoldsASemicolonAndDoesNotExist_IsSqlsrc206ByItsPosition()
    {
        var manifest = _scene.Run.Folder.PathOf("obj/a;" + Secrets.Word + ".manifest");

        var result = await _scene.DescribeAsync("--manifest", manifest, "--online", "auto");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            "sqlsource : error SQLSRC206: The project manifest at position 3 cannot be read: there is no such file"
        );
        result.Error.ShouldNotContain(Secrets.Word);
    }

    [Fact]
    public async Task Run_OnlineWithAManifestThatHoldsASemicolonAndCannotBeOpened_LeavesOutTheSystemsReason()
    {
        var manifest = _scene.Run.Folder.WriteFile(
            "obj/a;" + Secrets.Word + ".manifest",
            _scene.Project.ManifestText()
        );
        _scene.Run.ReadTextFile = path => throw new IOException($"'{path}' is gone");

        var result = await _scene.DescribeAsync("--manifest", manifest, "--online", "auto");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            "sqlsource : error SQLSRC206: The project manifest at position 3 cannot be read: the system would not "
                + "read it"
        );
        result.Error.ShouldNotContain(Secrets.Word);
    }

    [Fact]
    public async Task Run_OnlineWithALogThatHoldsASemicolonAndCannotBeMade_IsSqlsrc218ByItsPosition()
    {
        var manifest = WriteManifest(_scene.Project.ManifestText());
        var log = _scene.Run.Folder.PathOf("no-such-folder/a;" + Secrets.Word + ".log");

        var result = await _scene.DescribeAsync("--manifest", manifest, "--online", "auto", "--log", log);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            "sqlsource : error SQLSRC218: The path at position 7 could not be written: the system would not write it"
        );
        result.Error.ShouldNotContain(Secrets.Word);
        _scene.Describer.Opened.ShouldBeEmpty();
    }
}
