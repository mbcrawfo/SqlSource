using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Shouldly;
using SqlSource.Parsing;
using SqlSource.Repository;
using SqlSource.Tool.Describing.Describers;
using SqlSource.Tool.Describing.Postgres;
using SqlSource.Tool.Describing.SqlServer;
using SqlSource.Tool.Processes;
using SqlSource.Tool.Testing;
using SqlSource.Tool.Watching;
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
    public void Create_Watch_IsTheRealWatcher() =>
        ToolHost.Create().Watch.ShouldBe(new Func<WatchRequest, IDisposable>(ToolHost.WatchFolder));

    [Fact]
    public void Hosts_Watch_WatchesOnlyUnderTheTemporaryFolder()
    {
        using var folder = new TempFolder();
        var seen = new List<FileChange>();
        var host = Hosts.Create(folder.Path, new FakeProcessRunner(), folder.Path);

        using (host.Watch(new WatchRequest(folder.Path, Recursive: true, Poll: true, seen.Add, _ => { })))
        {
            // The real watcher, which polls here, accepts the folder and is stopped by the dispose.
        }

        // Outside the temporary folder nothing is watched, so a folder that is not there is no fault.
        var outside = Path.Combine(RepositoryRoot.Path(), "no-such-folder");
        host.Watch(new WatchRequest(outside, Recursive: true, Poll: false, seen.Add, _ => { })).Dispose();
        seen.ShouldBeEmpty();
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
    public void Create_OpenReadFile_IsTheRealOpen() =>
        ToolHost.Create().OpenReadFile.ShouldBe(new Func<string, Stream?>(ToolHost.OpenRead));

    [Fact]
    public void Create_RealHost_OpenReadFile_GivesAStreamAndNullForNoFileOrAFolder()
    {
        using var folder = new TempFolder();
        var path = folder.WriteFile("report.zip", "x");
        var host = ToolHost.Create();

        using (var stream = host.OpenReadFile(path).ShouldNotBeNull())
        {
            stream.CanRead.ShouldBeTrue();
            stream.CanWrite.ShouldBeFalse();
            stream.ReadByte().ShouldBe('x');
        }

        host.OpenReadFile(folder.PathOf("missing/report.zip")).ShouldBeNull();
        host.OpenReadFile(folder.PathOf("nothing.zip")).ShouldBeNull();
        host.OpenReadFile(folder.CreateFolder("a-folder")).ShouldBeNull();
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
