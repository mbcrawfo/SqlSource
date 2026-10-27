using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// "describe" from the command line to the sidecars on the disk, with the describer of the tests.
public sealed class DescribeRunTests : IDisposable
{
    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public DescribeRunTests()
    {
        _users = _scene.Project.AddSql("Users.sql", SqlTexts.TwoUsers);
    }

    private string Sidecar => SidecarFormat.PathFor(_users);

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_QueriesWithoutASidecar_AreDescribedAndTheSidecarIsWritten()
    {
        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        var sidecar = SidecarFiles.Read(_users);
        sidecar.FormatVersion.ShouldBe(SidecarFormat.Version);
        sidecar.ToolVersion.ShouldBe(PackageVersion.Prefix);
        sidecar
            .Queries.Select(entry => $"{entry.Name} {entry.Engine} {entry.Database} {entry.ServerVersion}")
            .ShouldBe(["GetUser postgres postgres 16.4", "ListUsers postgres postgres 16.4"]);
        sidecar.Queries.ShouldAllBe(entry => entry.Hash.Length == 64);
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe(Secrets.Connection);
        _scene.Describer.Closed.ShouldBe(1);
    }

    // A describer's events carry durations, and its clock is the run's.
    [Fact]
    public async Task Run_Describer_IsGivenTheClockOfTheRun()
    {
        _ = await _scene.DescribeAsync();

        _scene.Describer.Opened.ShouldHaveSingleItem().Time.ShouldBeSameAs(_scene.Clock);
    }

    [Fact]
    public async Task Run_SecondRunWithNothingChanged_DescribesNothingAndWritesNothing()
    {
        _ = await _scene.DescribeAsync();
        File.SetLastWriteTimeUtc(Sidecar, SidecarFiles.Old);

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 0 described, 2 skipped, 0 failed\n", ""));
        _scene.Describer.Described.Count.ShouldBe(2);
        _scene.Describer.Opened.Count.ShouldBe(1);
        File.GetLastWriteTimeUtc(Sidecar).ShouldBe(SidecarFiles.Old);
    }

