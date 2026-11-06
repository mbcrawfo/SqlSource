using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Generation.Paths;
using SqlSource.Snapshot;
using SqlSource.Tool.Bundles;
using SqlSource.Tool.Bundles.Renaming.Postgres;
using SqlSource.Tool.Describing.Capture;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Bundles;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// "diagnose --query" as a user runs it: one query of a project described again by the engine's real describer,
// answered from a case of the describers' fixtures, and its bundle written, renamed and proved, or raw.
public sealed class DiagnoseTests : IDisposable
{
    private const string Plain = "select-plain";

    private const string Query = "Users.sql:GetEverything";

    private readonly DiagnoseScene _scene = new();

    public void Dispose() => _scene.Dispose();

    private static string ReadText(string path) => File.ReadAllText(path);

    [Fact]
    public async Task Diagnose_AQuery_WritesARenamedBundleAndItsMapping()
    {
        var fixture = _scene.Case(Plain);
        _ = _scene.AddCase(fixture);

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "report.zip");

        result.ShouldBe(new CliResult(0, _scene.Written("report.zip"), ""));
        var bundle = BundleReplays.Read(_scene.PathOf("report.zip"));
        bundle.Info.ShouldNotBeNull().Renamed.ShouldBeTrue();
        BundleLeaks.Of(bundle, [fixture.Query], PostgresWords.IsEngineWord).ShouldBeEmpty();
        using var mapping = JsonDocument.Parse(ReadText(_scene.PathOf("report.zip.mapping.json")));
        mapping.RootElement.GetProperty("mappingVersion").GetInt32().ShouldBe(1);
        mapping
            .RootElement.GetProperty("names")
            .EnumerateArray()
            .Select(static name => name.GetProperty("original").GetString())
            .ShouldContain("GetEverything");

        var replayed = await _scene.Run.RunAsync("replay", "report.zip");

