using System;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;
using Xunit.Sdk;

namespace SqlSource.Tool.Tests;

// The skip of a test that has the driver read a password file, which it does only while PGPASSWORD is not set.
[Collection(nameof(ChangesTheEnvironment))]
public sealed class PasswordFilesTests
{
    private const string Variable = "PGPASSWORD";

    [Fact]
    public void SkipUnlessRead_PasswordVariable_SkipsWhileItIsSetAndNotOtherwise()
    {
        var was = Environment.GetEnvironmentVariable(Variable);
        try
        {
            Environment.SetEnvironmentVariable(Variable, "x");
            Should
                .Throw<SkipException>(PasswordFiles.SkipUnlessRead)
                .Message.ShouldEndWith("PGPASSWORD is set, and the driver takes it before a password file.");

            Environment.SetEnvironmentVariable(Variable, null);
            Should.NotThrow(PasswordFiles.SkipUnlessRead);
        }
        finally
        {
            Environment.SetEnvironmentVariable(Variable, was);
        }
    }
}
