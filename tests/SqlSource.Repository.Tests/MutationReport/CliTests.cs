using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The compare command, run in process on reports that Reports writes to a folder of the test's own.
public sealed class CliTests
{
    [Fact]
    public async Task RunAsync_NothingLost_ExitsZeroWithTheSummaryAlone() =>
        await InFolderAsync(async folder =>
        {
            var before = Write(folder, "before.json", (7, "Killed"));
            var after = Write(folder, "after.json", (7, "Killed"));

            var (code, output, error) = await RunAsync("compare", before, after);

            code.ShouldBe(0);
            output.ShouldBe(Lines("1 detected before, 1 still detected, 0 lost, 0 not comparable"));
            error.ShouldBeEmpty();
        });

    [Fact]
    public async Task RunAsync_MutantLostAndMutantsNotComparable_ExitsOneAndNamesEach() =>
        await InFolderAsync(async folder =>
        {
            var before = Write(folder, "before.json", (7, "Killed"), (9, "Killed"), (12, "Killed"));
            var after = Write(folder, "after.json", (7, "Survived"), (9, "Ignored"));

            var (code, output, error) = await RunAsync("compare", before, after);

            code.ShouldBe(1);
            output.ShouldBe(
                Lines(
                    "LOST A.cs:7:1 Boolean mutation -> true (was Killed, now Survived)",
                    "NOT COMPARABLE A.cs:9:1 Boolean mutation -> true (was Killed, now Ignored)",
                    "NOT COMPARABLE A.cs:12:1 Boolean mutation -> true (was Killed, now absent)",
                    "3 detected before, 0 still detected, 1 lost, 2 not comparable"
                )
            );
            error.ShouldBeEmpty();
        });

    [Theory]
    [InlineData]
    [InlineData("compare")]
    [InlineData("compare", "one.json")]
    [InlineData("compare", "one.json", "two.json", "three.json")]
    [InlineData("condemn", "one.json", "two.json")]
    [InlineData("static")]
    [InlineData("static", "one.json", "two.json")]
    [InlineData("slices", "root")]
    [InlineData("slices", "root", "out")]
    [InlineData("slices", "root", "out", "--limit", "0", "tests")]
    [InlineData("slices", "root", "out", "--limit", "many", "tests")]
    [InlineData("slices", "root", "out", "tests", "--limit")]
    [InlineData("slices", "root", "out", "tests", "--exclude-from")]
    [InlineData("slices", "root", "out", "--unknown", "tests")]
    [InlineData("condense", "root")]
    [InlineData("kills", "report.json")]
    [InlineData("kills", "report.json", "N.A.One", "N.A.Two")]
    [InlineData("condense", "root", "run", "--broad-from")]
    [InlineData("condense", "root", "run", "--narrow-from", "file")]
    public async Task RunAsync_OtherArguments_ExitsTwoWithTheUsage(params string[] arguments)
    {
        var (code, output, error) = await RunAsync(arguments);

        code.ShouldBe(2);
        error.ShouldBe(
            Lines(
                "Usage: compare <before.json> <after.json>",
                "       static <report.json>",
                "       slices <repository root> <output folder> [--limit <lines>] [--exclude-from <file>] "
                    + "<folder> ...",
                "       condense <repository root> <run folder> [--broad-from <file>]",
                "       kills <report.json> <test name>"
            )
        );
        output.ShouldBeEmpty();
    }

