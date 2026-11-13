using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The condense command, on a repository and a run folder that each test writes: the slices of tests/P, the reports
// of its mutation scopes and the results of its test projects.
public sealed class CondenserTests : IDisposable
{
    private readonly string _root = Directory.CreateTempSubdirectory("sqlsource-condense-").FullName;

    private string Run => Path.Combine(_root, "run");

    private string Source => _root.Replace('\\', '/') + "/src/SqlSource";

    // A source of six lines, for mutants at columns 1 to 5 of a line.
    private const string Lines6 = "line 1\nline 2\nline 3\nline 4\nline 5\nline 6\n";

    public void Dispose() => Directory.Delete(_root, recursive: true);

    // Two tests, one in each of two slices, and a file of mutants: one that both kill, one that the first kills
    // alone, one that survives under the second, one that no test reaches, one that does not compile and a static one.
    private void WriteTwoSlicesAndTheirScope()
    {
        WriteSlice("P-1", "tests/P/ATests.cs");
        WriteSlice("P-2", "tests/P/BTests.cs");
        WriteScope(
            "one",
            new ReportFile(
                Source + "/A.cs",
                Lines6,
                At(1, "Killed", ["t1", "t2"], ["t1", "t2"]),
                At(2, "Killed", ["t1"], ["t1"]),
                At(3, "Survived", ["t2"], []),
                At(4, "NoCoverage", [], []),
                At(5, "CompileError", [], []),
                At(6, "Ignored", [], []) with
                {
                    Static = true,
                }
            )
        );
        WriteTimes(
            "P",
            ("tests/P/ATests.cs", "N.ATests", "Reads", "Pass", "0.5"),
            ("tests/P/BTests.cs", "N.BTests", "Reads", "Pass", "1.5")
        );
    }

    [Fact]
    public async Task RunAsync_Run_WritesEvidenceForEachSliceAndTheGaps()
    {
        WriteTwoSlicesAndTheirScope();

        var (code, output, error) = await RunAsync();

        error.ShouldBeEmpty();
        code.ShouldBe(0);
        output.ShouldBe(Lines("evidence/P-1.md", "evidence/P-2.md", "evidence/gaps.md"));
        var first = Evidence("P-1.md");
        first.ShouldContain("- tests/P/ATests.cs (3)\n");
        first.ShouldContain("- one: ran 2 tests of tests/P; tested 4 mutants of src/SqlSource.\n");
        first.ShouldContain("- `ATests.Reads`: 1 row, 0.500 s; kills 2, alone 1, timeouts under it 0; no cover\n");
        first.ShouldContain("    - alone: src/SqlSource/A.cs:2:1 Boolean mutation\n");
        first.ShouldNotContain("## Survivors");
        first.ShouldContain(
            "- src/SqlSource/A.cs: no test covers lines 4; blind, does not compile: 5; blind, static: 6\n"
        );
        first.ShouldContain("- `ATests`: 0.500 s, 25.0% of tests/P (2.0 s)\n");
        var second = Evidence("P-2.md");
        second.ShouldContain(
            "- `BTests.Reads`: 1 row, 1.5 s; kills 1, alone 0, timeouts under it 0; cover: `ATests.Reads`\n"
        );
        second.ShouldContain(
            "- src/SqlSource/A.cs:3:1 Boolean mutation -> `true`: covered by 1 method here: `BTests.Reads`\n"
        );
        var gaps = Evidence("gaps.md");
        gaps.ShouldContain("## src/SqlSource\n");
        gaps.ShouldContain("| src/SqlSource/A.cs | 4 | 2 | 1 | 1 | 0 | 1 | 1 |\n");
    }

    // The reports name their files in full, so a root given from the working folder, as ".", is made whole first.
    [Fact]
    public async Task RunAsync_RootGivenFromTheWorkingFolder_FindsTheTimesOfItsTests()
    {
        WriteTwoSlicesAndTheirScope();
        using var output = new StringWriter();
        using var error = new StringWriter();

        var code = await Condenser.RunAsync(
            Path.GetRelativePath(Directory.GetCurrentDirectory(), _root),
            Run,
            [],
            output,
            error,
            TestContext.Current.CancellationToken
        );

        error.ToString().ShouldBeEmpty();
        code.ShouldBe(0);
        Evidence("P-1.md").ShouldContain("- `ATests.Reads`: 1 row, 0.500 s; kills 2");
    }

