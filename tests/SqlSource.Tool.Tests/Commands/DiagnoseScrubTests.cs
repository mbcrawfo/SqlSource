using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// "diagnose --scrub": a raw bundle that "diagnose --no-obfuscate" wrote, renamed later with no database and no
// project, and written as "diagnose" writes a renamed one.
public sealed class DiagnoseScrubTests : IDisposable
{
    private const string Query = "Users.sql:GetEverything";

    private readonly DiagnoseScene _scene = new();

    public DiagnoseScrubTests()
    {
        _ = _scene.AddCase(_scene.Case("select-plain"));
    }

    public void Dispose() => _scene.Dispose();

    // Writes a raw bundle, then makes any question to a database throw, since a scrub asks none.
    private async Task WriteRawAsync()
    {
        var raw = await _scene.DiagnoseAsync("--query", Query, "--out", "raw.zip", "--no-obfuscate");
        raw.ExitCode.ShouldBe(0, raw.Error);
        _scene.Run.Exchanges = static _ => throw new InvalidOperationException("A scrub asks no database.");
    }

    [Fact]
    public async Task Diagnose_ScrubOfTheRawBundle_ReplaysToReproduced()
    {
        await WriteRawAsync();
        var requests = _scene.Run.Processes.Requests.Count;

        var result = await _scene.DiagnoseAsync("--scrub", "raw.zip", "--out", "report.zip");

        result.ShouldBe(new CliResult(0, _scene.Written("report.zip"), ""));
        _scene.Run.Processes.Requests.Count.ShouldBe(requests);
        BundleReplays.Read(_scene.PathOf("report.zip")).Info.ShouldNotBeNull().Renamed.ShouldBeTrue();
        File.Exists(_scene.PathOf("report.zip" + BundleFiles.MappingSuffix)).ShouldBeTrue();

        var replayed = await _scene.Run.RunAsync("replay", "report.zip");

        replayed.Error.ShouldBeEmpty();
        replayed.ExitCode.ShouldBe(0);
        replayed.Out.ShouldEndWith(DiagnoseScene.Reproduced);
    }

    // The same kind of bundle as diagnose writes directly: the same bundle.json, so renamed and with the same outcome,
    // an expected.json in both or in neither, and the same explain settings.
    [Fact]
    public async Task Diagnose_ScrubOfTheRawBundle_GivesWhatDiagnoseGives()
    {
        _ = await _scene.DiagnoseAsync("--query", Query, "--out", "direct.zip");
        await WriteRawAsync();

        _ = await _scene.DiagnoseAsync("--scrub", "raw.zip", "--out", "scrubbed.zip");

        var direct = BundleReplays.Read(_scene.PathOf("direct.zip"));
        var scrubbed = BundleReplays.Read(_scene.PathOf("scrubbed.zip"));
        scrubbed.Info.ShouldBe(direct.Info);
        (scrubbed.Expected is null).ShouldBe(direct.Expected is null);
        scrubbed.ExplainSettings.ShouldBe(direct.ExplainSettings);
    }

    [Fact]
    public async Task Diagnose_ScrubOfARenamedBundle_IsSQLSRC295()
    {
        _ = await _scene.DiagnoseAsync("--query", Query, "--out", "report.zip");
        var zip = _scene.PathOf("report.zip");

        var result = await _scene.DiagnoseAsync("--scrub", "report.zip", "--out", "again.zip");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{zip} : error SQLSRC295: '{zip}' is not a raw bundle: it is renamed already\n"
                    + Expected.See("SQLSRC295")
            )
        );
        File.Exists(_scene.PathOf("again.zip")).ShouldBeFalse();
    }

    // A case of the fixtures is a folder with no bundle.json, which replay reads.
    [Fact]
    public async Task Diagnose_ScrubOfAFolderWithoutBundleJson_IsSQLSRC295()
    {
        var folder = _scene.Run.Folder.CreateFolder("case");
        foreach (var file in Directory.GetFiles(_scene.Case("select-plain").Folder))
        {
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        }

        var result = await _scene.DiagnoseAsync("--scrub", "case", "--out", "report.zip");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{folder} : error SQLSRC295: '{folder}' is not a raw bundle: it has no bundle.json that says it is "
                    + "raw\n"
                    + Expected.See("SQLSRC295")
            )
        );
        File.Exists(_scene.PathOf("report.zip")).ShouldBeFalse();
    }

    // An error of reading the bundle is replay's.
    [Fact]
    public async Task Diagnose_ScrubOfNothing_IsSQLSRC288()
    {
        var result = await _scene.DiagnoseAsync("--scrub", "missing.zip", "--out", "report.zip");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{_scene.PathOf("missing.zip")} : error SQLSRC288: The bundle cannot be read: it is neither a zip nor a "
                + "folder that holds query.sql and capture.json\n"
        );
    }

    // A bundle's path that holds "=" or ";" is named by its position, as replay names it.
    [Fact]
    public async Task Diagnose_ScrubOfAPathWithAnEqualsSign_NamesItsPosition()
    {
        var result = await _scene.DiagnoseAsync("--scrub", "a=" + Secrets.Word, "--out", "report.zip");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                "sqlsource : error SQLSRC288: The bundle cannot be read: it is neither a zip nor a folder that holds "
                    + "query.sql and capture.json\n"
                    + "    bundle: The path at position 3\n"
                    + Expected.See("SQLSRC288")
            )
        );
    }

    [Fact]
    public async Task Diagnose_ScrubOverASqlFile_IsSQLSRC218()
    {
        await WriteRawAsync();

        var result = await _scene.DiagnoseAsync("--scrub", "raw.zip", "--out", "Users.sql");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"{_scene.PathOf("Users.sql")} : error SQLSRC218: ");
        (await File.ReadAllTextAsync(_scene.PathOf("Users.sql"), TestContext.Current.CancellationToken)).ShouldBe(
            _scene.Case("select-plain").Query
        );
    }
}
