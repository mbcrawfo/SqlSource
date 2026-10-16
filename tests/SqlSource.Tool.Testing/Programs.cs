using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using SqlSource.Tool.Processes;
using Xunit;

namespace SqlSource.Tool.Testing;

// The programs that tests start: those of the operating system, for the tests of ProcessRunner, and the tool that
// was built beside the tests, for a test that runs it in a process of its own.
internal static class Programs
{
    // The tool that was built beside the tests.
    public static string ToolPath => Path.Combine(AppContext.BaseDirectory, "SqlSource.Tool.dll");

    // A run of a program in the temporary directory, with the environment of the test process.
    public static ProcessRequest Request(string program, params string[] arguments) =>
        new(program, [.. arguments], Path.GetTempPath(), [], [], []);

    // Skips the test when no folder of the path holds a program of the name.
    public static void SkipUnlessOnPath(string program) =>
        Assert.SkipUnless(IsOnPath(program), $"There is no '{program}' on the path.");

    // Starts the tool in the working directory given, with the temporary directory given.
    public static Process StartTool(string workingDirectory, string temp, params string[] arguments)
    {
        var start = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
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

        SetToolEnvironment(start, temp);
        var tool = Process.Start(start)!;
        tool.StandardInput.Close();
        return tool;
    }

    // Path.GetTempPath reads the first where there are signals, and the others on Windows.
    public static void SetToolEnvironment(ProcessStartInfo start, string temp)
    {
        start.Environment["TMPDIR"] = temp;
        start.Environment["TMP"] = temp;
        start.Environment["TEMP"] = temp;
        start.Environment["DOTNET_NOLOGO"] = "true";
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