    // The SQL, a declaration, a default and the database are each part of what was described.
    [Theory]
    [InlineData(SqlTexts.GetUser, "-- name: GetUser\nSELECT 11;\n")]
    [InlineData(
        "-- name: GetUser\n-- param: @id int\nSELECT @id;\n",
        "-- name: GetUser\n-- param: @id bigint\nSELECT @id;\n"
    )]
    [InlineData("-- name: GetUser\nSELECT 1 {{w:WHERE 1 = 1}};\n", "-- name: GetUser\nSELECT 1 {{w:WHERE 2 = 2}};\n")]
    [InlineData(SqlTexts.GetUser, "-- name: GetUser\n-- database: billing\nSELECT 1;\n")]
    public async Task Run_QueryThatChanged_IsDescribedAgain(string before, string after)
    {
        await File.WriteAllTextAsync(_users, before, TestContext.Current.CancellationToken);
        (await _scene.DescribeAsync()).Out.ShouldEndWith(": 1 described, 0 skipped, 0 failed\n");
        await File.WriteAllTextAsync(_users, after, TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldEndWith(": 1 described, 0 skipped, 0 failed\n");
        _scene.Describer.Described.Count.ShouldBe(2);
    }

    // A comment changes no type, and is not part of the hash.
    [Fact]
    public async Task Run_QueryWhoseCommentChanged_IsSkipped()
    {
        _ = await _scene.DescribeAsync();
        await File.WriteAllTextAsync(
            _users,
            SqlTexts.TwoUsers.Replace("SELECT 1;", "/* the first */\nSELECT 1;", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 0 described, 2 skipped, 0 failed\n", ""));
    }

    private async Task ShouldBeWrittenAgainAsync(Func<string, Sidecar, string> replace)
    {
        _ = await _scene.DescribeAsync();
        var written = await File.ReadAllTextAsync(Sidecar, TestContext.Current.CancellationToken);
        var sidecar = SidecarFiles.Read(_users);
        await File.WriteAllTextAsync(Sidecar, replace(written, sidecar), TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        (await File.ReadAllTextAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(written);
    }

    [Fact]
    public Task Run_SidecarOfAnotherToolVersion_IsWrittenAgainWithEveryQueryDescribed() =>
        ShouldBeWrittenAgainAsync((_, sidecar) => SidecarWriter.Write(sidecar with { ToolVersion = "0.0.1" }));

    [Fact]
    public Task Run_SidecarOfALowerFormatVersion_IsWrittenAgainWithEveryQueryDescribed() =>
        ShouldBeWrittenAgainAsync((_, sidecar) => SidecarWriter.Write(sidecar with { FormatVersion = 0 }));

    [Fact]
    public Task Run_SidecarWithAMergeConflict_IsWrittenAgainWithEveryQueryDescribed() =>
        ShouldBeWrittenAgainAsync((written, _) => "<<<<<<< HEAD\n" + written);

    [Fact]
    public async Task Run_SidecarOfAHigherFormatVersion_IsSqlsrc221AndTheFileIsLeftAlone()
    {
        _ = await _scene.DescribeAsync();
        var newer = SidecarWriter.Write(SidecarFiles.Read(_users) with { FormatVersion = 2 });
        await File.WriteAllTextAsync(Sidecar, newer, TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync("--force");

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 0 described, 0 skipped, 2 failed\n",
                $"{Sidecar} : error SQLSRC221: '{Sidecar}' has format 2, and this tool writes format 1\n"
                    + "    help: update the SqlSource.Tool package\n"
                    + Expected.See("SQLSRC221")
                    + DescribeScene.RunAgain
            )
        );
        (await File.ReadAllTextAsync(Sidecar, TestContext.Current.CancellationToken)).ShouldBe(newer);
        _scene.Describer.Described.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Run_Force_DescribesWhatIsCurrent()
    {
        _ = await _scene.DescribeAsync();
        _scene.Describer.Server = _scene.Describer.Server with { Version = "17.0" };

        var result = await _scene.DescribeAsync("--force");

        result.ShouldBe(new CliResult(0, "postgres (postgres): 2 described, 0 skipped, 0 failed\n", ""));
        SidecarFiles.Read(_users).Queries.ShouldAllBe(entry => entry.ServerVersion == "17.0");
    }

    [Fact]
    public async Task Run_OneQueryFails_TheOthersAreDescribedAndTheFileIsNotWritten()
    {
        _scene.Describer.Failures["ListUsers"] = FakeDescriber.Failure("ListUsers", "42601: syntax error");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 1 described, 0 skipped, 1 failed\n",
                $"{_users}(4,10): error SQLSRC999: The server rejected the query 'ListUsers'\n"
                    + "    query: ListUsers, database postgres, postgres 16.4, FakeDriver 1.2.3\n"
                    + "    step: describe columns\n"
                    + "    server: 42601: syntax error\n"
                    + "    help: check the SQL\n"
                    + "    see: https://example.test/sqlsrc999\n"
                    + $"{_users} : error SQLSRC217: The sidecar of '{_users}' was not written, because not every "
                    + "query of the file has an entry\n"
                    + "    query: ListUsers: failed\n"
                    + "    help: describe the whole file, or fix the queries that failed\n"
                    + Expected.See("SQLSRC217")
                    + DescribeScene.RunAgain
            )
        );
        File.Exists(Sidecar).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_SessionCannotBeOpened_IsReportedOnceForTheDatabase()
    {
        _scene.Describer.OpenFailure = FakeDescriber.Failure("(open)", "28P01: password authentication failed");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe("postgres (postgres): 0 described, 0 skipped, 2 failed\n");
        result.Error.Split("error SQLSRC").Length.ShouldBe(2);
        result.Error.ShouldStartWith($"{_users}(1,10): error SQLSRC999: ");
        File.Exists(Sidecar).ShouldBeFalse();
    }

    // A developer who has one of a project's two databases is not stopped by the other.
    [Fact]
    public async Task Run_NoConnectionAndEveryQueryCurrent_IsNoErrorAndTheSummarySaysSo()
    {
        _ = await _scene.DescribeAsync();
        _scene.Run.Environment.Clear();

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): no connection, 2 skipped\n", ""));
    }

    [Fact]
    public async Task Run_NoConnectionAndOneQueryStale_IsSqlsrc213Once()
    {
        _ = await _scene.DescribeAsync();
        await File.WriteAllTextAsync(
            _users,
            SqlTexts.WithSecondQueryEdited(SqlTexts.TwoUsers),
            TestContext.Current.CancellationToken
        );
        _scene.Run.Environment.Clear();

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 0 described, 1 skipped, 1 failed\n",
                $"{_users}(4,10): error SQLSRC213: No connection is given for the database 'postgres'\n"
                    + "    help: pass --connection postgres=<connection string>, or set SQLSOURCE_CONNECTION_POSTGRES\n"
                    + Expected.See("SQLSRC213")
                    + DescribeScene.RunAgain
            )
        );
    }

    // Under "--force" every query is described, so a database of the run always needs its connection.
    [Fact]
    public async Task Run_NoConnectionUnderForce_IsSqlsrc213ThoughEveryQueryIsCurrent()
    {
        _ = await _scene.DescribeAsync();
        _scene.Run.Environment.Clear();

        var result = await _scene.DescribeAsync("--force");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldStartWith($"{_users}(1,10): error SQLSRC213: ");
        result.Out.ShouldBe("postgres (postgres): 0 described, 0 skipped, 2 failed\n");
    }

    [Fact]
    public async Task Run_ConnectionOnTheCommandLine_IsTheOneTheDescriberIsGiven()
    {
        var result = await _scene.DescribeAsync("--connection", "POSTGRES=Host=other;Port=5433");

        result.ExitCode.ShouldBe(0);
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=other;Port=5433");
    }

    // The plan reports SQLSRC209 for the file.  Its queries count as failed, and the run adds no error of its own.
    [Fact]
    public async Task Run_FileThatIsNotReady_FailsItsQueriesWithThePlansErrorAlone()
    {
        _scene.Project.Properties["SqlSourceDialect"] = "ansi";

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe("ansi (ansi): 0 described, 0 skipped, 2 failed\n");
        result.Error.Split("error SQLSRC").Length.ShouldBe(2);
        result.Error.ShouldContain("error SQLSRC209: ");
        _scene.Describer.Opened.ShouldBeEmpty();
    }

    // The plan reports SQLSRC016 for a settings file that the .sql file reaches, at its line and column.  The queries
    // are described all the same, and the exit code is 1.
    [Fact]
    public async Task Run_SettingsFileWithAnError_IsReportedAtItsPlaceAndFailsTheRun()
    {
        var settings = _scene.Project.AddSettings(
            "sqlsource.json", /*lang=json,strict*/
            "{\n  \"Dialect\": \"postgres\"\n}"
        );

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 2 described, 0 skipped, 0 failed\n",
                $"{settings}(2,3): error SQLSRC016: 'Dialect' is not a key of a settings file\n"
                    + Expected.See("SQLSRC016")
                    + DescribeScene.RunAgain
            )
        );
    }

    // A database that no query of the run can be described for is none that the variable could be meant for.
    [Fact]
    public async Task Run_VariableWithNoNameAndASecondDatabaseThatCannotBeDescribed_IsUsedForTheOneThatCan()
    {
        _ = _scene.Project.AddSql("Old.sql", "-- dialect: ansi\n-- name: Old\nSELECT 1;\n");
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_POSTGRES");
        _scene.Run.Environment["SQLSOURCE_CONNECTION"] = "Host=one";

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.Split("error SQLSRC").Length.ShouldBe(2);
        result.Error.ShouldContain("error SQLSRC209: ");
        result
            .Out.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .ShouldBe(
                [
                    "ansi (ansi): 0 described, 0 skipped, 1 failed",
                    "postgres (postgres): 2 described, 0 skipped, 0 failed",
                ],
                ignoreOrder: true
            );
        _scene.Describer.Opened.ShouldHaveSingleItem().Connection.ShouldBe("Host=one");
    }

    // The scene's describers are cleared to stand for a dialect that a version of the tool has no describer for.
    [Fact]
    public async Task Run_NoDescriberRegistered_IsSqlsrc216AtEachQueryAndNothingIsWritten()
    {
        _scene.Run.Describers.Clear();

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 0 described, 0 skipped, 2 failed\n",
                $"{_users}(1,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n"
                    + Expected.See("SQLSRC216")
                    + $"{_users}(4,10): error SQLSRC216: This version of sqlsource cannot describe 'postgres'\n"
                    + Expected.See("SQLSRC216")
                    + DescribeScene.RunAgain
            )
        );
        File.Exists(Sidecar).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_CancelledWhileAQueryIsDescribed_WritesNothingAndPrintsNothing()
    {
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        _scene.Describer.BeforeDescribe = _ => cancel.CancelAsync();

        var result = await _scene.Run.RunAsync(cancel.Token, "describe");

        result.ShouldBe(new CliResult(1, "", ""));
        File.Exists(Sidecar).ShouldBeFalse();
        Directory.EnumerateFiles(_scene.Run.Folder.Path, "*.tmp").ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_NoConnectionAndOnlyAQueryThatFailedInThePlan_IsNoSqlsrc213AndTheSummaryCountsIt()
    {
        _ = await _scene.DescribeAsync();
        _ = _scene.Project.AddSql("Tokens.sql", "-- name: Find\nSELECT 1 {{where}};\n");
        _scene.Run.Environment.Clear();

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC210: ");
        result.Error.ShouldNotContain("SQLSRC213");
        result.Out.ShouldBe("postgres (postgres): 0 described, 2 skipped, 1 failed\n");
    }

    [Fact]
    public async Task Run_DescriberThatThrows_IsSqlsrc200WithoutTheConnectionAndNothingIsWritten()
    {
        _scene.Describer.Exceptions["ListUsers"] = new InvalidOperationException($"bad {Secrets.Connection}");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                1,
                "",
                "sqlsource : error SQLSRC200: sqlsource failed unexpectedly: System.InvalidOperationException: "
                    + "bad ***\n"
                    + Expected.See("SQLSRC200")
                    + DescribeScene.RunAgain
            )
        );
        File.Exists(Sidecar).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_QueryThatWasDeleted_LosesItsEntry()
    {
        _ = await _scene.DescribeAsync();
        await File.WriteAllTextAsync(_users, SqlTexts.GetUser, TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "postgres (postgres): 0 described, 1 skipped, 0 failed\n", ""));
        SidecarFiles.Read(_users).Queries.ShouldHaveSingleItem().Name.ShouldBe("GetUser");
    }

    [Fact]
    public async Task Run_FileThatNeedsNoEntryAnyMore_LosesItsSidecar()
    {
        _ = await _scene.DescribeAsync();
        File.Exists(Sidecar).ShouldBeTrue();
        await File.WriteAllTextAsync(
            _users,
            "-- output: sql\n" + SqlTexts.TwoUsers,
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync();

        result.ShouldBe(new CliResult(0, "", ""));
        File.Exists(Sidecar).ShouldBeFalse();
    }
}
