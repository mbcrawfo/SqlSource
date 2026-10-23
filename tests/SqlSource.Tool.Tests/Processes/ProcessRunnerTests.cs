using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Processes;

// The runner of a real run, with real programs: "dotnet", and ones that every operating system has.  A test of one
// of those is skipped where the program is not on the path.  ProcessRunnerEnvironmentTests has the test of the
// variables that a program gets.
public sealed class ProcessRunnerTests
{
    private static readonly ProcessRunner Runner = new();

    private static ProcessRequest Request(string program, params string[] arguments) =>
        Programs.Request(program, arguments);

    // Waits for half a minute.
    private static ProcessRequest Wait()
    {
        var (program, arguments) = Programs.Waiter(30);
        return Request(program, arguments);
    }

    // Writes its id to one file, starts a program that waits, writes that one's id to the other, and waits for it.
    // Windows has no shell that knows its own id but PowerShell.  The files reach the script as variables, which
    // need no quoting, and with their full paths: the programs do not run in the folder of the files, since Windows
    // does not delete a folder that a process runs in.
    private static ProcessRequest WaitWithAChild(string parentFile, string childFile)
    {
        var request = OperatingSystem.IsWindows()
            ? Request(
                "powershell",
                "-NoProfile",
                "-NonInteractive",
                "-Command",
                "[IO.File]::WriteAllText($env:SQLSOURCE_TEST_PARENT, $PID);"
                    + " $child = Start-Process ping -ArgumentList '-n', '30', '127.0.0.1' -NoNewWindow -PassThru;"
                    + " [IO.File]::WriteAllText($env:SQLSOURCE_TEST_CHILD, $child.Id);"
                    + " $child.WaitForExit()"
            )
            : Request(
                "sh",
                "-c",
                "echo $$ > \"$SQLSOURCE_TEST_PARENT\"; sleep 30 & echo $! > \"$SQLSOURCE_TEST_CHILD\"; wait"
            );
        return request with
        {
            SetVariables = request
                .SetVariables.Add("SQLSOURCE_TEST_PARENT", parentFile)
                .Add("SQLSOURCE_TEST_CHILD", childFile),
        };
    }

    [Fact]
    public async Task Run_Dotnet_GivesItsExitCodeAndItsOutput()
    {
        var result = await Runner.RunAsync(
            Request(Programs.Dotnet, "--version"),
            TestContext.Current.CancellationToken
        );

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.Trim().ShouldNotBeEmpty();
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_ProgramThatFails_GivesItsExitCodeAndItsErrorOutput()
    {
        var result = await Runner.RunAsync(
            Request(Programs.Dotnet, "sqlsource-no-such-command"),
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
        using var folder = new TempFolder();
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var request = WaitWithAChild(folder.PathOf("parent"), folder.PathOf("child"));
        Programs.SkipUnlessOnPath(request.Program);
        Programs.SkipUnlessOnPath(Wait().Program);

        var run = Runner.RunAsync(request, cancel.Token);
        var parent = await ReadIdAsync(folder.PathOf("parent"));
        var child = await ReadIdAsync(folder.PathOf("child"));
        IsRunning(parent).ShouldBeTrue();
        IsRunning(child).ShouldBeTrue();
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(run);
        await Eventually.TrueAsync(() => !IsRunning(parent), "the program is still running");
        await Eventually.TrueAsync(() => !IsRunning(child), "the program that the program started is still running");
    }

    // The id that a script wrote to a file, once it has.  PowerShell takes seconds to start on a machine that has
    // not run it.
    private static async Task<int> ReadIdAsync(string file) =>
        await Eventually.ValueAsync<int>(
            async () => File.Exists(file) && int.TryParse(await File.ReadAllTextAsync(file), out var id) ? id : null,
            $"Nothing wrote an id to '{file}'."
        );

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

    // A kill only asks, and on Windows the program ends some time after.  The run waits for that: what its caller
    // deletes next, the folder that MSBuild writes its manifest to, is then no longer held by it.  The kill here
    // is done only when the test lets it, after the runner asked for it, so it is late on every system.
    [Fact]
    public async Task Run_Cancelled_EndsOnlyWhenTheProgramHas()
    {
        var wait = Wait();
        Programs.SkipUnlessOnPath(wait.Program);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var asked = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var let = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var runner = new ProcessRunner(process =>
        {
            var id = process.Id;
            _ = Task.Run(async () =>
            {
                await let.Task;
                using var late = Process.GetProcessById(id);
                late.Kill();
            });
            asked.SetResult(id);
        });

        var run = runner.RunAsync(wait, cancel.Token);
        await cancel.CancelAsync();
        var program = await asked.Task;
        IsRunning(program).ShouldBeTrue();
        let.SetResult();

        _ = await Should.ThrowAsync<OperationCanceledException>(run);
        IsRunning(program).ShouldBeFalse();
    }

    // A kill of the whole tree can fail, as when a process of it belongs to another user.  The run was cancelled
    // all the same, and that is what its caller is told: anything else would be reported as SQLSRC200.
    [Theory]
    [InlineData(typeof(InvalidOperationException))]
    [InlineData(typeof(Win32Exception))]
    [InlineData(typeof(AggregateException))]
    public async Task Run_CancelledAndTheKillFails_ThrowsTheCancellation(Type failure)
    {
        var wait = Wait();
        Programs.SkipUnlessOnPath(wait.Program);
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var runner = new ProcessRunner(process =>
        {
            // The program is ended, so that the test leaves none, and the kill fails as the system's would.
            process.Kill(entireProcessTree: true);
            throw (Exception)Activator.CreateInstance(failure)!;
        });

        var run = runner.RunAsync(wait, cancel.Token);
        await cancel.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(run);
    }

    [Fact]
    public async Task Run_CancelledBeforeItStarts_Throws()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        _ = await Should.ThrowAsync<OperationCanceledException>(Runner.RunAsync(Wait(), cancelled.Token));
    }
}