    [Fact]
    public async Task RunAsync_Slices_WritesAFileForEachSliceAndTheIndex() =>
        await InFolderAsync(async folder =>
        {
            WriteLines(folder, "tests/P/A/ATests.cs", 60);
            WriteLines(folder, "tests/P/B/BTests.cs", 30);
            WriteLines(folder, "tests/P/B/CTests.cs", 50);
            WriteLines(folder, "tests/P/B/notes.md", 500);
            WriteLines(folder, "tests/P/B/bin/Built.cs", 500);
            WriteLines(folder, "tests/P/B/obj/Built.cs", 500);
            WriteLines(folder, "tests/P/Data/Example.cs", 500);
            WriteLines(folder, "tests/Q/QTests.cs", 500);
            var exclusions = Path.Combine(folder, "excluded.txt");
            await File.WriteAllLinesAsync(
                exclusions,
                ["# test data", "", "tests/P/Data/  # holds no test"],
                TestContext.Current.CancellationToken
            );
            var slices = Path.Combine(folder, "run", "slices");

            var (code, output, error) = await RunAsync(
                "slices",
                folder,
                slices,
                "--limit",
                "100",
                "--exclude-from",
                exclusions,
                "tests/P/"
            );

            error.ShouldBeEmpty();
            code.ShouldBe(0);
            output.ShouldBe(Lines("2 slices of 3 files and 140 lines"));
            ReadLines(slices, "index.txt").ShouldBe(["P.A-1 60 1", "P.B-1 80 2"]);
            ReadLines(slices, "P.A-1.txt").ShouldBe(["tests/P/A/ATests.cs"]);
            ReadLines(slices, "P.B-1.txt").ShouldBe(["tests/P/B/BTests.cs", "tests/P/B/CTests.cs"]);
            Directory.GetFiles(slices).Length.ShouldBe(3);
        });

    [Fact]
    public async Task RunAsync_SlicesOfFilesOfOnePartialClass_KeepsThemTogether() =>
        await InFolderAsync(async folder =>
        {
            WriteLines(folder, "tests/P/A.cs", 60);
            WriteLines(folder, "tests/P/B.cs", 30, "public sealed partial class BTests");
            WriteLines(folder, "tests/P/C.cs", 10, "    public partial class BTests");
            WriteLines(folder, "tests/P/D.cs", 30, "public sealed partial class BTests");
            WriteLines(folder, "tests/P/E.cs", 20);
            var slices = Path.Combine(folder, "slices");

            var (code, _, _) = await RunAsync("slices", folder, slices, "--limit", "100", "tests/P");

            code.ShouldBe(0);
            ReadLines(slices, "P-2.txt").ShouldBe(["tests/P/B.cs", "tests/P/C.cs", "tests/P/D.cs", "tests/P/E.cs"]);
        });

    [Fact]
    public async Task RunAsync_SlicesOfTwoFolders_CutsEachByItselfAndNumbersThemTogether() =>
        await InFolderAsync(async folder =>
        {
            WriteLines(folder, "tests/P/A.cs", 10);
            WriteLines(folder, "tests/Q/B.cs", 10);
            var slices = Path.Combine(folder, "slices");

            var (code, _, _) = await RunAsync("slices", folder, slices, "tests/P", "tests/Q");

            code.ShouldBe(0);
            ReadLines(slices, "index.txt").ShouldBe(["P-1 10 1", "Q-1 10 1"]);
        });

    // A class's share of its project's time, and a slice's evidence, are of one project: a folder of several
    // projects is cut project by project, however small they are.
    [Fact]
    public async Task RunAsync_SlicesOfAFolderOfSeveralProjects_PutsNoTwoProjectsInOneSlice() =>
        await InFolderAsync(async folder =>
        {
            WriteLines(folder, "tests/D/A/A.csproj", 1, "<Project />");
            WriteLines(folder, "tests/D/A/ATests.cs", 10);
            WriteLines(folder, "tests/D/A/Sub/MoreTests.cs", 10);
            WriteLines(folder, "tests/D/B/B.csproj", 1, "<Project />");
            WriteLines(folder, "tests/D/B/BTests.cs", 10);
            WriteLines(folder, "tests/D/Shared.cs", 10);
            WriteLines(folder, "tests/D/Loose/LooseTests.cs", 10);
            var slices = Path.Combine(folder, "slices");

            var (code, _, error) = await RunAsync("slices", folder, slices, "tests/D");

            error.ShouldBeEmpty();
            code.ShouldBe(0);
            ReadLines(slices, "index.txt").ShouldBe(["D.A-1 20 2", "D.B-1 10 1", "D-1 20 2"]);
            ReadLines(slices, "D.A-1.txt").ShouldBe(["tests/D/A/ATests.cs", "tests/D/A/Sub/MoreTests.cs"]);
            ReadLines(slices, "D-1.txt").ShouldBe(["tests/D/Loose/LooseTests.cs", "tests/D/Shared.cs"]);
        });

