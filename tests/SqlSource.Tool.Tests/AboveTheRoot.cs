using System.Linq;

namespace SqlSource.Tool.Tests;

// A .sql path that climbs above the root of the file system and so has no normalised path.  Outside Windows a
// backslash is no separator to Path.GetFullPath, which keeps the .. segments that the generator's rule resolves; on
// Windows it resolves them itself, so a test of such a path skips there.
internal static class AboveTheRoot
{
    public const string SkipReason = "Path.GetFullPath resolves a backslash's .. on Windows.";

    public static string SqlFile { get; } = "a" + string.Concat(Enumerable.Repeat(@"\..", 64)) + @"\q.sql";
}
