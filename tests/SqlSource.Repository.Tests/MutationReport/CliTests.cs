using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.MutationReport;
using Xunit;

namespace SqlSource.Repository.Tests.MutationReport;

// The compare command, run in process on reports that Reports writes to a folder of the test's own.
public sealed class CliTests
{
    private const string Usage = "Usage: compare <before.json> <after.json>";

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
    [InlineData("condense", "one.json", "two.json")]
    public async Task RunAsync_OtherArguments_ExitsTwoWithTheUsage(params string[] arguments)
    {
        var (code, output, error) = await RunAsync(arguments);

        code.ShouldBe(2);
        error.ShouldBe(Lines(Usage));
        output.ShouldBeEmpty();
    }

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
