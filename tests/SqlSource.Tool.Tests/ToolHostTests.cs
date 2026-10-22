using System;
using System.IO;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests;

public sealed class ToolHostTests
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
        _ = host.Describers.Find(SqlDialect.PostgreSql).ShouldBeOfType<PostgresDescriber>();
        _ = host.Describers.Find(SqlDialect.SqlServer).ShouldBeOfType<SqlServerDescriber>();
        host.Exchange("billing").ShouldBeSameAs(LiveExchange.Instance);
        host.Time.ShouldBeSameAs(TimeProvider.System);
    }

    [Fact]
    public void Create_RealHost_MakesAFileForWritingAndEmptiesOneThatIsThere()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile("run.log", "of an earlier run\n");
        var created = folder.PathOf("new.log");

        using (var stream = ToolHost.Create().CreateFile(path))
        {
            stream.CanWrite.ShouldBeTrue();
            stream.CanRead.ShouldBeFalse();
        }

        File.ReadAllText(path).ShouldBeEmpty();
        using (var stream = ToolHost.Create().CreateFile(created))
        {
            stream.WriteByte((byte)'x');
        }

        File.ReadAllText(created).ShouldBe("x");
    }
}
