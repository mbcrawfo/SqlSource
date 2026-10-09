using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

public class CliTests
{
    private const string Secret = "s3cret";

    [Fact]
    public async Task Run_NoArguments_PrintsTheHelp()
    {
        using var run = new CliRun();

        var result = await run.RunAsync();

        result.ExitCode.ShouldBe(0);
        // Nothing here is a text of System.CommandLine, which it writes in the language of the machine.
        result.Out.ShouldContain("Describes the SQL queries of a project");
        result.Out.ShouldContain("--version");
        result.Error.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public async Task Run_Help_ListsTheOptionsThatDoSomething(string option)
    {
        using var run = new CliRun();

        var result = await run.RunAsync(option);

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("Describes the SQL queries of a project");
        result.Out.ShouldContain("--version");
        result.Out.ShouldContain("--help");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_Version_PrintsTheInformationalVersionOfTheTool()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--version");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldBe(Cli.Version + "\n");
        // The test host has a version of its own, which is what System.CommandLine would print.
        result.Out.ShouldMatch(@"^\d+\.\d+\.\d+");
        result.Out.ShouldStartWith(PackageVersion.Prefix);
        result.Error.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--conection=" + Secret)]
    [InlineData("--conection:" + Secret)]
    [InlineData("--conection", Secret)]
    [InlineData("describe", "--conection", Secret)]
    [InlineData("describe", "--conection=billing=Host=db;Password=" + Secret)]
    public async Task Run_UnknownOption_IsNamedWithoutItsValue(params string[] args)
    {
        using var run = new CliRun();

        var result = await run.RunAsync(args);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--conection'\n"));
    }

    [Fact]
    public async Task Run_TwoUnknownOptions_ReportsTheFirstAlone()
    {
        using var run = new CliRun();

        // The second may be the value of the first.
        var result = await run.RunAsync("--pasword", "-" + Secret);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--pasword'\n"));
    }

    [Fact]
    public async Task Run_UnknownOptionAfterAnotherMistake_ReportsTheOptionAlone()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("describe", "one", "two", "--oops");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--oops'\n"));
    }

    [Fact]
    public async Task Run_UnknownCommand_IsReportedByItsPosition()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("descrbe");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 1\n"));
    }

    [Fact]
    public async Task Run_ValueForAnOptionThatTakesNone_NamesTheOptionAlone()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--help=" + Secret);

        result.ShouldBe(new CliResult(1, "", "sqlsource: option '--help' takes no value\n"));
    }

    [Fact]
    public async Task Run_TokenThatStartsWithAnAtSign_IsNotReadAsAFile()
    {
        using var run = new CliRun();
        _ = run.Folder.WriteFile("args.rsp", "--version");

        var result = await run.RunAsync("@args.rsp");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 1\n"));
    }

    [Fact]
    public async Task Run_DoubleHyphen_IsAnUnknownOption()
    {
        using var run = new CliRun();

        var result = await run.RunAsync("--", "--version");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--'\n"));
    }

    [Theory]
    [InlineData("[suggest]")]
    [InlineData("/h")]
    [InlineData("sqlsource")]
    public async Task Run_TokenThatSystemCommandLineGivesAMeaning_IsAnArgumentLikeAnyOther(string token)
    {
        using var run = new CliRun();

        var result = await run.RunAsync(token);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 1\n"));
    }

    [Fact]
    public async Task Run_Exception_IsReportedAsSqlsrc200WithoutAStackTrace()
    {
        using var run = new CliRun { Out = new FailingWriter("the pipe is closed") };

        var result = await run.RunAsync("--version");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe(
            "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: "
                + "System.InvalidOperationException: the pipe is closed\n"
                + "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc200\n"
        );
    }

    [Fact]
    public async Task Run_ExceptionWithTheDebugVariableSet_AddsTheStackTraceBeforeTheLink()
    {
        using var run = new CliRun { Out = new FailingWriter("the pipe is closed") };
        run.Environment["SQLSOURCE_DEBUG"] = "1";

        var result = await run.RunAsync("--version");

        result.ExitCode.ShouldBe(1);
        var lines = result.Error.TrimEnd('\n').Split('\n');
        lines[0].ShouldStartWith("sqlsource : error SQLSRC200: ");
        lines[1].ShouldBe("    trace: System.InvalidOperationException: the pipe is closed");
        lines[2].ShouldStartWith("    trace:    at ");
        lines[^1].ShouldStartWith("    see: ");
        // Every line but the first is a continuation line: nothing of a trace starts in the first column.
        lines[1..].ShouldAllBe(line => line.StartsWith("    ", StringComparison.Ordinal));
    }

    [Fact]
    public async Task Run_ExceptionWithTheDebugVariableEmpty_HasNoStackTrace()
    {
        using var run = new CliRun { Out = new FailingWriter("the pipe is closed") };
        run.Environment["SQLSOURCE_DEBUG"] = "";

        var result = await run.RunAsync("--version");

        result.Error.ShouldNotContain("trace:");
    }

    [Fact]
    public async Task Run_ExceptionWhoseMessageHasLineBreaks_KeepsTheErrorOnOneLine()
    {
        using var run = new CliRun { Out = new FailingWriter("first\r\nsecond\nthird") };

        var result = await run.RunAsync("--version");

        result.Error.Split('\n')[0].ShouldEndWith("System.InvalidOperationException: first second third");
        result.Error.Split('\n').Length.ShouldBe(3);
    }

    [Fact]
    public async Task Run_CancelledBeforeItStarts_PrintsNothingAndExitsWithOne()
    {
        using var run = new CliRun();

        var result = await run.RunCancelledAsync("--version");

        result.ShouldBe(new CliResult(1, "", ""));
    }

    // Stands in for a standard output that fails: every write throws.
    private sealed class FailingWriter(string message) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => throw new InvalidOperationException(message);
    }
}
