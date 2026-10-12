using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Tests.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// The tool in a process of its own, by its real entry, on a project of Fixtures/Projects with the real
// "dotnet msbuild": a signal reaches a process and nothing less, and Cli.Run is where the tool listens.  One test
// for the systems that have signals, and one for Windows.
public sealed class TerminationTests : IDisposable
{
    // The folders that a run makes in the temporary directory for its manifests.
    private const string RunFolders = "sqlsource-*";

    private readonly FixtureProjects _fixtures = new();

    public void Dispose() => _fixtures.Dispose();

    // SIGTERM is what "timeout", a cancelled CI job and an editor send, and SIGHUP what a closed terminal and a
    // lost session do.  Left alone, either ends the process at once with the exit code 143 or 129, and the folder
    // of the run stays with the MSBuild it started.
    [Theory]
    [InlineData("-TERM")]
    [InlineData("-HUP")]
    public async Task Run_SignalThatEndsAProcess_CancelsTheRunWhichEndsWithTheExitCode1AndLeavesNoFolder(string signal)
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "Nothing sends a signal to a process on Windows.");
        Programs.SkipUnlessOnPath("kill");
        var waiting = _fixtures.PathOf("waiting");
        var project = ProjectThatWaits(waiting);
        var temp = Directory.CreateDirectory(_fixtures.PathOf("tmp")).FullName;

        using var tool = StartTool(temp, "describe", project);
        try
        {
            var output = tool.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
            var error = tool.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);

            // The target of the project has begun its wait: the run has its folder, and an MSBuild that has
            // started a program of its own.
            await AppearsAsync(() => File.Exists(waiting), $"Nothing wrote '{waiting}'.");
            using (var kill = Process.Start("kill", [signal, tool.Id.ToString(CultureInfo.InvariantCulture)]))
            {
                await kill.WaitForExitAsync(TestContext.Current.CancellationToken);
            }

            await tool.WaitForExitAsync(TestContext.Current.CancellationToken);

            // A run that was cancelled prints nothing more.
            (await error).ShouldBeEmpty();
            (await output).ShouldBeEmpty();
            tool.ExitCode.ShouldBe(1);
            // An MSBuild that was killed leaves the pipes of its runtime there, which are none of the tool's.
            Directory.EnumerateDirectories(temp, RunFolders).ShouldBeEmpty();
        }
        finally
        {
            if (!tool.HasExited)
            {
                tool.Kill(entireProcessTree: true);
            }
        }
    }

    // A console that is closed is the SIGHUP of Windows, and the system ends the process when the handler of it
    // returns: the run has ended by then, or its folder stays with the MSBuild it started.  The tool has a console
    // of its own here, which "start" gives it, and "taskkill" without /F closes the window of it.  Nothing can
    // raise the other event, the shutdown of the system, which has the same handler.
    [Fact]
    public async Task Run_ConsoleThatIsClosed_CancelsTheRunWhichLeavesNoFolder()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Only Windows closes the console of a process.");
        Programs.SkipUnlessOnPath("cmd");
        Programs.SkipUnlessOnPath("taskkill");
        var waiting = _fixtures.PathOf("waiting");
        var project = ProjectThatWaits(waiting);
        var temp = Directory.CreateDirectory(_fixtures.PathOf("tmp")).FullName;
        var title = "sqlsource-test-" + Guid.NewGuid().ToString("N");

        // The shell has no console, and waits for the tool, so it ends when the tool does.
        var start = new ProcessStartInfo("cmd")
        {
            WorkingDirectory = _fixtures.Root,
            CreateNoWindow = true,
            Arguments =
                $"/c start \"{title}\" /wait dotnet --roll-forward LatestMajor \"{ToolPath}\" describe \"{project}\"",
        };
        SetEnvironment(start, temp);
        using var shell = Process.Start(start)!;
        try
        {
            // The target of the project has begun its wait.  A wait that failed would end the run by itself, and
            // the test would take that for the close: so the run is seen to go on for a while first.  Nothing
            // here says what the tool wrote, which went to its console.
            await AppearsAsync(() => File.Exists(waiting), $"Nothing wrote '{waiting}'.");
            await Task.Delay(TimeSpan.FromSeconds(3), TestContext.Current.CancellationToken);
            shell.HasExited.ShouldBeFalse("the run ended by itself, before its console was closed");
            _ = Directory.EnumerateDirectories(temp, RunFolders).ShouldHaveSingleItem();

            var closed = await CloseConsoleAsync(title);

            // Left alone, the run would wait for its MSBuild for two minutes.
            using var soon = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            soon.CancelAfter(TimeSpan.FromSeconds(60));
            try
            {
                await shell.WaitForExitAsync(soon.Token);
            }
            catch (OperationCanceledException) when (!TestContext.Current.CancellationToken.IsCancellationRequested)
            {
                Assert.Fail($"The run did not end when its console was closed.  taskkill said: {closed}");
            }

            Directory.EnumerateDirectories(temp, RunFolders).ShouldBeEmpty(closed);
        }
        finally
        {
            if (!shell.HasExited)
            {
                shell.Kill(entireProcessTree: true);
            }
        }
    }

    // The tool that was built beside the tests.
    private static string ToolPath => Path.Combine(AppContext.BaseDirectory, "SqlSource.Tool.dll");

    // A copy of a fixture whose manifest target writes the file given and then waits for two minutes, so that a
    // run of it ends only when it is told to: a test that failed to tell it sees a run that goes on.
    private string ProjectThatWaits(string waiting)
    {
        var project = _fixtures.Copy("Single");
        var wait = OperatingSystem.IsWindows() ? "ping -n 120 127.0.0.1 &gt; nul" : "sleep 120";
        _ = _fixtures.WriteFile(
            "Single/Directory.Build.targets",
            $"""
            <Project>
                <Target Name="Wait" BeforeTargets="SqlSourceWriteManifest">
                    <WriteLinesToFile File="{SecurityElement.Escape(waiting)}" Lines="waiting" />
                    <Exec Command="{wait}" />
                </Target>
            </Project>

            """
        );
        return project;
    }

    // Starts the tool with the temporary directory given.
    private Process StartTool(string temp, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _fixtures.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
        };

        // The tool beside the tests has the libraries that the tests were given, which are of the tests' runtime
        // and not of the older one that the tool asks for.
        start.ArgumentList.Add("--roll-forward");
        start.ArgumentList.Add("LatestMajor");
        start.ArgumentList.Add(ToolPath);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        SetEnvironment(start, temp);
        var tool = Process.Start(start)!;
        tool.StandardInput.Close();
        return tool;
    }

    // Path.GetTempPath reads the first where there are signals, and the others on Windows.
    private static void SetEnvironment(ProcessStartInfo start, string temp)
    {
        start.Environment["TMPDIR"] = temp;
        start.Environment["TMP"] = temp;
        start.Environment["TEMP"] = temp;
        start.Environment["DOTNET_NOLOGO"] = "true";
    }

    // Closes the console whose window has the title, and gives what "taskkill" said, for a test that fails.
    private static async Task<string> CloseConsoleAsync(string title)
    {
        var start = new ProcessStartInfo("taskkill") { RedirectStandardOutput = true, RedirectStandardError = true };
        start.ArgumentList.Add("/FI");
        start.ArgumentList.Add($"WINDOWTITLE eq {title}*");
        using var taskkill = Process.Start(start)!;
        var error = taskkill.StandardError.ReadToEndAsync(TestContext.Current.CancellationToken);
        var output = await taskkill.StandardOutput.ReadToEndAsync(TestContext.Current.CancellationToken);
        await taskkill.WaitForExitAsync(TestContext.Current.CancellationToken);
        return $"{output.Trim()} {(await error).Trim()} (exit code {taskkill.ExitCode})";
    }

    // Waits for what a program of the test does, which with a first run of MSBuild on a machine takes a while.
    private static async Task AppearsAsync(Func<bool> isThere, string otherwise)
    {
        for (var attempt = 0; attempt < 12000; attempt++)
        {
            if (isThere())
            {
                return;
            }

            await Task.Delay(10, TestContext.Current.CancellationToken);
        }

        throw new TimeoutException(otherwise);
    }
}
