using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// A filter narrows what a run changes as well as what it describes: a run for one database, one file or one
// project leaves every other sidecar as it was.
public sealed class FilterRunTests : IDisposable
{
    private readonly DescribeScene _scene = new();
    private readonly string _invoices;
    private readonly string _plain;
    private readonly string _users;

    public FilterRunTests()
    {
        _invoices = _scene.Project.AddSql("Invoices.sql", "-- name: ListInvoices\n-- database: billing\nSELECT 3;\n");
        _plain = _scene.Project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 4;\n");
        _users = _scene.Project.AddSql("Users.sql", SqlTexts.Users);
    }

    public void Dispose() => _scene.Dispose();

    private static string HashOf(string sqlPath, string query) =>
        SidecarFiles.Read(sqlPath).Find(query).ShouldNotBeNull().Hash;

    // A file as a run leaves it when it is killed between the write of a sidecar and the move, long ago.
    private string Leftover(string sqlName)
    {
        var path = _scene.Run.Folder.PathOf($"{sqlName}.json.0123456789abcdef0123456789abcdef.tmp");
        File.WriteAllText(path, "{");
        File.SetLastWriteTimeUtc(path, SidecarFiles.Old);
        return path;
    }

    // The .sql file of the first is gone since, so no run looks at its sidecar again.
    [Fact]
    public async Task Run_OnTheWhole_RemovesTheTemporaryFilesThatAKilledRunLeft()
    {
        var ofAFileThatIsGone = Leftover("Gone.sql");
        var ofAFileOfTheRun = Leftover("Users.sql");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0, result.Error);
        File.Exists(ofAFileThatIsGone).ShouldBeFalse();
        File.Exists(ofAFileOfTheRun).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_Database_RemovesOnlyTheTemporaryFilesOfTheSidecarsItLooksAt()
    {
        _ = await _scene.DescribeAsync();
        var ofAFileThatIsGone = Leftover("Gone.sql");
        var ofAFileOfAnotherDatabase = Leftover("Plain.sql");
        var ofAFileOfTheRun = Leftover("Invoices.sql");

        var result = await _scene.DescribeAsync("--database", "billing");

        result.ExitCode.ShouldBe(0, result.Error);
        File.Exists(ofAFileThatIsGone).ShouldBeTrue();
        File.Exists(ofAFileOfAnotherDatabase).ShouldBeTrue();
        File.Exists(ofAFileOfTheRun).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_Check_RemovesNoTemporaryFile()
    {
        _ = await _scene.DescribeAsync();
        var ofAFileThatIsGone = Leftover("Gone.sql");
        var ofAFileOfTheRun = Leftover("Users.sql");

        var result = await _scene.DescribeAsync("--check");

        result.ExitCode.ShouldBe(0, result.Error);
        File.Exists(ofAFileThatIsGone).ShouldBeTrue();
        File.Exists(ofAFileOfTheRun).ShouldBeTrue();
    }

    [Fact]
    public async Task Run_DatabaseWithTheOthersEntriesCurrent_WritesTheMixedFile()
    {
        _ = await _scene.DescribeAsync();
        var getUser = HashOf(_users, "GetUser");
        var getInvoice = HashOf(_users, "GetInvoice");
        await File.WriteAllTextAsync(
            _users,
            SqlTexts.WithSecondQueryEdited(SqlTexts.Users),
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--database", "billing");

        result.ShouldBe(new CliResult(0, "billing (postgres): 1 described, 1 skipped, 0 failed\n", ""));
        HashOf(_users, "GetUser").ShouldBe(getUser);
        HashOf(_users, "GetInvoice").ShouldNotBe(getInvoice);
    }

    [Fact]
    public async Task Run_DatabaseWithOneOfTheOthersEntriesStale_DoesNotWriteAndSaysWhichQueryIsNotInTheRun()
    {
        _ = await _scene.DescribeAsync();
        var before = await File.ReadAllBytesAsync(SidecarFormat.PathFor(_users), TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            _users,
            SqlTexts.WithSecondQueryEdited(SqlTexts.Users.Replace("SELECT 1;", "SELECT 11;", StringComparison.Ordinal)),
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync("--database", "billing");

        result.ShouldBe(
            new CliResult(
                1,
                "billing (postgres): 1 described, 1 skipped, 0 failed\n",
                $"{_users} : error SQLSRC217: The sidecar of '{_users}' was not written, because not every query of "
                    + "the file has an entry\n"
                    + "    query: GetUser: not in this run\n"
                    + "    help: describe the whole file, or fix the queries that failed\n"
                    + Expected.See("SQLSRC217")
                    + DescribeScene.RunAgain
            )
        );
        (await File.ReadAllBytesAsync(SidecarFormat.PathFor(_users), TestContext.Current.CancellationToken)).ShouldBe(
            before
        );
    }

    // A sidecar of a newer tool is an error where it is read.  This one is not read.
    [Fact]
    public async Task Run_FileWhoseQueriesAllBelongToAnotherDatabase_IsNotReadWrittenOrDeleted()
    {
        await File.WriteAllTextAsync(
            SidecarFormat.PathFor(_invoices),
            SidecarFiles.Newer,
            TestContext.Current.CancellationToken
        );
        File.SetLastWriteTimeUtc(SidecarFormat.PathFor(_invoices), SidecarFiles.Old);

        var result = await _scene.DescribeAsync("--database", "postgres", "--force");

        result.Error.ShouldNotContain("SQLSRC221");
        (await File.ReadAllTextAsync(SidecarFormat.PathFor(_invoices), TestContext.Current.CancellationToken)).ShouldBe(
            SidecarFiles.Newer
        );
        File.GetLastWriteTimeUtc(SidecarFormat.PathFor(_invoices)).ShouldBe(SidecarFiles.Old);
    }

    [Theory]
    [InlineData("App.csproj", "--database", "postgres")]
    [InlineData("App.csproj", "Users.sql")]
    [InlineData("App.slnx", "--project", "App.csproj")]
    public async Task Run_FileThatNeedsNoEntry_KeepsItsSidecarUnderEachFilter(params string[] args)
    {
        // Two units stand in the working directory, so each run names its own.
        _ = _scene.Run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"App.csproj\" /></Solution>");
        await File.WriteAllTextAsync(
            SidecarFormat.PathFor(_plain),
            "of a query that needed an entry once",
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync(args);

        // The run reached its end, where the summary is written.
        result.Out.ShouldContain("postgres (postgres): 1 described, 0 skipped, 0 failed\n");
        result.Error.ShouldNotContain("SQLSRC218");
        (await File.ReadAllTextAsync(SidecarFormat.PathFor(_plain), TestContext.Current.CancellationToken)).ShouldBe(
            "of a query that needed an entry once"
        );
    }

    [Fact]
    public async Task Run_FileThatNeedsNoEntry_LosesItsSidecarInARunWithNoFilter()
    {
        await File.WriteAllTextAsync(
            SidecarFormat.PathFor(_plain),
            "of a query that needed an entry once",
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        File.Exists(SidecarFormat.PathFor(_plain)).ShouldBeFalse();
    }

    // The plan drops a setting or a claim it could not read, so after an error a file can look as if it needs no
    // entry.  A run that reported anything deletes nothing.
    [Fact]
    public async Task Run_FileThatNeedsNoEntry_KeepsItsSidecarInARunThatReportedAnError()
    {
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser", "relation \"users\" does not exist");
        await File.WriteAllTextAsync(
            SidecarFormat.PathFor(_plain),
            "of a query that needed an entry once",
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("SQLSRC999");
        (await File.ReadAllTextAsync(SidecarFormat.PathFor(_plain), TestContext.Current.CancellationToken)).ShouldBe(
            "of a query that needed an entry once"
        );
    }

    // A sidecar of a newer tool is left alone by a run that is allowed to delete, as it is by one that reads it.
    [Fact]
    public async Task Run_FileThatNeedsNoEntryWithASidecarOfAHigherFormat_KeepsItAndReportsNothing()
    {
        await File.WriteAllTextAsync(
            SidecarFormat.PathFor(_plain),
            SidecarFiles.Newer,
            TestContext.Current.CancellationToken
        );
        var before = await File.ReadAllBytesAsync(SidecarFormat.PathFor(_plain), TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
        (await File.ReadAllBytesAsync(SidecarFormat.PathFor(_plain), TestContext.Current.CancellationToken)).ShouldBe(
            before
        );
    }

    // The planner drops a setting it cannot read and falls back to the next level, so a file can look as if it needs no
    // entry only because of the error: a run that reported anything deletes nothing.
    [Fact]
    public async Task Run_SettingThatIsNotValid_KeepsTheSidecarOfTheFileItHides()
    {
        _scene.Project.Properties["SqlSourceOutput"] = "sql";
        // A query that needs an entry, so that the summary shows the run went on to describe and to decide the files.
        var wanted = _scene.Project.AddSql("Wanted.sql", "-- name: Wanted\n-- output: models\nSELECT 6;\n");
        var broken = _scene.Project.AddSql("Broken.sql", "-- name: Broken\nSELECT 5;\n", ("SqlSourceOutput", "modles"));
        await File.WriteAllTextAsync(
            SidecarFormat.PathFor(broken),
            "of a query that needed an entry",
            TestContext.Current.CancellationToken
        );
        var before = await File.ReadAllBytesAsync(SidecarFormat.PathFor(broken), TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC014: ");
        result.Out.ShouldBe("postgres (postgres): 1 described, 0 skipped, 0 failed\n");
        File.Exists(SidecarFormat.PathFor(wanted)).ShouldBeTrue();
        File.Exists(SidecarFormat.PathFor(broken)).ShouldBeTrue();
        (await File.ReadAllBytesAsync(SidecarFormat.PathFor(broken), TestContext.Current.CancellationToken)).ShouldBe(
            before
        );
    }

    [Fact]
    public async Task Run_SqlPath_DescribesAndWritesThatFileAlone()
    {
        var result = await _scene.DescribeAsync("Users.sql");

        result.ShouldBe(
            new CliResult(
                0,
                "billing (postgres): 1 described, 0 skipped, 0 failed\n"
                    + "postgres (postgres): 1 described, 0 skipped, 0 failed\n",
                ""
            )
        );
        _scene.Describer.Described.Select(request => request.Name).ShouldBe(["GetInvoice", "GetUser"]);
        File.Exists(SidecarFormat.PathFor(_users)).ShouldBeTrue();
        File.Exists(SidecarFormat.PathFor(_invoices)).ShouldBeFalse();
    }

    [Fact]
    public async Task Run_Project_DescribesAndWritesTheFilesOfThatProjectAlone()
    {
        using var run = new CliRun();
        var describer = new FakeDescriber();
        run.Describers.Add(describer);
        run.Environment["SQLSOURCE_CONNECTION"] = Secrets.Connection;
        var a = Plans.Postgres(run.Folder, "A").AnsweredBy(run.Processes);
        var b = Plans.Postgres(run.Folder, "B").AnsweredBy(run.Processes);
        var inA = a.AddSql("A.sql", "-- name: InA\nSELECT 1;\n");
        var inB = b.AddSql("B.sql", "-- name: InB\nSELECT 2;\n");
        var plainOfA = a.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n");
        await File.WriteAllTextAsync(SidecarFormat.PathFor(plainOfA), "kept", TestContext.Current.CancellationToken);
        _ = run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" /></Solution>"
        );

        var result = await run.RunAsync("describe", "--project", a.ProjectPath);

        result.ShouldBe(new CliResult(0, "postgres (postgres): 1 described, 0 skipped, 0 failed\n", ""));
        describer.Described.ShouldHaveSingleItem().Name.ShouldBe("InA");
        File.Exists(SidecarFormat.PathFor(inA)).ShouldBeTrue();
        File.Exists(SidecarFormat.PathFor(inB)).ShouldBeFalse();
        (await File.ReadAllTextAsync(SidecarFormat.PathFor(plainOfA), TestContext.Current.CancellationToken)).ShouldBe(
            "kept"
        );
    }
}
