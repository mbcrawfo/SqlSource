using System;
using System.IO;
using System.Linq;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// "replay" as a user runs it: a bundle, a zip or a folder, replayed with the released describers and no database.  The
// tests' host reads files only under the temporary folder, so each test copies its case into the run's folder.
public sealed class ReplayTests : IDisposable
{
    private const string Reproduced = "sqlsource: reproduced: the pipeline gives this answer\n";

    private const string Raw = "sqlsource: this bundle is not renamed, and must not be posted\n";

    private static readonly JsonSerializerOptions Relaxed = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    private readonly CliRun _run = new() { ReleasedDescribers = true };

    public void Dispose() => _run.Dispose();

    // Each case of the describers' fixtures as its engine and its name.
    public static TheoryData<string, string> Cases =>
        [
            with(
                FixtureCases
                    .Of("postgres")
                    .Concat(FixtureCases.Of("mssql"))
                    .Select(static found => (found.Engine, found.Name))
            ),
        ];

    // A bundle.json for a case of the fixtures.
    private static BundleInfo InfoOf(string engine, bool renamed = false) => BundleInfos.Sample(engine, renamed);

    // Copies a case of the fixtures into a folder of the run, and gives the folder's full path.
    private string CopyCase(string engine, string name, string folder = "case")
    {
        var source = FixtureCases.Named(engine, name).Folder;
        var target = _run.Folder.CreateFolder(folder);
        foreach (var file in Directory.GetFiles(source))
        {
            File.Copy(file, Path.Combine(target, Path.GetFileName(file)));
        }

        return target;
    }

    // Writes a bundle's files into a folder of the run, and gives the folder's full path.
    private string WriteFolder(Bundle bundle, string folder = "case")
    {
        var target = _run.Folder.CreateFolder(folder);
        if (bundle.Info is { } info)
        {
            File.WriteAllText(Path.Combine(target, BundleFiles.Info), BundleJson.Write(info));
        }

        File.WriteAllText(Path.Combine(target, BundleFiles.Query), bundle.Query);
        File.WriteAllText(Path.Combine(target, BundleFiles.Capture), bundle.Capture);
        if (bundle.Expected is { } expected)
        {
            File.WriteAllText(Path.Combine(target, BundleFiles.Expected), expected);
        }

        return target;
    }

    // A file's text, read and written from a test that awaits the run.
    private static string ReadText(string path) => File.ReadAllText(path);

    private static void WriteText(string path, string text) => File.WriteAllText(path, text);

    // The case's capture.json with every call of the method removed.
    private static void RemoveCalls(string folder, string method)
    {
        var path = Path.Combine(folder, BundleFiles.Capture);
        var capture = CaptureJson.Read(File.ReadAllText(path)).Value.ShouldNotBeNull();
        var cut = new DescribeCapture(capture.Engine);
        foreach (var call in capture.Calls.Where(call => call.Method != method))
        {
            cut.Add(call);
        }

        File.WriteAllText(path, CaptureJson.Write(cut));
    }

    [Theory]
    [MemberData(nameof(Cases))]
    public async Task Replay_EveryFixtureFolder_IsReproduced(string engine, string name)
    {
        _ = CopyCase(engine, name);

        var result = await _run.RunAsync("replay", "case");

        result.Error.ShouldBeEmpty();
        result.ExitCode.ShouldBe(0);
        result.Out.ShouldEndWith(Reproduced);
    }

