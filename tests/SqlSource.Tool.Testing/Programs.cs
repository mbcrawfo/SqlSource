using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Projects;
using Xunit;

namespace SqlSource.Tool.Testing;

// The programs that tests start: those of the operating system, for the tests of ProcessRunner, the tool that was
// built beside the tests, for a test that runs it in a process of its own, and the dotnet that started the tests,
// which RunAsync runs to its end with a limit as it runs any other.
internal static class Programs
{
    // How long a program that was killed has to be gone.
    private static readonly TimeSpan KillWait = TimeSpan.FromSeconds(30);

    // The tool that was built beside the tests.
    public static string ToolPath => Path.Combine(AppContext.BaseDirectory, "SqlSource.Tool.dll");

    // The deps file of the test project that is running, beside the tool.
    private static string TestsDepsFile =>
        Path.Combine(AppContext.BaseDirectory, Assembly.GetEntryAssembly()!.GetName().Name + ".deps.json");

    // A run of a program in the temporary directory, with the environment of the test process.
    public static ProcessRequest Request(string program, params string[] arguments) =>
        new(program, [.. arguments], Path.GetTempPath(), [], [], []);

    // A program of the operating system that waits for the seconds given and ends, with its arguments.  Windows has no
    // sleep, but ping waits as long.
    public static (string Program, string[] Arguments) Waiter(int seconds)
    {
        var count = seconds.ToString(CultureInfo.InvariantCulture);
        if (OperatingSystem.IsWindows())
        {
            return ("ping", ["-n", count, "127.0.0.1"]);
        }

        return ("sleep", [count]);
    }

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
        // and not of the older one that the tool asks for.  The tests' deps file names them where they are: the
        // tool's own names a library with a copy for each platform, as SqlClient, under its runtime's folder of
        // runtimes/, which the tests' output does not have.
        start.ArgumentList.Add("exec");
        start.ArgumentList.Add("--roll-forward");
        start.ArgumentList.Add("LatestMajor");
        start.ArgumentList.Add("--depsfile");
        start.ArgumentList.Add(TestsDepsFile);
        start.ArgumentList.Add(ToolPath);
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        SetToolEnvironment(start, temp);
        return start;
    }

    // Runs the program to its end and gives what it wrote.  It sets the three Redirect* of the start it is given, reads
    // both streams while the program runs, so that neither fills and holds it, and gives the program no input.
    // whileRunning runs once the program has started.
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
            await KillAsync(program, start, exception);
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

    // What it wrote is left out when a program that escaped the kill still holds a stream, or a read of one failed.
    private static async Task<TimeoutException> DidNotEndAsync(
        ProcessStartInfo start,
        TimeSpan patience,
        Task<string> output,
        Task<string> error
    )
    {
        var message = $"'{start.FileName}', or a program it started, did not end within {patience}.";
        try
        {
            var written = await Task.WhenAll(output, error).WaitAsync(KillWait);
            return new TimeoutException($"{message}  It wrote: {written[0]}{written[1]}");
        }
        catch (Exception exception) when (exception is TimeoutException or IOException or ObjectDisposedException)
        {
            return new TimeoutException(message);
        }
    }

    // The whole tree, since a shell or the SDK starts programs of its own that hold the streams open.  A program that
    // has just ended is no failure.  A program that is still there after KillWait is the failure, and gives the one
    // that brought the kill as its inner exception.
    private static async Task KillAsync(Process program, ProcessStartInfo start, Exception failure)
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

        try
        {
            await program.WaitForExitAsync(CancellationToken.None).WaitAsync(KillWait);
        }
        catch (TimeoutException)
        {
            throw new TimeoutException($"'{start.FileName}' was still there {KillWait} after it was killed.", failure);
        }
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
