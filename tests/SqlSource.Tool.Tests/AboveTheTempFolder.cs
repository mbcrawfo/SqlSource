using System.IO;
using SqlSource.Tool.DotEnv;
using Xunit;

namespace SqlSource.Tool.Tests;

// The tests make their folders in the system's temporary folder, and what is above it is the machine's.  A test that
// takes for granted that nothing there would change what it checks skips where something does, rather than fail.
internal static class AboveTheTempFolder
{
    public const string RepositoryReason = "The temporary folder is inside a git work tree.";

    public const string EnvReason = "A folder above the temporary folder holds a .env.";

    // For a test of what the tool does outside a repository: the tool climbs to the nearest folder that holds a .git.
    public static void SkipWhenInARepository(string folder) =>
        Assert.SkipWhen(DotEnvSearch.IsInRepository(folder), RepositoryReason);

    // For a build that must find no .env: the build looks for one in every folder up to the root of the file system.
    public static void SkipWhenAnEnvIsAbove(string folder)
    {
        for (
            var above = Path.GetDirectoryName(Path.GetFullPath(folder));
            above is not null;
            above = Path.GetDirectoryName(above)
        )
        {
            Assert.SkipWhen(File.Exists(Path.Combine(above, DotEnvSearch.FileName)), EnvReason);
        }
    }
}