    [Fact]
    public async Task RunAsync_BroadTest_IsNoOthersCover()
    {
        WriteTwoSlicesAndTheirScope();

        var (code, _, _) = await RunAsync("N.BTests.");

        code.ShouldBe(0);
        Evidence("P-1.md")
            .ShouldContain("- `ATests.Reads`: 1 row, 0.500 s; kills 2, alone 2, timeouts under it 0; no cover\n");
        Evidence("P-2.md").ShouldContain("- `BTests.Reads` (broad): 1 row, 1.5 s; kills 1\n");
    }

    [Fact]
    public async Task RunAsync_TestsOfASliceInTwoScopes_AreMeasuredInBoth()
    {
        WriteSlice("P-1", "tests/P/ATests.cs");
        WriteScope(
            "one",
            new ReportFile(Source + "/A.cs", Lines6, At(1, "Killed", ["t1"], ["t1"])),
            new ReportFile(Source + "/Sub/B.cs", Lines6, At(1, "Ignored", [], []))
        );
        WriteScope(
            "two",
            new ReportFile(Source + "/A.cs", Lines6, At(1, "Ignored", [], [])),
            new ReportFile(Source + "/Sub/B.cs", Lines6, At(1, "Killed", ["t1"], ["t1"]))
        );

        var (code, _, error) = await RunAsync();

        error.ShouldBeEmpty();
        code.ShouldBe(0);
        var evidence = Evidence("P-1.md");
        evidence.ShouldContain("- one: ran 2 tests of tests/P; tested 1 mutants of src/SqlSource.\n");
        evidence.ShouldContain("- two: ran 2 tests of tests/P; tested 1 mutants of src/SqlSource/Sub.\n");
        evidence.ShouldContain("- `ATests.Reads`: 1 row, no time; kills 2, alone 2, timeouts under it 0; no cover\n");
    }

    // Review focus 4 of docs/superpowers/plans/2026-11-12-reviewing-test-quality-skill.md.
    [Fact]
    public async Task RunAsync_SliceInNoScope_IsNotMeasured()
    {
        WriteTwoSlicesAndTheirScope();
        WriteSlice("P-3", "tests/P/CTests.cs");
        WriteTimes(
            "P",
            ("tests/P/CTests.cs", "N.CTests", "Reads", "Pass", "2"),
            ("tests/P/CTests.cs", "N.CTests", "Reads", "Pass", "1")
        );

        var (code, _, _) = await RunAsync();

        code.ShouldBe(0);
        var evidence = Evidence("P-3.md");
        evidence.ShouldContain("None: no mutation scope ran these tests");
        evidence.ShouldContain("- `CTests.Reads`: 2 rows, 3.0 s\n");
        evidence.ShouldNotContain("kills");
    }

    [Fact]
    public async Task RunAsync_TestThatTheProjectRanAndNoScopeDid_IsNotMeasuredAmongThoseThatWere()
    {
        WriteTwoSlicesAndTheirScope();
        WriteTimes("P", ("tests/P/ATests.cs", "N.ATests", "Waits", "Skip", "0"));

        var (code, _, _) = await RunAsync();

        code.ShouldBe(0);
        Evidence("P-1.md").ShouldContain("- `ATests.Waits`: 1 row, skipped; not measured: in no scope's run\n");
    }

    // Review focus 5: the kills of a scope whose run failed, or that tested a static mutant, are not real.
    [Fact]
    public async Task RunAsync_ScopeThatFailed_ExitsTwoNamingIt()
    {
        WriteTwoSlicesAndTheirScope();
        await File.WriteAllTextAsync(
            Path.Combine(Run, "mutation", "one", "failed"),
            "1",
            TestContext.Current.CancellationToken
        );

        var (code, output, error) = await RunAsync();

        code.ShouldBe(2);
        error.ShouldBe(
            Lines("The scope one failed, so its report is not evidence: run it again or remove its folder.")
        );
        output.ShouldBeEmpty();
        Directory.Exists(Path.Combine(Run, "evidence")).ShouldBeFalse();
    }

    [Fact]
    public async Task RunAsync_ScopeWithNoReport_ExitsTwoNamingIt()
    {
        WriteTwoSlicesAndTheirScope();
        File.Delete(Path.Combine(Run, "mutation", "one", "reports", "mutation-report.json"));

        var (code, _, error) = await RunAsync();

        code.ShouldBe(2);
        error.ShouldStartWith("Cannot read the report of the scope one: ");
    }

