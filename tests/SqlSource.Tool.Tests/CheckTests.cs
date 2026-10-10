using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tool.Tests;

// "describe --check": every query is described, the sidecars on the disk are compared, and no file is changed.
public sealed class CheckTests : IDisposable
{
    private const string Users = "-- name: GetUser\nSELECT 1;\n\n-- name: ListUsers\nSELECT 2;\n";

    private static readonly DateTime Old = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public CheckTests()
    {
        _users = _scene.Project.AddSql("Users.sql", Users);
    }

    private string Sidecar => _users + ".json";

    public void Dispose() => _scene.Dispose();

    // Describes, and makes the sidecar look old, so that a write would show.
    private async Task<byte[]> DescribeFirstAsync()
    {
        (await _scene.DescribeAsync()).ExitCode.ShouldBe(0);
        File.SetLastWriteTimeUtc(Sidecar, Old);
        return await File.ReadAllBytesAsync(Sidecar, TestContext.Current.CancellationToken);
    }

    private async Task ShouldBeUntouchedAsync(byte[] before)
    {
        (await File.ReadAllBytesAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(before);
        File.GetLastWriteTimeUtc(Sidecar).ShouldBe(Old);
    }

    [Theory]
    [InlineData("--check")]
    [InlineData("--check", "--force")]
    public async Task Check_SidecarsThatDescribeWrote_DescribesEveryQueryAgainAndChangesNoFile(params string[] args)
    {
        var before = await DescribeFirstAsync();

        var result = await _scene.DescribeAsync(args);

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
        // Two by the first run, and two by the check: none is skipped for a current entry.
        _scene.Describer.Described.Count.ShouldBe(4);
        await ShouldBeUntouchedAsync(before);
    }

    [Fact]
    public async Task Check_NoSidecar_MakesNone()
    {
        _ = await _scene.DescribeAsync("--check");

        _scene.Describer.Described.Count.ShouldBe(2);
        File.Exists(Sidecar).ShouldBeFalse();
        Directory.GetFiles(Path.GetDirectoryName(_users)!, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_QueryThatFails_ExitsOneAndChangesNoFile()
    {
        var before = await DescribeFirstAsync();
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC999: ");
        // A description that is not saved is no error of a check, which saves none.
        result.Error.ShouldNotContain("SQLSRC217");
        await ShouldBeUntouchedAsync(before);
    }

    // Without --check a database whose queries are all current needs no connection.
    [Fact]
    public async Task Check_DatabaseWithoutAConnection_IsSqlsrc213ThoughEveryEntryIsCurrent()
    {
        _ = await DescribeFirstAsync();
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_POSTGRES");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC213: No connection is given for the database 'postgres'");
    }

    [Fact]
    public async Task Check_SidecarOfAHigherFormatVersion_IsNotSqlsrc221AndItsQueriesAreDescribed()
    {
        _ = await DescribeFirstAsync();
        var newer = SidecarWriter.Write(TestSidecar.Read(_users) with { FormatVersion = 2 });
        await File.WriteAllTextAsync(Sidecar, newer, TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync("--check");

        result.Error.ShouldNotContain("SQLSRC221");
        _scene.Describer.Described.Count.ShouldBe(4);
        (await File.ReadAllTextAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(newer);
    }
}
