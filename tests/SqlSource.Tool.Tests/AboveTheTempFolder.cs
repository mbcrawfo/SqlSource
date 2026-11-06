using SqlSource.Tool.Watching;
using Xunit;

namespace SqlSource.Tool.Tests;

// The tests make their folders in the system's temporary folder, and what is above it is the machine's.  A test that
// takes for granted that nothing there would change what it checks skips where something does, rather than fail.
internal static class AboveTheTempFolder
{
    public const string RepositoryReason = "The temporary folder is inside a git work tree.";

    // For a test of what the watch does outside a repository: it watches the folders up to the nearest that holds a
    // .git, for the settings files and a restore's assets folder.
    public static void SkipWhenInARepository(string folder) =>
        Assert.SkipWhen(RepositoryFolders.IsInRepository(folder), RepositoryReason);
}