        replayed.Error.ShouldBeEmpty();
        replayed.ExitCode.ShouldBe(0);
        replayed.Out.ShouldEndWith(DiagnoseScene.Reproduced);
    }

    [Fact]
    public async Task Diagnose_NoObfuscate_WritesARawBundleAndNoMapping()
    {
        _ = _scene.AddCase(_scene.Case(Plain));

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "raw.zip", "--no-obfuscate");

        result.ShouldBe(new CliResult(0, _scene.WrittenRaw("raw.zip"), ""));
        File.Exists(_scene.PathOf("raw.zip.mapping.json")).ShouldBeFalse();
        var bundle = BundleReplays.Read(_scene.PathOf("raw.zip"));
        bundle.Info.ShouldNotBeNull().Renamed.ShouldBeFalse();
        bundle.Query.ShouldContain("-- name: GetEverything\n");

        var replayed = await _scene.Run.RunAsync("replay", "raw.zip");

        replayed.ExitCode.ShouldBe(0);
        replayed.Out.ShouldStartWith("sqlsource: this bundle is not renamed, and must not be posted\n");
        replayed.Out.ShouldEndWith(DiagnoseScene.Reproduced);
    }

    // The bundle of a query that the describer refuses is a report too: bundle.json holds the failure, and the run
    // says it without the error, which the user saw when describe ran.
    [Fact]
    public async Task Diagnose_ARefusedQuery_WritesItsFailure()
    {
        var refused = BundleReplays.PrepareTypeNotDetermined();
        _ = _scene.Project.AddSql(DiagnoseScene.File, refused.Query);
        _scene.AnswerWith(BundleReplays.CaptureOf(refused));

        var result = await _scene.DiagnoseAsync("--query", "Users.sql:RenameUser", "--out", "report.zip");

        result.ShouldBe(
            new CliResult(0, "sqlsource: the query was refused: SQLSRC226\n" + _scene.Written("report.zip"), "")
        );
        result.Error.ShouldNotContain(Cli.RunAgainLine);
        var bundle = BundleReplays.Read(_scene.PathOf("report.zip"));
        bundle.Info.ShouldNotBeNull().Outcome.ShouldBe(BundleOutcome.Failure("SQLSRC226", "describe parameters"));
        bundle.Expected.ShouldBeNull();

        var replayed = await _scene.Run.RunAsync("replay", "report.zip");

        replayed.ExitCode.ShouldBe(0);
        replayed.Out.ShouldEndWith(DiagnoseScene.Reproduced);
    }

    // The bundle needs the server's version, and the capture holds nothing of the query.  The port 1 of this machine
    // refuses at once.
    [Fact]
    public async Task Diagnose_ASessionThatDoesNotOpen_WritesNothing()
    {
        _ = _scene.AddCase(_scene.Case(Plain));
        _scene.Run.Exchanges = null;
        _scene.Run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = "Host=127.0.0.1;Port=1;Timeout=5;Password=x1y2z3w4";

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "report.zip");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldContain("error SQLSRC224: ");
        result.Error.ShouldContain("    server: Npgsql could not reach the server: ConnectionRefused");
        File.Exists(_scene.PathOf("report.zip")).ShouldBeFalse();
    }

    // Only the named query is described: the capture answers nothing of the others.  The file's sidecar is left as it
    // was, and no other is written.
    [Fact]
    public async Task Diagnose_AFileOfSeveralQueries_DescribesTheOneAndWritesNoSidecar()
    {
        var fixture = _scene.Case(Plain);
        var path = _scene.AddCase(
            fixture,
            "-- name: First\nSELECT 1;\n\n" + fixture.Query + "\n-- name: Last\nSELECT 2;\n"
        );
        var sidecar = SidecarFormat.PathFor(path);
        var text = SidecarFiles.Empty(toolVersion: "0.0.1");
        await File.WriteAllTextAsync(sidecar, text, TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(sidecar, SidecarFiles.Old);

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "raw.zip", "--no-obfuscate");

        result.ShouldBe(new CliResult(0, _scene.WrittenRaw("raw.zip"), ""));
        ReadText(sidecar).ShouldBe(text);
        File.GetLastWriteTimeUtc(sidecar).ShouldBe(SidecarFiles.Old);
        Directory
            .GetFiles(_scene.Run.Folder.Path, "*" + SqlPath.SidecarExtension, SearchOption.AllDirectories)
            .ShouldBe([sidecar]);
        var query = BundleReplays.Read(_scene.PathOf("raw.zip")).Query;
        query.ShouldContain("-- name: GetEverything\n");
        query.ShouldNotContain("First");
        query.ShouldNotContain("Last");
    }

    // An error of the plan at another .sql file of the project cannot change the query: the bundle is written, the run
    // exits 0 and prints no error, and the error is in the log, as describe prints it.  One row for each error the plan
    // gives at a claimed file.  Wrong.sql comes after Users.sql, so the database has the query's dialect and SQLSRC211
    // is at Wrong.sql.
    [Theory]
    [InlineData("SQLSRC101", "-- name: Other\nSELECT 'open\n", false)]
    [InlineData("SQLSRC103", "-- name: 1bad\nSELECT 1;\n", false)]
    [InlineData("SQLSRC209", "-- dialect: ansi\n-- name: Other\nSELECT 1;\n", false)]
    [InlineData("SQLSRC210", "-- name: Other\nSELECT 1 FROM {{t}};\n", false)]
    [InlineData("SQLSRC211", "-- dialect: mssql\n-- database: postgres\n-- name: Other\nSELECT 1;\n", false)]
    [InlineData("SQLSRC271", "-- name: Other\nSELECT 'caf\u00e9';\n", true)]
    public async Task Diagnose_AnotherFileWithAnErrorOfThePlan_WritesTheBundleAndLogsTheError(
        string id,
        string text,
        bool latin1
    )
    {
        _ = _scene.AddCase(_scene.Case(Plain));
        var wrong = latin1
            ? _scene.Project.AddSqlBytes("Wrong.sql", Encoding.Latin1.GetBytes(text))
            : _scene.Project.AddSql("Wrong.sql", text);

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "report.zip", "--log", "run.log");

        result.ShouldBe(new CliResult(0, _scene.Written("report.zip"), ""));
        BundleReplays.Read(_scene.PathOf("report.zip")).Info.ShouldNotBeNull().Renamed.ShouldBeTrue();
        var logged = LogReader.Events(_scene.PathOf("run.log"), "diagnostic").ShouldHaveSingleItem();
        logged.GetProperty("id").GetString().ShouldBe(id);
        logged.GetProperty("path").GetString().ShouldBe(wrong);
    }

    // An error at the query's own file can change the query: it is printed, and the run stops.
    [Fact]
    public async Task Diagnose_AnErrorOfThePlanAtTheQuerysFile_IsPrintedAndWritesNoBundle()
    {
        var fixture = _scene.Case(Plain);
        var path = _scene.AddCase(fixture, fixture.Query + "\n-- name: 1bad\nSELECT 1;\n");

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "report.zip");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{path}(");
        result.Error.ShouldContain(": error SQLSRC103: ");
        File.Exists(_scene.PathOf("report.zip")).ShouldBeFalse();
    }

    // Any other error written before the query is described may change the query, so the run stops: no bundle, no
    // line of one, and exit 1, with the error printed.  One row for each kind of place: another query of the query's
    // own file, a settings file, a C# file, another project of the solution, and the .env file.
    [Theory]
    [InlineData("SQLSRC210", "Users.sql")]
    [InlineData("SQLSRC016", "sqlsource.json")]
    [InlineData("SQLSRC208", "Others.cs")]
    [InlineData("SQLSRC220", "Tools/Tools.csproj")]
    [InlineData("SQLSRC286", ".env")]
    public async Task Diagnose_AnErrorThatMayChangeTheQuery_IsPrintedAndWritesNoBundle(string id, string at)
    {
        var fixture = _scene.Case(Plain);
        _ = _scene.AddCase(fixture);
        string[] unit = [];
        switch (at)
        {
            case DiagnoseScene.File:
                _ = _scene.AddCase(fixture, fixture.Query + "\n-- name: Other\nSELECT 1 FROM {{t}};\n");
                break;
            case "sqlsource.json":
                _scene.Project.Settings["nonsense"] = "x";
                break;
            case "Others.cs":
                _ = _scene.Project.AddSource(at, Plans.TypeNamed("Others", "Source = Folder"));
                break;
            case "Tools/Tools.csproj":
                var tools = _scene.Run.Folder.WriteFile(at, "<Project />");
                _scene.Run.Processes.Projects[tools] = new FakeProject
                {
                    Imported = "",
                    ProjectAssetsFile = _scene.PathOf("Tools/obj/missing.assets.json"),
                };
                _ = _scene.Run.Folder.WriteFile(
                    "All.slnx",
                    "<Solution><Project Path=\"App.csproj\" /><Project Path=\"Tools/Tools.csproj\" /></Solution>"
                );
                unit = ["All.slnx"];
                break;
            default:
                _ = _scene.Run.Folder.WriteFile(at, "SQLSOURCE_CONNECTION_OTHER=\"Host=db\n");
                break;
        }

        var result = await _scene.DiagnoseAsync([.. unit, "--query", Query, "--out", "report.zip"]);

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith(_scene.PathOf(at));
        result.Error.ShouldContain($" error {id}: ");
        File.Exists(_scene.PathOf("report.zip")).ShouldBeFalse();
        File.Exists(_scene.PathOf("report.zip" + BundleFiles.MappingSuffix)).ShouldBeFalse();
    }

    // A name is compared exactly, as the generator compares them.
    [Fact]
    public async Task Diagnose_ANameInAnotherCase_IsSQLSRC292()
    {
        var path = _scene.AddCase(_scene.Case(Plain));

        var result = await _scene.DiagnoseAsync("--query", "Users.sql:getEverything", "--out", "report.zip");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{path} : error SQLSRC292: There is no query 'getEverything' to diagnose in '{path}': the file has no "
                    + "query of that name\n"
                    + Expected.See("SQLSRC292")
            )
        );
        File.Exists(_scene.PathOf("report.zip")).ShouldBeFalse();
    }

    // A path that holds "=" or ";" is named by its position, as describe names a .sql path.
    [Fact]
    public async Task Diagnose_ANameNotInAFileWhosePathHoldsAnEqualsSign_IsSQLSRC292AtItsPosition()
    {
        _ = _scene.Project.AddSource("Others.cs", Plans.TypeNamed("Others", "Source = \"a=b\""));
        _ = _scene.Project.AddSql("a=b/Users.sql", "-- name: GetUser\nSELECT 1;\n");

        var result = await _scene.DiagnoseAsync("--query", "a=b/Users.sql:Nope", "--out", "report.zip");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                "sqlsource : error SQLSRC292: There is no query 'Nope' to diagnose in 'The path at position 3': the "
                    + "file has no query of that name\n"
                    + Expected.See("SQLSRC292")
            )
        );
    }

    [Fact]
    public async Task Diagnose_AnSqlOutputQuery_IsSQLSRC292()
    {
        var path = _scene.Project.AddSql(DiagnoseScene.File, "-- name: Plain\n-- output: sql\nSELECT 1;\n");

        var result = await _scene.DiagnoseAsync("--query", "Users.sql:Plain", "--out", "report.zip");

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{path} : error SQLSRC292: There is no query 'Plain' to diagnose in '{path}': the query's output is "
                    + "sql, which needs no description\n"
                    + Expected.See("SQLSRC292")
            )
        );
    }

    // A file that no type claims is describe's error for a .sql path.
    [Fact]
    public async Task Diagnose_AFileThatNoTypeClaims_IsSQLSRC212()
    {
        _ = _scene.AddCase(_scene.Case(Plain));

        var result = await _scene.DiagnoseAsync("--query", "Other.sql:GetEverything", "--out", "report.zip");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith(
            $"sqlsource : error SQLSRC212: '{_scene.PathOf("Other.sql")}' is not a .sql file that a type of the run "
                + "claims\n"
        );
    }

    [Theory]
    [InlineData("report.SQL.JSON")]
    [InlineData("App.csproj")]
    public async Task Diagnose_OutIsAFileOfTheRun_IsSQLSRC218(string name)
    {
        _ = _scene.AddCase(_scene.Case(Plain));
        var before = File.Exists(_scene.PathOf(name)) ? ReadText(_scene.PathOf(name)) : null;

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", name);

        var path = _scene.PathOf(name);
        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{path} : error SQLSRC218: '{path}' could not be written: a bundle is not written over a .sql file, "
                    + "a sidecar, a project or a solution\n"
                    + Expected.See("SQLSRC218")
            )
        );
        (File.Exists(path) ? ReadText(path) : null).ShouldBe(before);
    }

    [Fact]
    public async Task Diagnose_OutEndsInSql_IsSQLSRC218()
    {
        _ = _scene.AddCase(_scene.Case(Plain));

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "report.sql");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"{_scene.PathOf("report.sql")} : error SQLSRC218: ");
        File.Exists(_scene.PathOf("report.sql")).ShouldBeFalse();
    }

    [Fact]
    public async Task Diagnose_OutInAFolderThatDoesNotExist_IsSQLSRC218()
    {
        _ = _scene.AddCase(_scene.Case(Plain));
        var path = _scene.PathOf("missing/report.zip");

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "missing/report.zip");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBeEmpty();
        result.Error.ShouldStartWith($"{path} : error SQLSRC218: '{path}' could not be written: ");
        Directory.Exists(_scene.PathOf("missing")).ShouldBeFalse();
    }

    // The zip is written, and then its mapping cannot be: the error is at the mapping, which the user needs to read
    // what comes back.
    [Fact]
    public async Task Diagnose_AMappingThatCannotBeWritten_IsSQLSRC218AtIt()
    {
        _ = _scene.AddCase(_scene.Case(Plain));
        var mapping = _scene.PathOf("report.zip.mapping.json");
        _ = Directory.CreateDirectory(mapping);

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "report.zip");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe($"sqlsource: bundle '{_scene.PathOf("report.zip")}' written\n");
        result.Error.ShouldStartWith($"{mapping} : error SQLSRC218: '{mapping}' could not be written: ");
    }

    // A setting PostgreSQL's renaming has no rule for: its name is no new name, no word of the engine's and no number.
    [Fact]
    public async Task Diagnose_ATokenThatCannotBeMadeSafe_IsSQLSRC293WithThePrivateRoute()
    {
        var fixture = _scene.Case(Plain);
        _ = _scene.AddCase(fixture);
        var capture = JsonNode.Parse(fixture.Capture).ShouldNotBeNull();
        var open = capture["calls"]!.AsArray().First(static call => call!["method"]!.GetValue<string>() == "open")!;
        open["answer"]!["settings"]!.AsArray().Add(new JsonObject { ["name"] = "work_mem", ["value"] = "4MB" });
        _scene.AnswerWith(CaptureJson.Read(capture.ToJsonString()).Value.ShouldNotBeNull());

        var result = await _scene.DiagnoseAsync("--query", Query, "--out", "report.zip");

        var zip = _scene.PathOf("report.zip");
        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"{zip} : error SQLSRC293: The bundle cannot be made safe to post: a word is not one of the new names, "
                    + "at capture.json call 0 'open' answer.settings[].value, found work_mem\n"
                    + DiagnoseScene.PrivateRoute
                    + Expected.See("SQLSRC293")
            )
        );
        File.Exists(zip).ShouldBeFalse();
        File.Exists(zip + BundleFiles.MappingSuffix).ShouldBeFalse();
    }

    // SQL Server's describer and renaming, as PostgreSQL's above.
    [Fact]
    public async Task Diagnose_ASqlServerQuery_WritesARenamedBundleThatReplays()
    {
        using var scene = new DiagnoseScene("mssql");
        _ = scene.AddCase(scene.Case("select-left-join"));
        var name = scene.Case("select-left-join").Query.Split('\n')[0]["-- name: ".Length..].Trim();

        var result = await scene.DiagnoseAsync("--query", "Users.sql:" + name, "--out", "report.zip");

        result.ShouldBe(new CliResult(0, scene.Written("report.zip"), ""));
        BundleReplays.Read(scene.PathOf("report.zip")).Info.ShouldNotBeNull().Engine.ShouldBe("mssql");

        var replayed = await scene.Run.RunAsync("replay", "report.zip");

        replayed.ExitCode.ShouldBe(0);
        replayed.Out.ShouldEndWith(DiagnoseScene.Reproduced);
    }

    [Fact]
    public async Task Diagnose_Help_ListsTheOptions()
    {
        var result = await _scene.DiagnoseAsync("--help");

        result.ExitCode.ShouldBe(0);
        foreach (
            var option in new[]
            {
                "--project",
                "--connection",
                "--query",
                "--out",
                "--no-obfuscate",
                "--verbose",
                "--log",
                "--scrub",
            }
        )
        {
            result.Out.ShouldContain(option);
        }

        _scene.Run.Processes.Requests.ShouldBeEmpty();
    }

    [Theory]
    [InlineData(
        new[] { "diagnose" },
        new[] { "sqlsource: command 'diagnose' needs --query or --scrub", "sqlsource: option '--out' is required" }
    )]
    [InlineData(new[] { "diagnose", "--query", "Q.sql:N" }, new[] { "sqlsource: option '--out' is required" })]
    [InlineData(new[] { "diagnose", "--scrub", "raw.zip" }, new[] { "sqlsource: option '--out' is required" })]
    [InlineData(
        new[] { "diagnose", "--out", "r.zip" },
        new[] { "sqlsource: command 'diagnose' needs --query or --scrub" }
    )]
    [InlineData(
        new[] { "diagnose", "--query", "Q.sql", "--out", "r.zip" },
        new[] { "sqlsource: the value of option '--query' at position 3 is not <file>:<name>" }
    )]
    [InlineData(
        new[] { "diagnose", "--query=:N", "--out", "r.zip" },
        new[] { "sqlsource: the value of option '--query' at position 2 is not <file>:<name>" }
    )]
    [InlineData(
        new[] { "diagnose", "--out", "r.zip", "--query", "Q.sql: " },
        new[] { "sqlsource: the value of option '--query' at position 5 is not <file>:<name>" }
    )]
    [InlineData(
        new[] { "diagnose", "--query", "Q.txt:N", "--out", "r.zip" },
        new[] { "sqlsource: the file of option '--query' at position 3 is not a .sql path" }
    )]
    [InlineData(
        new[] { "diagnose", "--query", "C:\\Q.sql", "--out", "r.zip" },
        new[] { "sqlsource: the file of option '--query' at position 3 is not a .sql path" }
    )]
    [InlineData(
        new[] { "diagnose", "--scrub", "raw.zip", "--out", "r.zip", "--no-obfuscate" },
        new[] { "sqlsource: option '--scrub' takes no other option but '--out'" }
    )]
    [InlineData(
        new[] { "diagnose", "App.csproj", "--scrub", "raw.zip", "--out", "r.zip", "--log", "run.log" },
        new[] { "sqlsource: option '--scrub' takes no other option but '--out'" }
    )]
    [InlineData(
        new[] { "diagnose", "--scrub", "raw.zip", "--query", "Q.sql:N", "--out", "r.zip" },
        new[] { "sqlsource: options '--query' and '--scrub' cannot be used together" }
    )]
    [InlineData(
        new[] { "diagnose", "--query", "Q.sql:N", "--out", " " },
        new[] { "sqlsource: the value of option '--out' at position 5 is not a path" }
    )]
    [InlineData(
        new[] { "diagnose", "--query", "Q.sql:N", "--out=a=" + Secrets.Word + ".zip" },
        new[] { "sqlsource: the value of option '--out' at position 4 holds '=' or ';', which a bundle's path may not" }
    )]
    [InlineData(
        new[] { "diagnose", "--scrub", "raw.zip", "--out", "a;" + Secrets.Word + ".zip" },
        new[] { "sqlsource: the value of option '--out' at position 5 holds '=' or ';', which a bundle's path may not" }
    )]
    [InlineData(
        new[] { "diagnose", "Q.sql", "--query", "Q.sql:N", "--out", "r.zip" },
        new[] { "sqlsource: the argument at position 2 is a .sql path: name the query with '--query <file>:<name>'" }
    )]
    [InlineData(
        new[] { "diagnose", "--query", "Q.sql:N", "--out", "r.zip", "--connection", Secrets.Word },
        new[] { "sqlsource: the value of option '--connection' at position 7 is not <name>=<connection string>" }
    )]
    [InlineData(
        new[] { "diagnose", "--query", "Q.sql:N", "--out", "r.zip", "--log", "a=" + Secrets.Word + ".log" },
        new[] { "sqlsource: the value of option '--log' at position 7 holds '=' or ';', which a log's path may not" }
    )]
    public async Task Diagnose_WrongCommandLine_IsAUsageLineThatNamesNoToken(string[] args, string[] lines)
    {
        var result = await _scene.Run.RunAsync(args);

        result.ShouldBe(new CliResult(1, "", string.Concat(lines.Select(static line => line + "\n"))));
        _scene.Run.Processes.Requests.ShouldBeEmpty();
    }

    // A query is found by the last colon, so a Windows path reads.
    [Fact]
    public void CheckUsage_QueryOfAWindowsPath_GivesNoLine()
    {
        var usage = UsageCheck.Check(
            Cli.BuildCommands(),
            ["diagnose", "--query", "C:\\work\\Users.sql:GetUser", "--out", "r.zip"]
        );

        usage.Messages.ShouldBeEmpty();
        DiagnoseCommand.CheckUsage(usage).ShouldBeEmpty();
    }
}
