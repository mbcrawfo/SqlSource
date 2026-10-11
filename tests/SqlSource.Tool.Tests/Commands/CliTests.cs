using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

public sealed class CliTests : IDisposable
{
    private readonly CliRun _run = new();

    public void Dispose() => _run.Dispose();

    [Fact]
    public async Task Run_NoArguments_PrintsTheHelp()
    {
        var result = await _run.RunAsync();

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
        var result = await _run.RunAsync(option);

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("Describes the SQL queries of a project");
        result.Out.ShouldContain("--version");
        result.Out.ShouldContain("--help");
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_Version_PrintsTheInformationalVersionOfTheTool()
    {
        var result = await _run.RunAsync("--version");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldBe(Cli.Version + "\n");
        // The test host has a version of its own, which is what System.CommandLine would print.
        result.Out.ShouldMatch(@"^\d+\.\d+\.\d+");
        result.Out.ShouldStartWith(PackageVersion.Prefix);
        result.Error.ShouldBeEmpty();
    }

    [Theory]
    [InlineData("--conection=" + Secrets.Word)]
    [InlineData("--conection:" + Secrets.Word)]
    [InlineData("--conection", Secrets.Word)]
    [InlineData("describe", "--conection", Secrets.Word)]
    [InlineData("describe", "--conection=billing=Host=db;Password=" + Secrets.Word)]
    public async Task Run_UnknownOption_IsNamedWithoutItsValue(params string[] args)
    {
        var result = await _run.RunAsync(args);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--conection'\n"));
    }

    [Fact]
    public async Task Run_TwoUnknownOptions_ReportsTheFirstAlone()
    {
        // The second may be the value of the first.
        var result = await _run.RunAsync("--pasword", "-" + Secrets.Word);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--pasword'\n"));
    }

    [Fact]
    public async Task Run_UnknownOptionAfterAnotherMistake_ReportsTheOptionAlone()
    {
        var result = await _run.RunAsync("describe", "one", "two", "--oops");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--oops'\n"));
    }

    [Fact]
    public async Task Run_UnknownCommand_IsReportedByItsPosition()
    {
        var result = await _run.RunAsync("descrbe");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 1\n"));
    }

    [Fact]
    public async Task Run_ValueForAnOptionThatTakesNone_NamesTheOptionAlone()
    {
        var result = await _run.RunAsync("--help=" + Secrets.Word);

        result.ShouldBe(new CliResult(1, "", "sqlsource: option '--help' takes no value\n"));
    }

    [Fact]
    public async Task Run_ValueForVersion_NamesTheOptionAlone()
    {
        var result = await _run.RunAsync("--version=" + Secrets.Word);

        result.ShouldBe(new CliResult(1, "", "sqlsource: option '--version' takes no value\n"));
    }

    [Fact]
    public void Parser_Settings_ReadEveryTokenAsItStands()
    {
        // UsageCheck reads "@file" as an argument and "-ab" as one option, and System.CommandLine must too.
        Cli.Parser.ResponseFileTokenReplacer.ShouldBeNull();
        Cli.Parser.EnablePosixBundling.ShouldBeFalse();
    }

    [Fact]
    public void BuildCommands_EveryOption_TakesNoValueOrNeedsOne()
    {
        // System.CommandLine's default for a flag takes an optional value, which UsageCheck cannot tell from the
        // argument after the flag.
        var commands = new List<Command> { Cli.BuildCommands() };

        for (var index = 0; index < commands.Count; index++)
        {
            commands.AddRange(commands[index].Subcommands);
            foreach (var option in commands[index].Options)
            {
                (option.Arity.MaximumNumberOfValues == 0 || option.Arity.MinimumNumberOfValues > 0).ShouldBeTrue(
                    option.Name
                );
            }
        }

        commands.Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task Run_TokenThatStartsWithAnAtSign_IsNotReadAsAFile()
    {
        _ = _run.Folder.WriteFile("args.rsp", "--version");

        var result = await _run.RunAsync("@args.rsp");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 1\n"));
    }

    [Fact]
    public async Task Run_DoubleHyphen_IsAnUnknownOption()
    {
        var result = await _run.RunAsync("--", "--version");

        result.ShouldBe(new CliResult(1, "", "sqlsource: unknown option '--'\n"));
    }

    [Theory]
    [InlineData("[suggest]")]
    [InlineData("/h")]
    [InlineData("sqlsource")]
    public async Task Run_TokenThatSystemCommandLineGivesAMeaning_IsAnArgumentLikeAnyOther(string token)
    {
        var result = await _run.RunAsync(token);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 1\n"));
    }

    [Fact]
    public async Task Run_Exception_IsReportedAsSqlsrc200WithoutAStackTrace()
    {
        _run.Out = new FailingWriter("the pipe is closed");

        var result = await _run.RunAsync("--version");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldBe(
            "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: "
                + "System.InvalidOperationException: the pipe is closed\n"
                + Expected.See("SQLSRC200")
        );
    }

    [Fact]
    public async Task Run_ExceptionWithTheDebugVariableSet_AddsTheStackTraceBeforeTheLink()
    {
        _run.Out = new FailingWriter("the pipe is closed");
        _run.Environment["SQLSOURCE_DEBUG"] = "1";

        var result = await _run.RunAsync("--version");

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
        _run.Out = new FailingWriter("the pipe is closed");
        _run.Environment["SQLSOURCE_DEBUG"] = "";

        var result = await _run.RunAsync("--version");

        result.Error.ShouldNotContain("trace:");
    }

    [Fact]
    public async Task Run_ExceptionWhoseMessageHasLineBreaks_KeepsTheErrorOnOneLine()
    {
        _run.Out = new FailingWriter("first\r\nsecond\nthird");

        var result = await _run.RunAsync("--version");

        result.Error.Split('\n')[0].ShouldEndWith("System.InvalidOperationException: first second third");
        result.Error.Split('\n').Length.ShouldBe(3);
    }

    // The counts are the run's, and the files were written: a summary that cannot be printed does not lose them.
    [Fact]
    public async Task Run_SummaryThatCannotBeWritten_StillEndsTheLogWithTheCounts()
    {
        _run.Out = new FailingWriter("the pipe is closed");
        _run.Describers.Add(new FakeDescriber());
        var project = Plans.Postgres(_run.Folder, directory: "").AnsweredBy(_run.Processes);
        _run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        _ = project.AddSql("Users.sql", SqlTexts.GetUser);

        var result = await _run.RunAsync("describe", "--log", "run.log");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith("sqlsource : error SQLSRC200: ");
        var end = LogReader.Read(_run.Folder.PathOf("run.log"))[^1];
        end.GetProperty("event").GetString().ShouldBe("run.end");
        var database = end.GetProperty("databases").EnumerateArray().ShouldHaveSingleItem();
        database.GetProperty("described").GetInt32().ShouldBe(1);
    }

    // The error of an exception belongs in the log: the run's reporter, which has the trace, reports it, and not
    // the one the run started with.
    [Fact]
    public async Task Run_SummaryThatCannotBeWrittenWithALog_HasTheErrorAsAnEventBeforeRunEnd()
    {
        _run.Out = new FailingWriter("the pipe is closed");
        _run.Describers.Add(new FakeDescriber());
        var project = Plans.Postgres(_run.Folder, directory: "").AnsweredBy(_run.Processes);
        _run.Environment["SQLSOURCE_CONNECTION_POSTGRES"] = Secrets.Connection;
        _ = project.AddSql("Users.sql", SqlTexts.GetUser);

        var result = await _run.RunAsync("describe", "--log", "run.log");

        result.ExitCode.ShouldBe(1);
        var events = LogReader.Read(_run.Folder.PathOf("run.log"));
        events[^2].GetProperty("event").GetString().ShouldBe("diagnostic");
        events[^2].GetProperty("id").GetString().ShouldBe("SQLSRC200");
        events[^1].GetProperty("event").GetString().ShouldBe("run.end");
    }

    [Fact]
    public async Task Run_CancelledBeforeItStarts_PrintsNothingAndExitsWithOne()
    {
        var result = await _run.RunCancelledAsync("--version");

        result.ShouldBe(new CliResult(1, "", ""));
    }

    [Fact]
    public void Run_HostThatCannotBeMade_IsReportedAsSqlsrc200()
    {
        // A working directory that was deleted: asking for it throws, before there is a host to report to.
        using var error = new StringWriter(CultureInfo.InvariantCulture) { NewLine = "\n" };

        var exitCode = Cli.Run(
            ["--version"],
            () => throw new FileNotFoundException("Unable to find the specified file."),
            error
        );

        exitCode.ShouldBe(1);
        error
            .ToString()
            .ShouldStartWith(
                "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: "
                    + "System.IO.FileNotFoundException: Unable to find the specified file.\n"
            );
    }

    // Stands in for a standard output that fails: every write throws.
    private sealed class FailingWriter(string message) : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void Write(char value) => throw new InvalidOperationException(message);
    }

    // A run that waits on a database and does not look at its token can still be ended from the keyboard.
    [Fact]
    public void Interrupt_FirstTime_CancelsTheRunAndKeepsTheProcess_SecondTime_LetsTheProcessEnd()
    {
        using var interrupt = new CancellationTokenSource();

        Cli.Interrupt(interrupt).ShouldBeTrue();
        interrupt.IsCancellationRequested.ShouldBeTrue();
        Cli.Interrupt(interrupt).ShouldBeFalse();
    }
}
