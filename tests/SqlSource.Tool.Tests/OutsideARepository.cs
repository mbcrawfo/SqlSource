using SqlSource.Tool.DotEnv;
using Xunit;

namespace SqlSource.Tool.Tests;

// A test of what the tool does outside a repository makes its folders in the system's temporary folder, and the tool
// climbs from them to the nearest folder that holds a .git.  Where the temporary folder is inside a git work tree, as
// with TMPDIR in a checkout or a home folder kept in git, such a test skips rather than fail.
internal static class OutsideARepository
{
    public const string SkipReason = "The temporary folder is inside a git work tree.";

    public static void SkipUnless(string folder) => Assert.SkipWhen(DotEnvSearch.IsInRepository(folder), SkipReason);
}
