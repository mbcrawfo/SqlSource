using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using SqlSource.Repository;
using SqlSource.Testing;
using SqlSource.Tool.Testing;

namespace SqlSource.DatabaseTests.Postgres;

// Runs tools/build-postgis-image.sh, or a copy of it, for PostgisImageScriptTests and PostgisImageCheckTests.
internal static class PostgisImageScript
{
    public static Task<ProgramResult> RunAsync(params string[] arguments) =>
        RunCopyAsync(
            RepositoryRoot.Path("tools", "build-postgis-image.sh"),
            RepositoryRoot.WorkingFolder.Path,
            arguments
        );

    // Runs the script at the path given, from the folder given, as a test runs a copy of the repository's.
    public static async Task<ProgramResult> RunCopyAsync(string script, string folder, params string[] arguments) =>
        await Programs.RunAsync(
            new ProcessStartInfo(await BashAsync(), [script, .. arguments]) { WorkingDirectory = folder },
            Eventually.Patience
        );

    // The bash that runs the repository's scripts.  On Windows the first bash on the PATH can be WSL's, as when a test
    // runs from an IDE, so it is Git's, whose install is three folders above what `git --exec-path` prints,
    // mingw64/libexec/git-core.
    private static async Task<string> BashAsync()
    {
        if (!OperatingSystem.IsWindows())
        {
            return "bash";
        }

        var git = await Programs.RunAsync(new ProcessStartInfo("git", ["--exec-path"]), Eventually.Patience);
        return Path.GetFullPath(Path.Combine(git.Output.Trim(), "..", "..", "..", "bin", "bash.exe"));
    }
}