    [Fact]
    public async Task Replay_ExpectedChanged_IsNotReproducedWithTheDifference()
    {
        var folder = CopyCase("postgres", "select-left-join");
        var path = Path.Combine(folder, BundleFiles.Expected);
        var sidecar = JsonNode.Parse(ReadText(path)).ShouldNotBeNull();
        var columns = sidecar["queries"].ShouldNotBeNull().AsObject().Single().Value.ShouldNotBeNull()["columns"];
        var index = columns
            .ShouldNotBeNull()
            .AsArray()
            .Select((column, at) => (Column: column.ShouldNotBeNull(), At: at))
            .First(static found =>
                found.Column["nullable"]?.GetValueKind() is JsonValueKind.True or JsonValueKind.False
            )
            .At;
        var column = columns[index].ShouldNotBeNull();
        column["nullable"] = !column["nullable"].ShouldNotBeNull().GetValue<bool>();
        WriteText(path, sidecar.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

        var result = await _run.RunAsync("replay", "case");

        result.Error.ShouldBeEmpty();
        result.ExitCode.ShouldBe(2);
        var lines = result.Out.Split('\n');
        lines.ShouldContain("sqlsource: not reproduced");
        lines.ShouldContain(line =>
            line.StartsWith($"sqlsource:   columns[{index}].nullable: ", StringComparison.Ordinal)
        );
        // A folder without bundle.json does not say what made it.
        result.Out.ShouldNotContain("the bundle was made by");
    }

    [Fact]
    public async Task Replay_ExpectedChangedWithBundleJson_SaysWhatMadeItAndWhatReplayedIt()
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain");
        var expected = JsonNode.Parse(bundle.Expected.ShouldNotBeNull()).ShouldNotBeNull();
        var entry = expected["queries"].ShouldNotBeNull().AsObject().Single().Value.ShouldNotBeNull();
        entry["serverVersion"] = "0.1";
        _ = WriteFolder(bundle with { Info = InfoOf("postgres", renamed: true), Expected = expected.ToJsonString() });

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(2);
        var lines = result.Out.TrimEnd('\n').Split('\n');
        lines[^3].ShouldBe("sqlsource: not reproduced");
        lines[^2].ShouldStartWith("sqlsource:   serverVersion: \"0.1\" -> ");
        lines[^1]
            .ShouldBe(
                $"sqlsource: the bundle was made by sqlsource 1.4.0, sidecar format {SidecarFormat.Version}; this is "
                    + $"sqlsource {Cli.Version}, sidecar format {SidecarFormat.Version}"
            );
    }

    // A control character of each kind that a crafted bundle may hold, and how it is printed: C0's escape and bell, a
    // character of C1, and DEL.
    public static TheoryData<string, string> Controls =>
        new()
        {
            { "\u001b", "\\u001B" },
            { "\u0007", "\\u0007" },
            { "\u009b", "\\u009B" },
            { "\u007f", "\\u007F" },
        };

    [Theory]
    [MemberData(nameof(Controls))]
    public async Task Replay_ToolVersionWithAControlCharacter_PrintsItsEscape(string control, string escape)
    {
        var bundle = BundleReplays.OfCase("postgres", "select-plain");
        var expected = JsonNode.Parse(bundle.Expected.ShouldNotBeNull()).ShouldNotBeNull();
        var entry = expected["queries"].ShouldNotBeNull().AsObject().Single().Value.ShouldNotBeNull();
        entry["serverVersion"] = "0.1";
        var info = InfoOf("postgres", renamed: true) with { ToolVersion = $"1.0{control}[3A\t{control}[0J" };
        _ = WriteFolder(bundle with { Info = info, Expected = expected.ToJsonString() });

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(2);
        result.Out.ShouldNotContain(control);
        result
            .Out.TrimEnd('\n')
            .Split('\n')[^1]
            .ShouldStartWith(
                $"sqlsource: the bundle was made by sqlsource 1.0{escape}[3A\t{escape}[0J, sidecar format "
            );
    }

    [Theory]
    [MemberData(nameof(Controls))]
    public async Task Replay_FailureWithAControlCharacter_PrintsItsEscape(string control, string escape)
    {
        _ = WriteFolder(
            BundleReplays.PrepareTypeNotDetermined() with
            {
                Info = InfoOf("postgres") with
                {
                    Renamed = true,
                    Outcome = BundleOutcome.Failure($"SQLSRC231{control}[2J", $"parse\t{control}"),
                },
            }
        );

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(2);
        result.Out.ShouldNotContain(control);
        result
            .Out.Split('\n')
            .ShouldContain(
                $"sqlsource:   expected SQLSRC231{escape}[2J at parse\t{escape}, "
                    + "replayed SQLSRC226 at describe parameters"
            );
    }