    [Fact]
    public async Task RunAsync_SlicesOfAFolderInsideAProject_CutsItAsOneFolder() =>
        await InFolderAsync(async folder =>
        {
            WriteLines(folder, "tests/D/A/A.csproj", 1, "<Project />");
            WriteLines(folder, "tests/D/A/X/XTests.cs", 10);
            WriteLines(folder, "tests/D/A/X/Y/YTests.cs", 10);
            var slices = Path.Combine(folder, "slices");

            var (code, _, _) = await RunAsync("slices", folder, slices, "tests/D/A/X");

            code.ShouldBe(0);
            ReadLines(slices, "index.txt").ShouldBe(["D.A.X-1 20 2"]);
        });

    // A folder is cut by its path from the root, however it is written.
    [Theory]
    [InlineData("./tests/P")]
    [InlineData("tests/P/")]
    [InlineData("tests/Q/../P")]
    public async Task RunAsync_SlicesOfAFolderWrittenAnotherWay_CutsItAsWrittenPlainly(string written) =>
        await InFolderAsync(async folder =>
        {
            WriteLines(folder, "tests/P/Own.cs", 60);
            WriteLines(folder, "tests/P/A/ATests.cs", 60);
            WriteLines(folder, "tests/P/B/BTests.cs", 60);
            _ = Directory.CreateDirectory(Path.Combine(folder, "tests", "Q"));
            var slices = Path.Combine(folder, "slices");

            var (code, _, error) = await RunAsync("slices", folder, slices, "--limit", "100", written);

            error.ShouldBeEmpty();
            code.ShouldBe(0);
            ReadLines(slices, "index.txt").ShouldBe(["P-1 60 1", "P.A-1 60 1", "P.B-1 60 1"]);
            ReadLines(slices, "P.A-1.txt").ShouldBe(["tests/P/A/ATests.cs"]);
        });

    [Theory]
    [InlineData(".")]
    [InlineData("tests/..")]
    [InlineData("..")]
    [InlineData("tests/../../elsewhere")]
    public async Task RunAsync_SlicesOfTheRootOrOfAFolderOutsideIt_ExitsTwoNamingIt(string outside) =>
        await InFolderAsync(async folder =>
        {
            var root = Path.Combine(folder, "repo");
            WriteLines(root, "tests/P/ATests.cs", 10);
            _ = Directory.CreateDirectory(Path.Combine(folder, "elsewhere"));
            var slices = Path.Combine(folder, "slices");

            var (code, output, error) = await RunAsync("slices", root, slices, outside);

            code.ShouldBe(2);
            error.ShouldBe(Lines($"The folder {outside} is not a folder below the repository's root."));
            output.ShouldBeEmpty();
            Directory.Exists(slices).ShouldBeFalse();
        });

    [Theory]
    [InlineData("tests/Missing")]
    [InlineData("tests/P", "tests/P/A")]
    public async Task RunAsync_SlicesOfAFolderThatDoesNotExistOrIsInsideAnother_ExitsTwoNamingIt(
        params string[] folders
    ) =>
        await InFolderAsync(async folder =>
        {
            WriteLines(folder, "tests/P/A/ATests.cs", 10);
            var slices = Path.Combine(folder, "slices");

            var (code, output, error) = await RunAsync(["slices", folder, slices, .. folders]);

            code.ShouldBe(2);
            error.ShouldContain(folders[^1]);
            output.ShouldBeEmpty();
            Directory.Exists(slices).ShouldBeFalse();
        });

