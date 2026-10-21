using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Testing;

// The programs that tests start: those of the operating system, for the tests of ProcessRunner, and the tool that
// was built beside the tests, for a test that runs it in a process of its own.
internal static class Programs
{
    // How long a program that was killed has to be gone.
    private static readonly TimeSpan KillWait = TimeSpan.FromSeconds(30);

    // The tool that was built beside the tests.
    public static string ToolPath => Path.Combine(AppContext.BaseDirectory, "SqlSource.Tool.dll");

    // A run of a program in the temporary directory, with the environment of the test process.
    public static ProcessRequest Request(string program, params string[] arguments) =>
        new(program, [.. arguments], Path.GetTempPath(), [], [], []);

    // Skips the test when no folder of the path holds a program of the name.
    public static void SkipUnlessOnPath(string program) =>
        Assert.SkipUnless(IsOnPath(program), $"There is no '{program}' on the path.");

    // The dotnet that started the tests, as the tool runs the dotnet that started it.
    public static string Dotnet => ProjectEvaluator.DotnetOf(Environment.GetEnvironmentVariable);

    // The start of the tool in the working directory given, with the temporary directory given.  Starts nothing.
    public static ProcessStartInfo ToolStart(string workingDirectory, string temp, params string[] arguments)
    {
        var start = new ProcessStartInfo(Dotnet) { WorkingDirectory = workingDirectory };

        // The tool beside the tests has the libraries that the tests were given, which are of the tests' runtime
        // and not of the older one that the tool asks for.
        start.ArgumentList.Add("--roll-forward");
        start.ArgumentList.Add("LatestMajor");
        start.ArgumentList.Add(ToolPath);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        SetToolEnvironment(start, temp);
        return start;
    }

    // Starts the tool in the working directory given, with the temporary directory given.
    public static Process StartTool(string workingDirectory, string temp, params string[] arguments)
    {
        var start = ToolStart(workingDirectory, temp, arguments);
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.RedirectStandardInput = true;
        var tool = Process.Start(start)!;
        tool.StandardInput.Close();
        return tool;
    }

    // Runs the program to its end and gives what it wrote.  It reads both streams while the program runs, so that
    // neither fills and holds it, and gives the program no input.  whileRunning runs once the program has started.
    // A program that does not end within the patience, a test that is cancelled, and a whileRunning that fails each
    // get the program and its children killed, and the program gone, before the exception is thrown.
    public static async Task<ProgramResult> RunAsync(
        ProcessStartInfo start,
        TimeSpan patience,
        Func<Process, Task>? whileRunning = null
    )
    {
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.RedirectStandardInput = true;
        var test = TestContext.Current.CancellationToken;
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(test);
        limit.CancelAfter(patience);
        using var program = Process.Start(start)!;

        // Not on a token: a program that is killed ends its streams, and these end with them.
        var output = program.StandardOutput.ReadToEndAsync();
        var error = program.StandardError.ReadToEndAsync();
        try
        {
            program.StandardInput.Close();
            if (whileRunning is not null)
            {
                // A callback that outlives the limit is abandoned when the program is killed, and a later failure of it
                // goes unobserved.
                await whileRunning(program).WaitAsync(limit.Token);
            }

            await program.WaitForExitAsync(limit.Token);
            var written = await Task.WhenAll(output, error).WaitAsync(limit.Token);
            return new ProgramResult(program.ExitCode, written[0], written[1]);
        }
        catch (Exception exception)
        {
            // The cancellation of whileRunning's own, with neither token cancelled, is rethrown as it is.
            await KillAsync(program);
            if (
                exception is OperationCanceledException
                && limit.IsCancellationRequested
                && !test.IsCancellationRequested
            )
            {
                throw await DidNotEndAsync(start, patience, output, error);
            }

            throw;
        }
    }

    // Path.GetTempPath reads TMPDIR on Unix and TMP or TEMP on Windows.  DOTNET_NOLOGO keeps the SDK's first-run
    // banner out of the output.
    public static void SetToolEnvironment(ProcessStartInfo start, string temp)
    {
        start.Environment["TMPDIR"] = temp;
        start.Environment["TMP"] = temp;
        start.Environment["TEMP"] = temp;
        start.Environment["DOTNET_NOLOGO"] = "true";
    }

    // What it wrote is left out when a program that escaped the kill still holds a stream.
    private static async Task<TimeoutException> DidNotEndAsync(
        ProcessStartInfo start,
        TimeSpan patience,
        Task<string> output,
        Task<string> error
    )
    {
        var message = $"'{start.FileName}' did not end within {patience}.";
        try
        {
            var written = await Task.WhenAll(output, error).WaitAsync(KillWait);
            return new TimeoutException($"{message}  It wrote: {written[0]}{written[1]}");
        }
        catch (TimeoutException)
        {
            return new TimeoutException(message);
        }
    }

    // The whole tree, since a shell or the SDK starts programs of its own that hold the streams open.  A program that
    // has just ended is no failure.
    private static async Task KillAsync(Process program)
    {
        try
        {
            program.Kill(entireProcessTree: true);
        }
        catch (Exception exception)
            when (exception is InvalidOperationException or Win32Exception or AggregateException)
        {
            // It ended between the failure and the kill, or the system would not end it or one that it started.
        }

        await program.WaitForExitAsync(CancellationToken.None).WaitAsync(KillWait);
    }

    private static bool IsOnPath(string program)
    {
        // Windows finds "sort" as "sort.exe", by the extensions of PATHEXT.
        string[] extensions = OperatingSystem.IsWindows()
            ?
            [
                .. (Environment.GetEnvironmentVariable("PATHEXT") ?? ".EXE").Split(
                    ';',
                    StringSplitOptions.RemoveEmptyEntries
                ),
            ]
            : [""];
        return (Environment.GetEnvironmentVariable("PATH") ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(folder => extensions.Any(extension => File.Exists(Path.Combine(folder, program + extension))));
    }
}

// What a program wrote, and the code it ended with.
internal sealed record ProgramResult(int ExitCode, string Output, string Error);
