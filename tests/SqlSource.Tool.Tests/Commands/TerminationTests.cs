using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Security;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Testing.Fixtures;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// The tool in a process of its own, by its real entry, on a project of Fixtures/Projects with the real
// "dotnet msbuild": a signal reaches a process and nothing less, and Cli.Run is where the tool listens.  One test
// for the systems that have signals, and one for Windows.  Only nightly.yml runs the test of a closed console, so try a
// change to it by starting that workflow by hand.
public sealed class TerminationTests : IDisposable
{
    // The folders that a run makes in the temporary directory for its manifests.
    private const string RunFolders = "sqlsource-*";

    // How soon the run ends once its console is closed.
    private static readonly TimeSpan Soon = TimeSpan.FromSeconds(60);

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

        var result = await Programs.RunAsync(
            Programs.ToolStart(_fixtures.Root, temp, "describe", project),
            Eventually.Patience,
            async tool =>
            {
                // The target of the project has begun its wait: the run has its folder, and an MSBuild that has
                // started a program of its own.
                await Eventually.TrueAsync(() => File.Exists(waiting), $"Nothing wrote '{waiting}'.");
                var kill = new ProcessStartInfo("kill", [signal, tool.Id.ToString(CultureInfo.InvariantCulture)]);
                var sent = await Programs.RunAsync(kill, Eventually.Patience);
                sent.ExitCode.ShouldBe(0, sent.Error);
            }
        );

        // A run that was cancelled prints nothing more.
        result.Error.ShouldBeEmpty();
        result.Output.ShouldBeEmpty();
        result.ExitCode.ShouldBe(1);
        // An MSBuild that was killed leaves the pipes of its runtime there, which are none of the tool's.
        Directory.EnumerateDirectories(temp, RunFolders).ShouldBeEmpty();
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
        var answers = _fixtures.PathOf("answers");
        var project = ProjectThatWaits(_fixtures.PathOf("waiting"), answers);
        var temp = Directory.CreateDirectory(_fixtures.PathOf("tmp")).FullName;
        var title = "sqlsource-test-" + Guid.NewGuid().ToString("N");

        // The shell has no console, and waits for the tool, so it ends when the tool does.
        var start = new ProcessStartInfo("cmd")
        {
            WorkingDirectory = _fixtures.Root,
            CreateNoWindow = true,
            Arguments =
                $"/c start \"{title}\" /wait \"{Programs.Dotnet}\" --roll-forward LatestMajor "
                + $"\"{Programs.ToolPath}\" describe \"{project}\"",
        };
        Programs.SetToolEnvironment(start, temp);
        var closed = "";
        _ = await Programs.RunAsync(
            start,
            Eventually.Patience,
            async shell =>
            {
                // The target of the project has begun its wait, and ping has answered twice, a second apart.  A wait
                // that failed would end the run by itself, and the test would take that for the close: so the wait is
                // seen to go on first.  Nothing here says what the tool wrote, which went to its console.
                await Eventually.TrueAsync(() => HasAnswered(answers), $"ping did not answer twice in '{answers}'.");
                shell.HasExited.ShouldBeFalse("the run ended by itself, before its console was closed");
                _ = Directory.EnumerateDirectories(temp, RunFolders).ShouldHaveSingleItem();

                closed = await CloseConsoleAsync(title);

                // Left alone, the run would wait for its MSBuild for two minutes.
                try
                {
                    await shell.WaitForExitAsync(TestContext.Current.CancellationToken).WaitAsync(Soon);
                }
                catch (TimeoutException)
                {
                    Assert.Fail($"The run did not end when its console was closed.  taskkill said: {closed}");
                }
            }
        );

        Directory.EnumerateDirectories(temp, RunFolders).ShouldBeEmpty(closed);
    }

    // A copy of a fixture whose manifest target writes the file given and then waits for two minutes, so that a
    // run of it ends only when it is told to: a test that failed to tell it sees a run that goes on.  On Windows the
    // waiter, ping, writes its answers to the file "answers" names, or to nothing.
    private string ProjectThatWaits(string waiting, string? answers = null)
    {
        var project = _fixtures.Copy("Single");
        var (program, arguments) = Programs.Waiter(120);
        var target = answers is null ? "nul" : $"\"{answers}\"";
        var output = OperatingSystem.IsWindows() ? " > " + target : "";
        var wait = SecurityElement.Escape(string.Join(' ', [program, .. arguments]) + output);
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

    // Whether ping has answered twice: its first line and each answer name the address, in every language.  cmd
    // holds the file open for writing while ping runs.
    private static bool HasAnswered(string answers)
    {
        try
        {
            using var stream = new FileStream(
                answers,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete
            );
            using var reader = new StreamReader(stream);
            var text = reader.ReadToEnd();
            return text.Split("127.0.0.1").Length > 3;
        }
        catch (IOException)
        {
            return false;
        }
    }

    // Closes the console whose window has the title, and gives what "taskkill" said, for a test that fails.
    private static async Task<string> CloseConsoleAsync(string title)
    {
        var start = new ProcessStartInfo("taskkill");
        start.ArgumentList.Add("/FI");
        start.ArgumentList.Add($"WINDOWTITLE eq {title}*");
        var taskkill = await Programs.RunAsync(start, Eventually.Patience);
        return $"{taskkill.Output.Trim()} {taskkill.Error.Trim()} (exit code {taskkill.ExitCode})";
    }
}
