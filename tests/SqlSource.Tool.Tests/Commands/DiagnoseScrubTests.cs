using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
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
    // an expected.json in both or in neither, the same explain settings, the same entries in the zip, and the same
    // text of query.sql and of capture.json, compared after each renaming is undone with its own mapping, since
    // each run renames with a key of its own.
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
        using var directZip = await ZipFile.OpenReadAsync(
            _scene.PathOf("direct.zip"),
            TestContext.Current.CancellationToken
        );
        using var scrubbedZip = await ZipFile.OpenReadAsync(
            _scene.PathOf("scrubbed.zip"),
            TestContext.Current.CancellationToken
        );
        scrubbedZip.Entries.Select(e => e.FullName).Order().ShouldBe(directZip.Entries.Select(e => e.FullName).Order());
        foreach (var name in new[] { "query.sql", "capture.json" })
        {
            (await RestoredTextAsync(_scene.PathOf("scrubbed.zip"), scrubbedZip, name)).ShouldBe(
                await RestoredTextAsync(_scene.PathOf("direct.zip"), directZip, name)
            );
        }
    }

    // The text of an entry with each replacement of the bundle's mapping put back to its original, longest first.
    private static async Task<string> RestoredTextAsync(string zipPath, ZipArchive zip, string name)
    {
        await using var stream = await zip.GetEntry(name)!.OpenAsync(TestContext.Current.CancellationToken);
        using var reader = new StreamReader(stream);
        var text = await reader.ReadToEndAsync(TestContext.Current.CancellationToken);
        using var mapping = JsonDocument.Parse(
            await File.ReadAllTextAsync(zipPath + BundleFiles.MappingSuffix, TestContext.Current.CancellationToken)
        );
        foreach (
            var entry in mapping
                .RootElement.GetProperty("names")
                .EnumerateArray()
                .OrderByDescending(e => e.GetProperty("replacement").GetString()!.Length)
        )
        {
            text = text.Replace(
                entry.GetProperty("replacement").GetString()!,
                entry.GetProperty("original").GetString()!,
                StringComparison.Ordinal
            );
        }
        return text;
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
