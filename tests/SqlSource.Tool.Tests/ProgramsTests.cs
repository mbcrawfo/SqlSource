using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

// The programs that a test runs with a limit: the dotnet that started the tests, and a program of the operating
// system that is killed, with its children, when a test cannot wait for it any longer.  Two tests change the
// environment of this process, so the class is of the collection that runs alone.
[Collection(nameof(ChangesTheEnvironment))]
public sealed class ProgramsTests
{
    private const string HostPath = "DOTNET_HOST_PATH";

    [Fact]
    public void Dotnet_HostPathSet_IsThatPath()
    {
        var found = Environment.GetEnvironmentVariable(HostPath);
        Environment.SetEnvironmentVariable(HostPath, "/opt/dotnet/dotnet");
        try
        {
            Programs.Dotnet.ShouldBe("/opt/dotnet/dotnet");
        }
        finally
        {
            Environment.SetEnvironmentVariable(HostPath, found);
        }
    }

    [Fact]
    public void Dotnet_HostPathEmpty_IsDotnet()
    {
        var found = Environment.GetEnvironmentVariable(HostPath);
        Environment.SetEnvironmentVariable(HostPath, "");
        try
        {
            Programs.Dotnet.ShouldBe("dotnet");
        }
        finally
        {
            Environment.SetEnvironmentVariable(HostPath, found);
        }
    }

    [Fact]
    public async Task RunAsync_Dotnet_GivesItsExitCodeAndOutput()
    {
        var result = await Programs.RunAsync(new ProcessStartInfo(Programs.Dotnet, ["--version"]), Eventually.Patience);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.Trim().ShouldNotBeEmpty();
        result.Error.ShouldBeEmpty();
    }

    // Both pipes fill at once, which a reader that takes one to its end before the other cannot get past.
    [Fact]
    public async Task RunAsync_ProgramThatWritesBothStreams_GivesBothWhole()
    {
        Assert.SkipWhen(OperatingSystem.IsWindows(), "There is no sh on Windows.");
        Programs.SkipUnlessOnPath("sh");
        var start = new ProcessStartInfo(
            "sh",
            ["-c", "head -c 200000 /dev/zero | tr '\\0' o; head -c 200000 /dev/zero | tr '\\0' e >&2"]
        );

        var result = await Programs.RunAsync(start, Eventually.Patience);

        result.ExitCode.ShouldBe(0, result.Error);
        result.Output.Trim().Length.ShouldBe(200_000);
        result.Error.Trim().Length.ShouldBe(200_000);
    }

    [Fact]
    public async Task RunAsync_ProgramThatOutlivesThePatience_IsKilledAndThrows()
    {
        var start = Sleeper();
        var wall = Stopwatch.StartNew();

        var thrown = await Should.ThrowAsync<TimeoutException>(() =>
            Programs.RunAsync(start, TimeSpan.FromMilliseconds(200))
        );

        thrown.Message.ShouldContain("did not end within");
        wall.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(10));
    }

    [Fact]
    public async Task RunAsync_Cancelled_KillsTheProgramAndThrowsTheCancellation()
    {
        var seen = 0;

        _ = await Should.ThrowAsync<OperationCanceledException>(() =>
            Programs.RunAsync(
                Sleeper(),
                Eventually.Patience,
                program =>
                {
                    seen = program.Id;
                    throw new OperationCanceledException();
                }
            )
        );

        ShouldHaveExited(seen);
    }

    [Fact]
    public async Task RunAsync_WhileRunningThatFails_KillsTheProgramAndThrowsItsException()
    {
        var seen = 0;

        var thrown = await Should.ThrowAsync<InvalidOperationException>(() =>
            Programs.RunAsync(
                Sleeper(),
                Eventually.Patience,
                program =>
                {
                    seen = program.Id;
                    throw new InvalidOperationException("seen");
                }
            )
        );

        thrown.Message.ShouldBe("seen");
        ShouldHaveExited(seen);
    }

    // A program that outlives any patience of these tests.  Windows has no sleep, but ping waits as long.
    private static ProcessStartInfo Sleeper()
    {
        if (OperatingSystem.IsWindows())
        {
            Programs.SkipUnlessOnPath("ping");
            return new ProcessStartInfo("ping", ["-n", "30", "127.0.0.1"]);
        }

        Programs.SkipUnlessOnPath("sleep");
        return new ProcessStartInfo("sleep", ["30"]);
    }

    // RunAsync does not give the program up before it is gone.  Its Process is disposed by then, so the test asks
    // the system for the id it saw.
    private static void ShouldHaveExited(int id)
    {
        id.ShouldNotBe(0);
        _ = Should.Throw<ArgumentException>(() => Process.GetProcessById(id).Dispose());
    }
}