    // A cut over an earlier one would leave the earlier one's slices beside its own.
    [Fact]
    public async Task RunAsync_SlicesIntoAFolderThatHoldsFiles_ExitsTwoAndWritesNothing() =>
        await InFolderAsync(async folder =>
        {
            WriteLines(folder, "tests/P/ATests.cs", 10);
            WriteLines(folder, "slices/Old-1.txt", 1);

            var (code, _, error) = await RunAsync("slices", folder, Path.Combine(folder, "slices"), "tests/P");

            code.ShouldBe(2);
            error.ShouldContain("is not empty");
            Directory.GetFiles(Path.Combine(folder, "slices")).Length.ShouldBe(1);
        });

    [Fact]
    public async Task RunAsync_ReportThatDoesNotExist_ExitsTwoAndNamesTheFile() =>
        await InFolderAsync(async folder =>
        {
            var before = Write(folder, "before.json", (7, "Killed"));
            var missing = Path.Combine(folder, "missing.json");

            var (code, output, error) = await RunAsync("compare", before, missing);

            code.ShouldBe(2);
            error.ShouldStartWith($"Cannot read {missing}: ");
            output.ShouldBeEmpty();
        });

    [Fact]
    public async Task RunAsync_FileThatIsNoReport_ExitsTwoAndNamesTheFile() =>
        await InFolderAsync(async folder =>
        {
            var before = Write(folder, "before.json", (7, "Killed"));
            var other = Path.Combine(folder, "other.json");
            await File.WriteAllTextAsync(other, "[]", TestContext.Current.CancellationToken);

            var (code, output, error) = await RunAsync("compare", before, other);

            code.ShouldBe(2);
            error.ShouldStartWith($"Cannot read {other}: ");
            output.ShouldBeEmpty();
        });

    [Fact]
    public async Task RunAsync_StaticOfAReportThatLeftItsStaticMutantsOut_PrintsTheirExclusionsAndExitsZero() =>
        await InFolderAsync(async folder =>
        {
            var report = Path.Combine(folder, "report.json");
            Reports.Write(
                report,
                "/repo",
                new ReportFile("/repo/B.cs", "abcdef", new ReportMutant(1, 1, 1, 3, "Ignored", Static: true)),
                new ReportFile(
                    "/repo/A.cs",
                    "abcdef\nghijkl",
                    new ReportMutant(2, 2, 2, 4, "CompileError", Static: true),
                    new ReportMutant(1, 1, 1, 3, "Killed")
                )
            );

            var (code, output, error) = await RunAsync("static", report);

            code.ShouldBe(0);
            output.ShouldBe(Lines("!A.cs{8..10}", "!B.cs{0..2}"));
            error.ShouldBeEmpty();
        });

    [Fact]
    public async Task RunAsync_StaticOfAReportThatTestedAStaticMutant_PrintsTheExclusionsAndExitsOne() =>
        await InFolderAsync(async folder =>
        {
            var report = Path.Combine(folder, "report.json");
            Reports.Write(
                report,
                "/repo",
                new ReportFile(
                    "/repo/A.cs",
                    "abcdef",
                    new ReportMutant(1, 1, 1, 3, "Killed", Static: true),
                    new ReportMutant(1, 4, 1, 6, "Survived", Static: true)
                )
            );

            var (code, output, error) = await RunAsync("static", report);

            code.ShouldBe(1);
            output.ShouldBe(Lines("!A.cs{0..2}{3..5}"));
            error.ShouldBe(Lines("2 static mutants were tested, so the report's kills cannot be trusted."));
        });

    [Fact]
    public async Task RunAsync_StaticOfAFileThatIsNoReport_ExitsTwoAndNamesTheFile() =>
        await InFolderAsync(async folder =>
        {
            var other = Path.Combine(folder, "other.json");
            await File.WriteAllTextAsync(other, "[]", TestContext.Current.CancellationToken);

            var (code, output, error) = await RunAsync("static", other);

            code.ShouldBe(2);
            error.ShouldStartWith($"Cannot read {other}: ");
            output.ShouldBeEmpty();
        });

