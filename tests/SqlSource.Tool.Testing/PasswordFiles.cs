using System;
using Xunit;

namespace SqlSource.Tool.Testing;

// What a test of a password file needs of the test process.
internal static class PasswordFiles
{
    // Skips the test while PGPASSWORD is set.  The driver takes PGPASSWORD before it reads a password file, so with
    // the variable set it never reads one.
    public static void SkipUnlessRead() =>
        Assert.SkipWhen(
            Environment.GetEnvironmentVariable("PGPASSWORD") is not null,
            "PGPASSWORD is set, and the driver takes it before a password file."
        );
}