    [Fact]
    public async Task RunAsync_ReportThatTestedAStaticMutant_ExitsTwoNamingItsScope()
    {
        WriteSlice("P-1", "tests/P/ATests.cs");
        WriteScope(
            "one",
            new ReportFile(Source + "/A.cs", Lines6, At(1, "Killed", ["t1"], ["t1"]) with { Static = true })
        );

        var (code, _, error) = await RunAsync();

        code.ShouldBe(2);
        error.ShouldBe(Lines("The scope one tested 1 static mutants, so its kills cannot be trusted: run it again."));
    }

    // Review focus 1.
    [Fact]
    public async Task RunAsync_ScopesThatTestOneMutant_ExitsTwoNamingTheFile()
    {
        WriteSlice("P-1", "tests/P/ATests.cs");
        WriteScope("one", new ReportFile(Source + "/A.cs", Lines6, At(1, "Killed", ["t1"], ["t1"])));
        WriteScope("two", new ReportFile(Source + "/A.cs", Lines6, At(1, "Survived", ["t1"], [])));

        var (code, _, error) = await RunAsync();

        code.ShouldBe(2);
        error.ShouldContain("src/SqlSource/A.cs");
    }

    [Fact]
    public async Task RunAsync_RunWithNoMutationFolder_WritesTimesAlone()
    {
        WriteSlice("P-1", "tests/P/ATests.cs");
        WriteTimes("P", ("tests/P/ATests.cs", "N.ATests", "Reads", "Pass", "0.5"));

        var (code, output, _) = await RunAsync();

        code.ShouldBe(0);
        output.ShouldBe(Lines("evidence/P-1.md"));
        Evidence("P-1.md").ShouldContain("- `ATests.Reads`: 1 row, 0.500 s\n");
    }

    // tests/SqlSource.Tests.RoslynFloor compiles the files of tests/SqlSource.Tests/Generator/ a second time, so its
    // results name those files too.  A slice's times are its own project's.
    [Fact]
    public async Task RunAsync_FileThatASecondProjectCompilesAgain_HasItsOwnProjectsTimesAlone()
    {
        WriteSlice("P-1", "tests/P/ATests.cs");
        WriteTimes(
            "P",
            ("tests/P/ATests.cs", "N.ATests", "Reads", "Pass", "0.5"),
            ("tests/P/BTests.cs", "N.BTests", "Reads", "Pass", "1.5")
        );
        WriteTimes(
            "P.Floor",
            ("tests/P/ATests.cs", "N.ATests", "Reads", "Pass", "7"),
            ("tests/P.Floor/FloorTests.cs", "N.FloorTests", "Reads", "Pass", "9")
        );

        var (code, _, _) = await RunAsync();

        code.ShouldBe(0);
        var evidence = Evidence("P-1.md");
        evidence.ShouldContain("- `ATests.Reads`: 1 row, 0.500 s\n");
        evidence.ShouldContain("- `ATests`: 0.500 s, 25.0% of tests/P (2.0 s)\n");
    }

    [Fact]
    public async Task RunAsync_TheoryWithOneRowSkipped_IsNotSkippedAndHasTheTimeOfItsOtherRows()
    {
        WriteSlice("P-1", "tests/P/ATests.cs");
        WriteTimes(
            "P",
            ("tests/P/ATests.cs", "N.ATests", "Reads", "Pass", "1.5"),
            ("tests/P/ATests.cs", "N.ATests", "Reads", "Skip", "0")
        );

        var (code, _, _) = await RunAsync();

        code.ShouldBe(0);
        Evidence("P-1.md").ShouldContain("- `ATests.Reads`: 2 rows, 1.5 s\n");
    }

    [Fact]
    public async Task RunAsync_SliceOfAProjectThatWasNotRun_HasNoTimes()
    {
        WriteSlice("P-1", "tests/P/ATests.cs");
        await File.WriteAllTextAsync(
            Path.Combine(_root, "tests", "P", "P.csproj"),
            "<Project />",
            TestContext.Current.CancellationToken
        );

        var (code, _, _) = await RunAsync();

        code.ShouldBe(0);
        Evidence("P-1.md").ShouldEndWith("Not run: tests/P has no times in this run.\n");
    }

