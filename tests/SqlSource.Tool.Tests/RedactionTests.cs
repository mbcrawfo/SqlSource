using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// No value of a connection reaches the log: not from the command line, and not from a describer.
public sealed class RedactionTests : IDisposable
{
    private const string Marker = "s3cret-marker";

    private const string Value = "Host=db;Password=" + Marker;

    private readonly DescribeScene _scene = new();

    public RedactionTests()
    {
        _scene.Run.Environment.Clear();
        _ = _scene.Project.AddSql("Users.sql", "-- name: GetUser\nSELECT 1;\n");
    }

    private string Log => _scene.Run.Folder.PathOf("run.log");

    public void Dispose() => _scene.Dispose();

    // Whichever way the option was written, the token keeps the option and the name and loses the value.
    [Theory]
    [InlineData("--connection|postgres=***", "--connection", "postgres=" + Value)]
    [InlineData("--connection=postgres=***", "--connection=postgres=" + Value)]
    [InlineData("--connection:postgres=***", "--connection:postgres=" + Value)]
    public async Task Run_ConnectionOnTheCommandLine_IsLoggedWithItsNameAndNoValue(string logged, params string[] args)
    {
        var result = await _scene.DescribeAsync(["--log", "run.log", .. args]);

        result.ExitCode.ShouldBe(0);
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Value);
        LogReader
            .Read(Log)[0]
            .GetProperty("arguments")
            .EnumerateArray()
            .Select(item => item.GetString())
            .ShouldBe(["describe", "--log", "run.log", .. logged.Split('|')]);
        (await File.ReadAllTextAsync(Log, TestContext.Current.CancellationToken)).ShouldNotContain(Marker);
    }

    // The value was typed where "name=value" stands and the shell split it: the line is wrong, and nothing is logged.
    [Fact]
    public async Task Run_WrongCommandLineThatHoldsAValue_WritesNoLog()
    {
        var result = await _scene.DescribeAsync(
            "--log",
            "run.log",
            "--connection",
            "postgres=Host=db;",
            "Password=" + Marker
        );

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldNotContain(Marker);
        File.Exists(Log).ShouldBeFalse();
    }
}
