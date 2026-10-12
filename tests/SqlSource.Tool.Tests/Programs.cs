using System;
using System.IO;
using System.Linq;
using SqlSource.Tool.Processes;
using Xunit;

namespace SqlSource.Tool.Tests;

// The programs of the operating system that the tests of ProcessRunner start.
internal static class Programs
{
    // A run of a program in the temporary directory, with the environment of the test process.
    public static ProcessRequest Request(string program, params string[] arguments) =>
        new(program, [.. arguments], Path.GetTempPath(), [], [], []);

    // Skips the test when no folder of the path holds a program of the name.
    public static void SkipUnlessOnPath(string program) =>
        Assert.SkipUnless(IsOnPath(program), $"There is no '{program}' on the path.");

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
