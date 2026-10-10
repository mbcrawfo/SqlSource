using System;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Processes;
using Xunit;

namespace SqlSource.Tool.Tests;

// The runner of a real run, with real programs: "dotnet", and three that every operating system has.
public class ProcessRunnerTests
{
    private static readonly ProcessRunner Runner = new();

    private static ProcessRequest Request(string program, params string[] arguments) =>
        new(program, [.. arguments], Path.GetTempPath(), [], []);

    // Prints the environment, one variable on a line.
    private static ProcessRequest PrintEnvironment() =>
        OperatingSystem.IsWindows() ? Request("cmd", "/c", "set") : Request("env");

    // Waits for half a minute.
    private static ProcessRequest Wait() =>
        OperatingSystem.IsWindows() ? Request("ping", "-n", "30", "127.0.0.1") : Request("sleep", "30");

    [Fact]
    public async Task Run_Dotnet_GivesItsExitCodeAndItsOutput()
    {
        var result = await Runner.RunAsync(Request("dotnet", "--version"), TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.Trim().ShouldNotBeEmpty();
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput()
    {
        var result = await Runner.RunAsync(
            Request("dotnet", "sqlsource-no-such-command"),
            TestContext.Current.CancellationToken
        );

        result.ExitCode.ShouldNotBe(0);
        (result.Output + result.Error).Trim().ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Run_VariableSet_ReachesTheProgramAndVariableRemovedDoesNot()
    {
        Environment.SetEnvironmentVariable("SQLSOURCE_TEST_REMOVED", "removed");
        Environment.SetEnvironmentVariable("SQLSOURCE_TEST_KEPT", "kept");
        var request = PrintEnvironment() with
        {
            SetVariables = ImmutableDictionary<string, string>.Empty.Add("SQLSOURCE_TEST_SET", "set"),
            RemovedVariables = ["SQLSOURCE_TEST_REMOVED", "SQLSOURCE_TEST_NEVER_SET"],
        };

        var result = await Runner.RunAsync(request, TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldContain("SQLSOURCE_TEST_SET=set");
        result.Output.ShouldContain("SQLSOURCE_TEST_KEPT=kept");
        result.Output.ShouldNotContain("SQLSOURCE_TEST_REMOVED");
    }

    [Fact]
    public async Task Run_ProgramThatReadsItsInput_FindsTheEndOfIt()
    {
        // "sort" with no file reads its input to the end.  With an input left open it would never end.
        var result = await Runner.RunAsync(Request("sort"), TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ProgramThatDoesNotExist_IsNotStartedAndSaysWhy()
    {
        var result = await Runner.RunAsync(Request("sqlsource-no-such-program"), TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(ProcessResult.NotStarted);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Run_Cancelled_KillsTheProgramAndThrows()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var time = Stopwatch.StartNew();

        var run = Runner.RunAsync(Wait(), cancel.Token);
        cancel.CancelAfter(TimeSpan.FromMilliseconds(200));

        _ = await Should.ThrowAsync<OperationCanceledException>(run);
        time.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(15));
    }

    [Fact]
    public async Task Run_CancelledBeforeItStarts_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(Runner.RunAsync(Wait(), cancelled.Token));
    }
}