    [Fact]
    public async Task Replay_EngineWithEscapeSequences_IsSqlsrc291WithTheirEscapes()
    {
        var folder = CopyCase("postgres", "select-plain");
        var path = Path.Combine(folder, BundleFiles.Capture);
        var capture = JsonNode.Parse(ReadText(path)).ShouldNotBeNull();
        capture["engine"] = "x\u001b]0;pwned\u0007";
        WriteText(path, capture.ToJsonString());

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{folder} : error SQLSRC291: The bundle is of the engine 'x\\u001B]0;pwned\\u0007', which this version of "
                + "sqlsource does not describe\n"
        );
    }

    [Fact]
    public async Task Replay_OutcomeKindWithAnEscapeSequence_IsSqlsrc288WithItsEscape()
    {
        var folder = CopyCase("postgres", "select-plain");
        var info = JsonNode.Parse(BundleJson.Write(InfoOf("postgres", renamed: true))).ShouldNotBeNull();
        info["outcome"].ShouldNotBeNull()["kind"] = "x\u001b[2Jy";
        WriteText(Path.Combine(folder, BundleFiles.Info), info.ToJsonString());

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{folder} : error SQLSRC288: The bundle cannot be read: the kind of the outcome of bundle.json is "
                + "'x\\u001B[2Jy', which is neither 'entry' nor 'failure'\n"
        );
    }

    [Fact]
    public async Task Replay_CaptureWithACallRemoved_IsSqlsrc290()
    {
        var folder = CopyCase("postgres", "select-left-join");
        RemoveCalls(folder, "explain");

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe(
            $"{folder} : error SQLSRC290: The bundle has no answer to 'explain' that the query asks: it was made by "
                + $"another version of sqlsource, and this is sqlsource {Cli.Version}\n"
                + Expected.See("SQLSRC290")
        );
        result.Out.ShouldNotContain("reproduced");
    }

    [Fact]
    public async Task Replay_CaptureWithACallRemovedAndBundleJson_NamesTheVersionThatMadeIt()
    {
        var folder = CopyCase("postgres", "select-left-join");
        RemoveCalls(folder, "explain");
        WriteText(Path.Combine(folder, BundleFiles.Info), BundleJson.Write(InfoOf("postgres", renamed: true)));

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("it was made by sqlsource 1.4.0, and this is sqlsource ");
    }

    // An answer that lacks a member that is not nullable is a question with no answer too, never SQLSRC200.
    [Fact]
    public async Task Replay_AnswerThatCannotBeRead_IsSqlsrc290()
    {
        var folder = CopyCase("postgres", "select-plain");
        var path = Path.Combine(folder, BundleFiles.Capture);
        var capture = JsonNode.Parse(ReadText(path)).ShouldNotBeNull();
        var open = capture["calls"]
            .ShouldNotBeNull()
            .AsArray()
            .Select(static call => call.ShouldNotBeNull())
            .First(static call => call["method"]?.GetValue<string>() == "open");
        open["answer"]
            .ShouldNotBeNull()["settings"]
            .ShouldNotBeNull()[0]
            .ShouldNotBeNull()
            .AsObject()
            .Remove("value")
            .ShouldBeTrue();
        WriteText(path, capture.ToJsonString());

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"{folder} : error SQLSRC290: The bundle has no answer to 'open' that the query asks"
        );
    }

    [Fact]
    public async Task Replay_ZipOfAFixture_GivesWhatTheFolderGives()
    {
        _ = CopyCase("postgres", "select-left-join");
        var bundle = BundleReplays.OfCase("postgres", "select-left-join") with { Info = InfoOf("postgres") };
        using (var zip = File.Create(_run.Folder.PathOf("report.zip")))
        {
            BundleWriter.Write(bundle, zip);
        }

        var folder = await _run.RunAsync("replay", "case");
        var zipped = await _run.RunAsync("replay", "report.zip");

        zipped.ExitCode.ShouldBe(0);
        zipped.Error.ShouldBeEmpty();
        zipped.Out.ShouldStartWith(Raw);
        zipped.Out[Raw.Length..].ShouldBe(folder.Out);
        folder.Out.ShouldEndWith(Reproduced);
    }

    [Fact]
    public async Task Replay_RawBundle_SaysSo()
    {
        var folder = CopyCase("postgres", "select-plain");
        WriteText(Path.Combine(folder, BundleFiles.Info), BundleJson.Write(InfoOf("postgres")));

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldStartWith(Raw);
        result.Out.ShouldEndWith(Reproduced);
    }

    [Fact]
    public async Task Replay_RenamedBundle_SaysNothingOfIt()
    {
        var folder = CopyCase("postgres", "select-plain");
        WriteText(Path.Combine(folder, BundleFiles.Info), BundleJson.Write(InfoOf("postgres", renamed: true)));

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldNotContain(Raw);
    }

    [Fact]
    public async Task Replay_BundleVersionTwo_IsSqlsrc289()
    {
        var folder = CopyCase("postgres", "select-plain");
        WriteText(
            Path.Combine(folder, BundleFiles.Info),
            BundleJson.Write(InfoOf("postgres") with { BundleVersion = 2 })
        );

        var result = await _run.RunAsync("replay", "case");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{folder} : error SQLSRC289: The bundle's bundleVersion is 2, and this version of sqlsource reads 1\n"
                    + Expected.See("SQLSRC289")
            )
        );
    }

    [Fact]
    public async Task Replay_BundleJsonOfANewerSidecarFormat_IsSqlsrc289()
    {
        var folder = CopyCase("postgres", "select-plain");
        WriteText(
            Path.Combine(folder, BundleFiles.Info),
            BundleJson.Write(InfoOf("postgres") with { FormatVersion = SidecarFormat.Version + 1 })
        );

        var result = await _run.RunAsync("replay", "case");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{folder} : error SQLSRC289: The bundle's formatVersion is {SidecarFormat.Version + 1}, and this "
                    + $"version of sqlsource reads {SidecarFormat.Version}\n"
                    + Expected.See("SQLSRC289")
            )
        );
    }

    // A folder without bundle.json, whose expected answer alone says the format.
    [Fact]
    public async Task Replay_ExpectedOfANewerSidecarFormat_IsSqlsrc289()
    {
        var folder = CopyCase("postgres", "select-plain");
        var path = Path.Combine(folder, BundleFiles.Expected);
        var sidecar = JsonNode.Parse(ReadText(path)).ShouldNotBeNull();
        sidecar["formatVersion"] = SidecarFormat.Version + 1;
        WriteText(path, sidecar.ToJsonString());

        var result = await _run.RunAsync("replay", "case");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{folder} : error SQLSRC289: The bundle's formatVersion is {SidecarFormat.Version + 1}, and this "
                    + $"version of sqlsource reads {SidecarFormat.Version}\n"
                    + Expected.See("SQLSRC289")
            )
        );
    }

    [Fact]
    public async Task Replay_NothingAtThePath_IsSqlsrc288()
    {
        var result = await _run.RunAsync("replay", "missing.zip");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{_run.Folder.PathOf("missing.zip")} : error SQLSRC288: The bundle cannot be read: it is neither a "
                    + "zip nor a folder that holds query.sql and capture.json\n"
                    + Expected.See("SQLSRC288")
            )
        );
    }

    [Fact]
    public async Task Replay_EngineWithNoDescriber_IsSqlsrc291()
    {
        var folder = CopyCase("postgres", "select-plain");
        var path = Path.Combine(folder, BundleFiles.Capture);
        var capture = JsonNode.Parse(ReadText(path)).ShouldNotBeNull();
        capture["engine"] = "db2";
        WriteText(path, capture.ToJsonString());

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe(
            $"{folder} : error SQLSRC291: The bundle is of the engine 'db2', which this version of sqlsource does not "
                + "describe\n"
                + Expected.See("SQLSRC291")
        );
    }

    [Fact]
    public async Task Replay_NoExpectedAnswer_PrintsWhatItGives()
    {
        var folder = CopyCase("postgres", "select-left-join");
        var expectedPath = Path.Combine(folder, BundleFiles.Expected);
        var entry = JsonNode.Parse(ReadText(expectedPath)).ShouldNotBeNull()["queries"].ShouldNotBeNull();
        File.Delete(expectedPath);
        // The entry it gives is this tool's.
        entry["GetUserOrders"].ShouldNotBeNull()["toolVersion"] = PackageVersion.Prefix;

        var result = await _run.RunAsync("replay", "case");

        result.Error.ShouldBeEmpty();
        result.ExitCode.ShouldBe(0);
        result.Out.ShouldEndWith($"sqlsource: replayed: {entry.ToJsonString(Relaxed)}\n");
    }

    [Fact]
    public async Task Replay_NoExpectedAnswerAndARefusal_PrintsTheIdAndTheStep()
    {
        _ = WriteFolder(BundleReplays.PrepareTypeNotDetermined());

        var result = await _run.RunAsync("replay", "case");

        result.Error.ShouldBeEmpty();
        result.ExitCode.ShouldBe(0);
        result.Out.ShouldEndWith("sqlsource: replayed: SQLSRC226 at describe parameters\n");
    }

    [Fact]
    public async Task Replay_RefusalThatBundleJsonExpects_IsReproduced()
    {
        _ = WriteFolder(
            BundleReplays.PrepareTypeNotDetermined() with
            {
                Info = InfoOf("postgres") with
                {
                    Renamed = true,
                    Outcome = BundleOutcome.Failure("SQLSRC226", "describe parameters"),
                },
            }
        );

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldEndWith(Reproduced);
    }

    [Fact]
    public async Task Replay_OtherRefusalThanBundleJsonExpects_IsNotReproduced()
    {
        _ = WriteFolder(
            BundleReplays.PrepareTypeNotDetermined() with
            {
                Info = InfoOf("postgres") with
                {
                    Renamed = true,
                    Outcome = BundleOutcome.Failure("SQLSRC231", step: null),
                },
            }
        );

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(2);
        var lines = result.Out.TrimEnd('\n').Split('\n');
        lines[^3].ShouldBe("sqlsource: not reproduced");
        lines[^2].ShouldBe("sqlsource:   expected SQLSRC231, replayed SQLSRC226 at describe parameters");
    }

    [Fact]
    public async Task Replay_EntryExpectedAndARefusalReplayed_IsNotReproduced()
    {
        var described = BundleReplays.OfCase("postgres", "update-inferred");
        _ = WriteFolder(BundleReplays.PrepareTypeNotDetermined() with { Expected = described.Expected });

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(2);
        result
            .Out.Split('\n')
            .ShouldContain("sqlsource:   expected an entry, replayed SQLSRC226 at describe parameters");
    }

    [Fact]
    public async Task Replay_Log_WritesTheDescribersEvents()
    {
        _ = CopyCase("postgres", "select-left-join");

        var result = await _run.RunAsync("replay", "case", "--log", "run.log");

        result.ExitCode.ShouldBe(0);
        var events = LogReader.Read(_run.Folder.PathOf("run.log"));
        LogReader.EventOf(events[0]).ShouldBe("run.start");
        events[0]
            .GetProperty("arguments")
            .EnumerateArray()
            .Select(static argument => argument.GetString())
            .ShouldBe(["replay", "case", "--log", "run.log"]);
        events.Select(LogReader.EventOf).ShouldContain("postgres.walk.node");
        LogReader.EventOf(events[^1]).ShouldBe("run.end");
        events[^1].GetProperty("exitCode").GetInt32().ShouldBe(0);
    }

    [Fact]
    public async Task Replay_LogFromTheVariable_IsWritten()
    {
        _ = CopyCase("postgres", "select-plain");
        _run.Environment["SQLSOURCE_LOG"] = "run.log";

        var result = await _run.RunAsync("replay", "case");

        result.ExitCode.ShouldBe(0);
        LogReader.Names(_run.Folder.PathOf("run.log"))[^1].ShouldBe("run.end");
    }

    [Fact]
    public async Task Replay_ErrorWithALog_IsAnEventBeforeRunEnd()
    {
        var folder = CopyCase("postgres", "select-left-join");
        RemoveCalls(folder, "explain");

        var result = await _run.RunAsync("replay", "case", "--log", "run.log");

        result.ExitCode.ShouldBe(1);
        var events = LogReader.Read(_run.Folder.PathOf("run.log"));
        events[^2].GetProperty("id").GetString().ShouldBe("SQLSRC290");
        events[^1].GetProperty("exitCode").GetInt32().ShouldBe(1);
    }

    [Fact]
    public async Task Replay_LogOverASqlFile_IsRefusedAndNothingRuns()
    {
        _ = CopyCase("postgres", "select-plain");

        var result = await _run.RunAsync("replay", "case", "--log", "Q.sql");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"{_run.Folder.PathOf("Q.sql")} : error SQLSRC218: ");
        result.Out.ShouldBeEmpty();
        File.Exists(_run.Folder.PathOf("Q.sql")).ShouldBeFalse();
    }

    [Fact]
    public async Task Replay_Failed_HasNoLineThatPointsToALog()
    {
        var result = await _run.RunAsync("replay", "missing");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain(Cli.RunAgainLine);
    }

    [Fact]
    public async Task Replay_WithoutVerbose_PrintsOnlyTheDecisions()
    {
        _ = CopyCase("postgres", "select-left-join");

        var result = await _run.RunAsync("replay", "case");

        var events = result
            .Out.Split('\n')
            .Where(static line => line.StartsWith("sqlsource: postgres.", StringComparison.Ordinal));
        events.ShouldNotBeEmpty();
        events.ShouldAllBe(static line =>
            line.StartsWith("sqlsource: postgres.walk.", StringComparison.Ordinal)
            || line.StartsWith("sqlsource: postgres.table-match:", StringComparison.Ordinal)
        );
    }

    [Fact]
    public async Task Replay_Verbose_PrintsEveryEvent()
    {
        _ = CopyCase("postgres", "select-left-join");

        var quiet = await _run.RunAsync("replay", "case");
        var verbose = await _run.RunAsync("replay", "case", "--verbose");

        verbose.ExitCode.ShouldBe(0);
        verbose.Out.ShouldEndWith(Reproduced);
        verbose.Out.Split('\n').Length.ShouldBeGreaterThan(quiet.Out.Split('\n').Length);
        verbose
            .Out.Split('\n')
            .ShouldContain(static line =>
                line.StartsWith("sqlsource: postgres.", StringComparison.Ordinal)
                && !line.StartsWith("sqlsource: postgres.walk.", StringComparison.Ordinal)
                && !line.StartsWith("sqlsource: postgres.table-match:", StringComparison.Ordinal)
            );
    }

    [Fact]
    public async Task Replay_PathWithEqualsSign_NamesItsPosition()
    {
        var result = await _run.RunAsync("replay", "a=" + Secrets.Word);

        result.ShouldBe(
            new CliResult(
                1,
                "",
                "sqlsource : error SQLSRC288: The bundle cannot be read: it is neither a zip nor a folder that holds "
                    + "query.sql and capture.json\n"
                    + "    bundle: The path at position 2\n"
                    + Expected.See("SQLSRC288")
            )
        );
    }

    // The system's reason quotes the path it could not read, and the message names that path by its position.
    [Fact]
    public async Task Replay_PathWithASemicolonThatCannotBeRead_HasNoPartOfItInTheReason()
    {
        _ = _run.Folder.CreateFolder("a;" + Secrets.Word);
        _run.ReadTextFile = static path => throw new IOException($"Could not read '{path}'.");

        var result = await _run.RunAsync("replay", "a;" + Secrets.Word);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain(Secrets.Word);
        result.Error.ShouldStartWith(
            "sqlsource : error SQLSRC288: The bundle cannot be read: Could not read 'The path at position 2"
        );
    }

    // The reason quotes the path with its control character escaped, and the message still names it by its position.
    [Fact]
    public async Task Replay_PathWithASemicolonAndAnEscapeThatCannotBeRead_HasNoPartOfItInTheReason()
    {
        _run.ReadTextFile = static path => throw new IOException($"Could not read '{path}'.");

        var result = await _run.RunAsync("replay", "a;\u001b" + Secrets.Word);

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain(Secrets.Word);
        result.Error.ShouldStartWith("sqlsource : error SQLSRC288: The bundle cannot be read: ");
        result.Error.ShouldContain("'The path at position 2");
    }

    [Fact]
    public async Task Replay_PathWithEqualsSignAndALog_LogsNoPartOfIt()
    {
        var result = await _run.RunAsync("replay", "a=" + Secrets.Word, "--log", "run.log");

        result.ExitCode.ShouldBe(1);
        ReadText(_run.Folder.PathOf("run.log")).ShouldNotContain(Secrets.Word);
    }

    [Fact]
    public async Task Replay_PathWithSpaces_Reads()
    {
        _ = CopyCase("postgres", "select-plain", "my bug report");

        var result = await _run.RunAsync("replay", "my bug report");

        result.Error.ShouldBeEmpty();
        result.ExitCode.ShouldBe(0);
        result.Out.ShouldEndWith(Reproduced);
    }

    [Fact]
    public async Task Replay_TwoBundles_IsAWrongCommandLine()
    {
        var result = await _run.RunAsync("replay", "a.zip", "b.zip");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 3\n"));
    }

    [Fact]
    public async Task Replay_NoBundle_IsAWrongCommandLine()
    {
        var result = await _run.RunAsync("replay");

        result.ShouldBe(new CliResult(1, "", "sqlsource: command 'replay' needs the path of a bundle\n"));
    }

    [Fact]
    public async Task Replay_LogPathWithAnEqualsSign_IsAWrongCommandLine()
    {
        var result = await _run.RunAsync("replay", "case", "--log", "a=" + Secrets.Word);

        result.ShouldBe(
            new CliResult(
                1,
                "",
                "sqlsource: the value of option '--log' at position 4 holds '=' or ';', which a log's path may not\n"
            )
        );
    }

    [Fact]
    public async Task Help_ListsReplay()
    {
        var result = await _run.RunAsync("--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("replay");
        result.Out.ShouldContain("Runs the pipeline from a bug-report bundle with no database");
    }

    [Fact]
    public async Task Help_OfReplay_ListsItsOptions()
    {
        var result = await _run.RunAsync("replay", "--help");

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
        result.Out.ShouldContain(
            "Print every event of the replay, not only the decisions of the walk and the table match"
        );
        result.Out.ShouldContain("--log");
    }
}
