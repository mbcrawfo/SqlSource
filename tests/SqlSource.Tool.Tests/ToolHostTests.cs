using System;
using System.IO;
using System.Runtime.InteropServices;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Testing;
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
        host.Runtime.ShouldBe(RuntimeInformation.FrameworkDescription);
        host.OperatingSystem.ShouldBe(RuntimeInformation.OSDescription);
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

    [Fact]
    public void Create_RealHost_ReadTextFile_GivesTextAndNullForNoFileOrAFolder()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile(".env", "KEY=value\r\n");
        var host = ToolHost.Create();

        host.ReadTextFile(path).ShouldBe("KEY=value\r\n");
        host.ReadTextFile(folder.PathOf("missing/.env")).ShouldBeNull();
        host.ReadTextFile(folder.PathOf("nothing.env")).ShouldBeNull();
        host.ReadTextFile(folder.CreateFolder("a-folder")).ShouldBeNull();
    }

    [Fact]
    public void Hosts_ReadTextFile_ReadsOnlyUnderTheTemporaryFolder()
    {
        using var folder = new TempFolder();
        var inside = folder.WriteFile(".env", "KEY=value");
        var outside = Path.Combine(RepositoryRoot.Path(), "README.md");
        File.Exists(outside).ShouldBeTrue();
        var host = Hosts.Create(folder.Path, new FakeProcessRunner(), folder.Path);

        host.ReadTextFile(inside).ShouldBe("KEY=value");
        host.ReadTextFile(outside).ShouldBeNull();
        Hosts.Real(folder.Path, folder.Path).ReadTextFile(outside).ShouldBeNull();
    }
}
