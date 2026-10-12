using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Processes;
using Xunit;

namespace SqlSource.Tool.Tests.Processes;

// The runner of a real run, with real programs: "dotnet", and ones that every operating system has.  A test of one
// of those is skipped where the program is not on the path.  ProcessRunnerEnvironmentTests has the test of the
// variables that a program gets.
public class ProcessRunnerTests
{
    private static readonly ProcessRunner Runner = new();

    private static ProcessRequest Request(string program, params string[] arguments) =>
        Programs.Request(program, arguments);

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
    public async Task Run_ProgramThatReadsItsInput_FindsTheEndOfIt()
    {
        Programs.SkipUnlessOnPath("sort");

        // "sort" with no file reads its input to the end.  With an input left open it would never end.
        var result = await Runner.RunAsync(Request("sort"), TestContext.Current.CancellationToken);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ProgramThatDoesNotExist_IsNotStartedAndSaysWhy()
    {
        var result = await Runner.RunAsync(Request("sqlsource-no-such-program"), TestContext.Current.CancellationToken);

        result.Started.ShouldBeFalse();
        result.ExitCode.ShouldNotBe(0);
        result.Output.ShouldBeEmpty();
        result.Error.ShouldNotBeEmpty();
    }

    // The program has started when RunAsync gives its task.  Had the program ended by itself, nothing would throw.
    [Fact]
    public async Task Run_Cancelled_Throws()
    {
        var wait = Wait();
        Programs.SkipUnlessOnPath(wait.Program);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);

        var run = Runner.RunAsync(wait, cancel.Token);
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(run);
    }

    // The wait for a process ends when the token is cancelled, whether the process was killed or not.  So this
    // looks at the processes: a shell that starts another program, as MSBuild starts its own, and writes the id
    // of each to a file.  Both must be gone.
    [Fact]
    public async Task Run_Cancelled_LeavesNeitherTheProgramNorWhatItStartedRunning()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "The program is a shell script.  docs/tech-debt/TD-0028.");
        Programs.SkipUnlessOnPath("sh");
        Programs.SkipUnlessOnPath("sleep");
        using var folder = new TempFolder();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var request = Request("sh", "-c", "echo $$ > parent; sleep 30 & echo $! > child; wait") with
        {
            WorkingDirectory = folder.Path,
        };

        var run = Runner.RunAsync(request, cancel.Token);
        var parent = await ReadIdAsync(folder.PathOf("parent"));
        var child = await ReadIdAsync(folder.PathOf("child"));
        IsRunning(parent).ShouldBeTrue();
        IsRunning(child).ShouldBeTrue();
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(run);
        (await EndsAsync(parent)).ShouldBeTrue("the program is still running");
        (await EndsAsync(child)).ShouldBeTrue("the program that the program started is still running");
    }

    // The id that a script wrote to a file, once it has.
    private static async Task<int> ReadIdAsync(string file)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            if (File.Exists(file) && int.TryParse(await File.ReadAllTextAsync(file), out var id))
            {
                return id;
            }

            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException($"Nothing wrote an id to '{file}'.");
    }

    private static async Task<bool> EndsAsync(int id)
    {
        for (var attempt = 0; attempt < 100 && IsRunning(id); attempt++)
        {
            await Task.Delay(50, TestContext.Current.CancellationToken);
        }

        return !IsRunning(id);
    }

    private static bool IsRunning(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            return !process.HasExited;
        }
        catch (ArgumentException)
        {
            // No process has the id.
            return false;
        }
    }

    [Fact]
    public async Task Run_CancelledBeforeItStarts_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(Runner.RunAsync(Wait(), cancelled.Token));
    }
}
