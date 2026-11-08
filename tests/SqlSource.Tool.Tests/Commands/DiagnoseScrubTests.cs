using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using SqlSource.Tool.Testing.Fixtures;
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
        WrittenBundles.Read(_scene.PathOf("report.zip")).Info.ShouldNotBeNull().Renamed.ShouldBeTrue();
        File.Exists(_scene.PathOf("report.zip" + BundleFiles.MappingSuffix)).ShouldBeTrue();

        var replayed = await _scene.Run.RunAsync("replay", "report.zip");

        replayed.Error.ShouldBeEmpty();
        replayed.ExitCode.ShouldBe(0);
        replayed.Out.ShouldEndWith(DiagnoseCommands.Reproduced);
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

        var direct = WrittenBundles.Read(_scene.PathOf("direct.zip"));
        var scrubbed = WrittenBundles.Read(_scene.PathOf("scrubbed.zip"));
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

    // A raw bundle that another version made, whose capture does not answer what this version asks, is replay's
    // SQLSRC290 and is not renamed: here SQL Server's parameters request of a reused parameter without its reused
    // record, as a version before that record wrote it.
    [Fact]
    public async Task Diagnose_ScrubOfABundleThatAnotherVersionMade_IsSQLSRC290()
    {
        using var run = new CliRun { ReleasedDescribers = true };
        var folder = await OlderRawBundleAsync(run, "update-copies-agree");
        await EditAsync(
            Path.Combine(folder, BundleFiles.Capture),
            static capture =>
                capture["calls"]
                    .ShouldNotBeNull()
                    .AsArray()
                    .Select(static call => call.ShouldNotBeNull())
                    .Single(static call => call["method"]?.GetValue<string>() == "parameters")["request"]
                    .ShouldNotBeNull()
                    .AsObject()
                    .Remove("reused")
                    .ShouldBeTrue()
        );

        var result = await run.RunAsync("diagnose", "--scrub", "old", "--out", "report.zip");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{folder} : error SQLSRC290: The bundle has no answer to 'parameters' that the query asks: it was "
                    + $"made by sqlsource 1.4.0, and this is sqlsource {Cli.Version}\n"
                    + Expected.See("SQLSRC290")
            )
        );
        File.Exists(run.Folder.PathOf("report.zip")).ShouldBeFalse();
    }

    // A raw bundle that another version made, whose answer this version does not reproduce from the same capture, is
    // SQLSRC296 and is not renamed, since the renaming's proof would refuse it as a fault of the renaming: here an
    // older describer's answer that the first column can be null.
    [Fact]
    public async Task Diagnose_ScrubOfABundleThatThisVersionDoesNotReproduce_IsSQLSRC296()
    {
        using var run = new CliRun { ReleasedDescribers = true };
        var folder = await OlderRawBundleAsync(run, "select-left-join");
        await EditAsync(
            Path.Combine(folder, BundleFiles.Expected),
            static expected =>
                expected["queries"]
                    .ShouldNotBeNull()["UsersWithOrders"]
                    .ShouldNotBeNull()["columns"]
                    .ShouldNotBeNull()[0]
                    .ShouldNotBeNull()["nullable"] = true
        );

        var result = await run.RunAsync("diagnose", "--scrub", "old", "--out", "report.zip");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{folder} : error SQLSRC296: This version of sqlsource does not reproduce the bundle's answer, so it "
                    + "is not renamed: columns[0].nullable: true -> false; it was made by sqlsource 1.4.0, and this "
                    + $"is sqlsource {Cli.Version}\n"
                    + Expected.See("SQLSRC296")
            )
        );
        File.Exists(run.Folder.PathOf("report.zip")).ShouldBeFalse();
    }

    // A case of SQL Server's fixtures copied into the folder "old" of the run, as a raw bundle that sqlsource 1.4.0
    // made; gives the folder's full path.
    private static async Task<string> OlderRawBundleAsync(CliRun run, string name)
    {
        var folder = run.Folder.CreateFolder("old");
        foreach (var file in Directory.GetFiles(FixtureCases.Named("mssql", name).Folder))
        {
            File.Copy(file, Path.Combine(folder, Path.GetFileName(file)));
        }

        var info = BundleInfos.Sample("mssql") with { ServerVersion = "17.0.1000.7" };
        await File.WriteAllTextAsync(
            Path.Combine(folder, BundleFiles.Info),
            BundleJson.Write(info),
            TestContext.Current.CancellationToken
        );
        return folder;
    }

    // A JSON file of a bundle, edited.
    private static async Task EditAsync(string path, Action<JsonNode> edit)
    {
        var node = JsonNode
            .Parse(await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken))
            .ShouldNotBeNull();
        edit(node);
        await File.WriteAllTextAsync(path, node.ToJsonString(), TestContext.Current.CancellationToken);
    }
}
