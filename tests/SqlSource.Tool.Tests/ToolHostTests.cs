using System;
using System.IO;
using Shouldly;
using SqlSource.Tool.Processes;
using Xunit;

namespace SqlSource.Tool.Tests;

public class ToolHostTests
{
    [Fact]
    public void Create_RealHost_IsTheConsoleTheCurrentDirectoryAndTheEnvironment()
    {
        var host = ToolHost.Create();

        host.Out.ShouldBeSameAs(Console.Out);
        host.Error.ShouldBeSameAs(Console.Error);
        host.WorkingDirectory.ShouldBe(Directory.GetCurrentDirectory());
        host.GetEnvironmentVariable("PATH").ShouldBe(Environment.GetEnvironmentVariable("PATH"));
        host.GetEnvironmentVariable("SQLSOURCE_NOT_A_VARIABLE").ShouldBeNull();
        _ = host.Processes.ShouldBeOfType<ProcessRunner>();
        host.TempDirectory.ShouldBe(Path.GetTempPath());
        host.ProcessorCount.ShouldBe(Environment.ProcessorCount);
    }
}