    [Fact]
    public async Task RunAsync_DatabaseProjects_WriteTheLinesOnlyTheyReach()
    {
        WriteSlice("DatabaseTests.Postgres-1", "tests/DatabaseTests/Postgres/ATests.cs");
        WriteTimes(
            "DatabaseTests/Postgres",
            ("tests/DatabaseTests/Postgres/ATests.cs", "N.ATests", "Reads", "Pass", "4")
        );
        WriteCoverage("DatabaseTests/Postgres", 1, 2, 7, 8);
        WriteCoverage("DatabaseTests/SqlServer", 9);
        WriteCoverage("P", 2);

        var (code, output, _) = await RunAsync();

        code.ShouldBe(0);
        output.ShouldBe(Lines("evidence/DatabaseTests.Postgres-1.md", "evidence/database-only-lines.md"));
        Evidence("database-only-lines.md").ShouldContain("- src/SqlSource/A.cs: 1, 7-9\n");
        Evidence("DatabaseTests.Postgres-1.md")
            .ShouldEndWith(
                "- `ATests`: 4.0 s, 100.0% of tests/DatabaseTests/Postgres (4.0 s)\n\n"
                    + "Lines only the database projects reach: see database-only-lines.md.\n"
            );
    }

    [Fact]
    public async Task RunAsync_RunWithNoSlices_ExitsTwo()
    {
        _ = Directory.CreateDirectory(Run);

        var (code, _, error) = await RunAsync();

        code.ShouldBe(2);
        error.ShouldStartWith("Cannot read the slices: ");
    }

    private static ReportMutant At(int line, string status, string[] coveredBy, string[] killedBy) =>
        new(line, 1, line, 5, status, CoveredBy: coveredBy, KilledBy: killedBy);

    // A slice of one file of three lines, which is written too, added to the run's index.
    private void WriteSlice(string name, string file)
    {
        var path = Path.Combine(_root, file);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllLines(path, ["// one", "// two", "// three"]);
        var slices = Path.Combine(Run, "slices");
        _ = Directory.CreateDirectory(slices);
        File.WriteAllLines(Path.Combine(slices, name + ".txt"), [file]);
        File.AppendAllLines(Path.Combine(slices, "index.txt"), [$"{name} 3 1"]);
    }

    // A scope's report, whose run ran the test t1 of tests/P/ATests.cs and t2 of tests/P/BTests.cs.
    private void WriteScope(string name, params ReportFile[] files)
    {
        var reports = Path.Combine(Run, "mutation", name, "reports");
        _ = Directory.CreateDirectory(reports);
        Reports.Write(
            Path.Combine(reports, "mutation-report.json"),
            Source,
            files,
            [
                new ReportTestFile(_root.Replace('\\', '/') + "/tests/P/ATests.cs", ("t1", "N.ATests.Reads")),
                new ReportTestFile(_root.Replace('\\', '/') + "/tests/P/BTests.cs", ("t2", "N.BTests.Reads")),
            ]
        );
    }

    // The results of a test project's run, added to those it already has.
    private void WriteTimes(
        string project,
        params (string File, string Type, string Method, string Result, string Time)[] tests
    )
    {
        var path = Path.Combine(Run, "tests", project, "results.xunit.xml");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var earlier = File.Exists(path)
            ? File.ReadAllText(path)
                .Replace("<assemblies><assembly><collection>", "", StringComparison.Ordinal)
                .Replace("</collection></assembly></assemblies>", "", StringComparison.Ordinal)
            : "";
        var rows = string.Concat(
            tests.Select(test =>
                $"""<test type="{test.Type}" method="{test.Method}" result="{test.Result}" time="{test.Time}" """
                + $"""source-file="{_root.Replace('\\', '/')}/{test.File}" />"""
            )
        );
        File.WriteAllText(
            path,
            $"<assemblies><assembly><collection>{earlier}{rows}</collection></assembly></assemblies>"
        );
    }

    // The coverage of a test project's run: the lines of src/SqlSource/A.cs that it hit.
    private void WriteCoverage(string project, params int[] lines)
    {
        var path = Path.Combine(Run, "tests", project, "coverage.cobertura.xml");
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var hit = string.Concat(lines.Select(line => $"""<line number="{line}" hits="1" />"""));
        File.WriteAllText(
            path,
            $"""<coverage><packages><package><classes><class filename="{Source}/A.cs"><lines>{hit}</lines></class>"""
                + "</classes></package></packages></coverage>"
        );
    }

    private string Evidence(string name) =>
        File.ReadAllText(Path.Combine(Run, "evidence", name)).ReplaceLineEndings("\n");

    private async Task<(int Code, string Output, string Error)> RunAsync(params string[] broad)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await Condenser.RunAsync(_root, Run, broad, output, error, TestContext.Current.CancellationToken);
        return (code, output.ToString(), error.ToString());
    }

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines) + Environment.NewLine;
}
