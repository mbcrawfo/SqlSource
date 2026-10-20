using System;
using System.Runtime.Versioning;

namespace SqlSource.Tool.Tests;

// What a test of a file or a folder that the system keeps the tool out of needs of the test process.
internal static class FileModes
{
    // Whether a file mode can keep a file's owner out: Windows has no such mode, and a privileged process is let in
    // whatever the mode says.  A test that cannot skips with an "if", Assert.Skip and a "return", and not with
    // Assert.SkipWhen: CA1416 reads the guard only on a path that returns, and so knows that what follows is not
    // Windows.
    [UnsupportedOSPlatformGuard("windows")]
    public static bool KeepTheOwnerOut => !OperatingSystem.IsWindows() && !Environment.IsPrivilegedProcess;
}
