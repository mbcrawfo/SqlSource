using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// "describe --manifest" runs on the project that a manifest file describes, and asks MSBuild nothing: the build that
// calls the tool has already evaluated the project and wrote the manifest.
public sealed class ManifestRunTests : IDisposable
{
    private readonly DescribeScene _scene = new();
    private readonly string _invoices;

    public ManifestRunTests()
    {
        _scene.Run.Environment.Clear();
        _invoices = _scene.Project.AddSql("Invoices.sql", SqlTexts.GetInvoice);
    }

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_Manifest_DescribesItsProjectStartsNoProcessAndWritesTheSidecar()
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        var manifest = _scene.WriteManifest();

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
        _ = _scene.WriteManifest();

        var result = await _scene.DescribeAsync("--manifest", "obj/App.csproj.manifest");

        result.ShouldBe(new CliResult(0, "billing (postgres): 1 described, 0 skipped, 0 failed\n", ""));
        _scene.Run.Processes.Requests.ShouldBeEmpty();
    }

    // The .env file read is in the folder of the project the manifest names, not in the manifest's folder.
    [Fact]
    public async Task Run_ManifestAndAConnectionOnlyInTheDotEnvFileOfTheProject_UsesIt()
    {
        _ = _scene.WriteDotEnv(DescribeScene.BillingFromTheFile);
        _ = _scene.Run.Folder.WriteFile("obj/.env", "SQLSOURCE_CONNECTION_BILLING=Host=from-the-manifest-folder\n");
        var manifest = _scene.WriteManifest();

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
        var manifest = _scene.WriteManifest("SqlSourceManifest=1\nProject=/x/App.csproj\n");

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
        var manifest = _scene.WriteManifest();
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

    // The build writes the paths of --manifest, --log and --env-file, and the owner ruled that such a path is no
    // secret, so the log's run.start holds them as they were given under --online, whatever they hold.
    [Theory]
    [InlineData(";")]
    [InlineData("=")]
    public async Task Run_OnlineWithBuildPathsThatHoldASemicolonOrAnEqualsSign_WritesThemInTheLog(string character)
    {
        _scene.Run.Environment["SQLSOURCE_CONNECTION_BILLING"] = Secrets.Connection;
        var manifest = _scene.Run.Folder.WriteFile($"obj/a{character}b.manifest", _scene.Project.ManifestText());
        var log = _scene.Run.Folder.PathOf($"obj/a{character}b.csproj.SqlSource.log");
        var envFile = _scene.Run.Folder.PathOf($"a{character}b.env");
        string[] args = ["describe", "--manifest", manifest, "--online", "auto", "--env-file", envFile, "--log", log];

        var result = await _scene.DescribeAsync(args[1..]);

        result.ShouldBe(
            new CliResult(0, "sqlsource: summary: billing (postgres): 1 described, 0 skipped, 0 failed\n", "")
        );
        LogReader
            .Read(log)[0]
            .GetProperty("arguments")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(args);
    }

    [Theory]
    [InlineData("--manifest", "obj/a;" + Secrets.Word + ".manifest")]
    [InlineData("--manifest", "obj/a=" + Secrets.Word + ".manifest")]
    [InlineData("--log", "obj/a;" + Secrets.Word + ".log")]
    [InlineData("--log", "obj/a=" + Secrets.Word + ".log")]
    [InlineData("--env-file", "a;" + Secrets.Word + ".env")]
    [InlineData("--env-file", "a=" + Secrets.Word + ".env")]
    public async Task Run_PathThatHoldsASemicolonOrAnEqualsSignWithoutOnline_IsRefusedByItsPosition(
        string option,
        string path
    )
    {
        var manifest = _scene.WriteManifest();
        string[] args = option == "--manifest" ? ["--manifest", path] : ["--manifest", manifest, option, path];

        var result = await _scene.DescribeAsync(args);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"sqlsource: the value of option '{option}' at position {args.Length + 1} holds");
        result.Error.ShouldNotContain(Secrets.Word);
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // Under --online a message prints the build's paths in full, whatever they hold, with the system's reason: the
    // owner ruled that a path the build writes is no secret.  The same tokens without --online are refused above.
    [Theory]
    [InlineData("--manifest", ";")]
    [InlineData("--manifest", "=")]
    [InlineData("--log", ";")]
    [InlineData("--log", "=")]
    [InlineData("--env-file", ";")]
    [InlineData("--env-file", "=")]
    public async Task Run_OnlineWithABuildPathThatHoldsASemicolonOrAnEqualsSign_PrintsItInFull(
        string option,
        string character
    )
    {
        var manifest = _scene.WriteManifest();
        var path = _scene.Run.Folder.PathOf($"no-such-folder/a{character}b.{option[2..]}");
        string[] args =
            option == "--manifest"
                ? ["--manifest", path, "--online", "true"]
                : ["--manifest", manifest, "--online", "true", option, path];

        var result = await _scene.DescribeAsync(args);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            option switch
            {
                "--manifest" =>
                    $"{path} : error SQLSRC206: The project manifest '{path}' cannot be read: there is no such file",
                "--log" => $"{path} : error SQLSRC218: '{path}' could not be written: ",
                _ => $"{_invoices}(1,10): error SQLSRC213: No connection is given for the database 'billing'; ",
            }
        );
        result.Error.ShouldNotContain("position");
        result.Error.ShouldNotContain("the system would not");
        if (option == "--env-file")
        {
            result.Error.ShouldContain($"in the environment or in {path}.  The run reads that file alone");
        }
    }

    [Fact]
    public async Task Run_OnlineWithAManifestThatHoldsASemicolonAndCannotBeOpened_IsSqlsrc206WithTheSystemsReason()
    {
        var manifest = _scene.Run.Folder.WriteFile(
            "obj/a;" + Secrets.Word + ".manifest",
            _scene.Project.ManifestText()
        );
        _scene.Run.ReadTextFile = path => throw new IOException($"'{path}' is gone");

        var result = await _scene.DescribeAsync("--manifest", manifest, "--online", "auto");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{manifest} : error SQLSRC206: The project manifest '{manifest}' cannot be read: '{manifest}' is gone"
        );
    }
}