    // A report of three tests, the two rows of N.ATests.Reads and N.BTests.Reads, and of one file's mutants.
    private static string WriteKills(string folder, params ReportMutant[] mutants)
    {
        var path = Path.Combine(folder, "report.json");
        Reports.Write(
            path,
            "/repo/src/SqlSource",
            [new ReportFile("/repo/src/SqlSource/Parsing/A.cs", "class A { }", mutants)],
            [
                new ReportTestFile("/repo/tests/P/ATests.cs", ("a1", "N.ATests.Reads"), ("a2", "N.ATests.Reads")),
                new ReportTestFile("/repo/tests/P/BTests.cs", ("b1", "N.BTests.Reads"), ("c1", "M.BTests.Reads")),
            ]
        );
        return path;
    }

    private static ReportMutant Killed(int line, params string[] killers) =>
        new(line, 1, line, 5, "Killed", CoveredBy: killers, KilledBy: killers);

    [Fact]
    public async Task RunAsync_KillsOfAMethod_ListsEachKillWithItsOtherKillers() =>
        await InFolderAsync(async folder =>
        {
            var report = WriteKills(
                folder,
                Killed(9, "a2", "b1", "c1"),
                Killed(3, "a1", "a2"),
                Killed(5, "b1"),
                new ReportMutant(7, 1, 7, 5, "Survived", CoveredBy: ["a1"], KilledBy: [])
            );

            var (code, output, error) = await RunAsync("kills", report, "N.ATests.Reads");

            error.ShouldBeEmpty();
            code.ShouldBe(0);
            output.ShouldBe(
                Lines(
                    "src/SqlSource/Parsing/A.cs:3:1 Boolean mutation -> `true`",
                    "    killed by it alone",
                    "SURVIVED src/SqlSource/Parsing/A.cs:7:1 Boolean mutation -> `true`",
                    "src/SqlSource/Parsing/A.cs:9:1 Boolean mutation -> `true`",
                    "    also killed by 2 methods: M.BTests.Reads, N.BTests.Reads",
                    "N.ATests.Reads: 2 rows, 2 kills, 1 alone, 0 timeouts under it, 1 survivors under it"
                )
            );
        });

    [Fact]
    public async Task RunAsync_KillsOfAMethodWithTimeouts_ListsThem() =>
        await InFolderAsync(async folder =>
        {
            var report = WriteKills(
                folder,
                new ReportMutant(4, 1, 4, 5, "Timeout", CoveredBy: ["b1", "a1"], KilledBy: []),
                new ReportMutant(6, 1, 6, 5, "Timeout", CoveredBy: ["a1"], KilledBy: [])
            );

            var (code, output, _) = await RunAsync("kills", report, "N.BTests.Reads");

            code.ShouldBe(0);
            output.ShouldBe(
                Lines(
                    "TIMEOUT src/SqlSource/Parsing/A.cs:4:1 Boolean mutation -> `true`",
                    "N.BTests.Reads: 1 rows, 0 kills, 0 alone, 1 timeouts under it, 0 survivors under it"
                )
            );
        });

    // A verifier of a finding that would strengthen a test reads which mutants survive under it.
    [Fact]
    public async Task RunAsync_KillsOfAMethodWithSurvivorsUnderIt_ListsThem() =>
        await InFolderAsync(async folder =>
        {
            var report = WriteKills(
                folder,
                new ReportMutant(4, 1, 4, 5, "Survived", CoveredBy: ["a1", "b1"], KilledBy: []),
                new ReportMutant(6, 1, 6, 5, "Survived", CoveredBy: ["b1"], KilledBy: []),
                Killed(8, "a2")
            );

            var (code, output, _) = await RunAsync("kills", report, "N.ATests.Reads");

            code.ShouldBe(0);
            output.ShouldBe(
                Lines(
                    "SURVIVED src/SqlSource/Parsing/A.cs:4:1 Boolean mutation -> `true`",
                    "src/SqlSource/Parsing/A.cs:8:1 Boolean mutation -> `true`",
                    "    killed by it alone",
                    "N.ATests.Reads: 2 rows, 1 kills, 1 alone, 0 timeouts under it, 1 survivors under it"
                )
            );
        });

    [Fact]
    public async Task RunAsync_KillsOfTheEndOfAName_FindsTheOneMethodThatEndsInIt() =>
        await InFolderAsync(async folder =>
        {
            var report = WriteKills(folder, Killed(3, "a1"));

            var (code, output, _) = await RunAsync("kills", report, "ATests.Reads");

            code.ShouldBe(0);
            output.ShouldEndWith(
                Lines("N.ATests.Reads: 2 rows, 1 kills, 1 alone, 0 timeouts under it, 0 survivors under it")
            );
        });

    [Fact]
    public async Task RunAsync_KillsOfAnEndThatSeveralMethodsShare_ExitsOneNamingThem() =>
        await InFolderAsync(async folder =>
        {
            var report = WriteKills(folder, Killed(3, "a1"));

            var (code, output, error) = await RunAsync("kills", report, "BTests.Reads");

            code.ShouldBe(1);
            error.ShouldBe(Lines("Several tests end in 'BTests.Reads': M.BTests.Reads, N.BTests.Reads"));
            output.ShouldBeEmpty();
        });

    [Fact]
    public async Task RunAsync_KillsOfANameTheReportLacks_ExitsOne() =>
        await InFolderAsync(async folder =>
        {
            var report = WriteKills(folder, Killed(3, "a1"));

            var (code, output, error) = await RunAsync("kills", report, "N.ATests.Read");

            code.ShouldBe(1);
            error.ShouldBe(
                Lines("The report holds no test named 'N.ATests.Read': a scope's report holds its own tests.")
            );
            output.ShouldBeEmpty();
        });

    [Fact]
    public async Task RunAsync_KillsOfAFileThatIsNoReport_ExitsTwoAndNamesTheFile() =>
        await InFolderAsync(async folder =>
        {
            var other = Path.Combine(folder, "other.json");
            await File.WriteAllTextAsync(other, "[]", TestContext.Current.CancellationToken);

            var (code, _, error) = await RunAsync("kills", other, "N.ATests.Reads");

            code.ShouldBe(2);
            error.ShouldStartWith($"Cannot read {other}: ");
        });

    private static async Task<(int Code, string Output, string Error)> RunAsync(params string[] arguments)
    {
        using var output = new StringWriter();
        using var error = new StringWriter();
        var code = await Cli.RunAsync(arguments, output, error);
        return (code, output.ToString(), error.ToString());
    }

    private static string Write(string folder, string name, params (int Line, string Status)[] mutants)
    {
        var path = Path.Combine(folder, name);
        Reports.Write(
            path,
            "/repo",
            Array.ConvertAll(mutants, mutant => ("/repo/A.cs", mutant.Line, "Boolean mutation", "true", mutant.Status))
        );
        return path;
    }

    // A file of a number of lines, the first of them the one given.
    private static void WriteLines(string folder, string path, int lines, string first = "// a line")
    {
        var file = Path.Combine(folder, path);
        _ = Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        File.WriteAllLines(file, [first, .. Enumerable.Repeat("// a line", lines - 1)]);
    }

    private static string[] ReadLines(string folder, string name) => File.ReadAllLines(Path.Combine(folder, name));

    private static string Lines(params string[] lines) => string.Join(Environment.NewLine, lines) + Environment.NewLine;

    private static async Task InFolderAsync(Func<string, Task> test)
    {
        var folder = Directory.CreateTempSubdirectory("sqlsource-mutation-report-").FullName;
        try
        {
            await test(folder);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }
}
