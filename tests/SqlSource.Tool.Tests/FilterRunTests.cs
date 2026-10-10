using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using Xunit;

namespace SqlSource.Tool.Tests;

// A filter narrows what a run changes as well as what it describes: a run for one database, one file or one
// project leaves every other sidecar as it was.
public sealed class FilterRunTests : IDisposable
{
    private const string Users =
        "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private static readonly DateTime Old = new(2001, 2, 3, 4, 5, 6, DateTimeKind.Utc);

    private readonly DescribeScene _scene = new();
    private readonly string _invoices;
    private readonly string _plain;
    private readonly string _users;

    public FilterRunTests()
    {
        _invoices = _scene.Project.AddSql("Invoices.sql", "-- name: ListInvoices\n-- database: billing\nSELECT 3;\n");
        _plain = _scene.Project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 4;\n");
        _users = _scene.Project.AddSql("Users.sql", Users);
    }

    public void Dispose() => _scene.Dispose();

    private static string HashOf(string sqlPath, string query) =>
        TestSidecar.Read(sqlPath).Find(query).ShouldNotBeNull().Hash;

    [Fact]
    public async Task Run_DatabaseWithTheOthersEntriesCurrent_WritesTheMixedFile()
    {
        _ = await _scene.DescribeAsync();
        var getUser = HashOf(_users, "GetUser");
        var getInvoice = HashOf(_users, "GetInvoice");
        await File.WriteAllTextAsync(
            _users,
            Users.Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal),
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
        var before = await File.ReadAllBytesAsync(_users + ".json", TestContext.Current.CancellationToken);
        await File.WriteAllTextAsync(
            _users,
            Users
                .Replace("SELECT 1;", "SELECT 11;", StringComparison.Ordinal)
                .Replace("SELECT 2;", "SELECT 22;", StringComparison.Ordinal),
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
                    + $"{DescribeScene.See}217\n"
            )
        );
        (await File.ReadAllBytesAsync(_users + ".json", TestContext.Current.CancellationToken)).ShouldBe(before);
    }

    // A sidecar of a newer tool is an error where it is read.  This one is not read.
    [Fact]
    public async Task Run_FileWhoseQueriesAllBelongToAnotherDatabase_IsNotReadWrittenOrDeleted()
    {
        var newer = SidecarWriter.Write(new Sidecar(2, "9.9.9", EquatableArray<SidecarEntry>.Empty));
        await File.WriteAllTextAsync(_invoices + ".json", newer, TestContext.Current.CancellationToken);
        File.SetLastWriteTimeUtc(_invoices + ".json", Old);

        var result = await _scene.DescribeAsync("--database", "postgres", "--force");

        result.Error.ShouldNotContain("SQLSRC221");
        (await File.ReadAllTextAsync(_invoices + ".json", TestContext.Current.CancellationToken)).ShouldBe(newer);
        File.GetLastWriteTimeUtc(_invoices + ".json").ShouldBe(Old);
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
            _plain + ".json",
            "of a query that needed an entry once",
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync(args);

        // The run reached its end, where the summary is written.
        result.Out.ShouldContain("postgres (postgres): 1 described, 0 skipped, 0 failed\n");
        result.Error.ShouldNotContain("SQLSRC218");
        (await File.ReadAllTextAsync(_plain + ".json", TestContext.Current.CancellationToken)).ShouldBe(
            "of a query that needed an entry once"
        );
    }

    [Fact]
    public async Task Run_FileThatNeedsNoEntry_LosesItsSidecarInARunWithNoFilter()
    {
        await File.WriteAllTextAsync(
            _plain + ".json",
            "of a query that needed an entry once",
            TestContext.Current.CancellationToken
        );

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(0);
        File.Exists(_plain + ".json").ShouldBeFalse();
    }

    // The planner drops a setting it cannot read and falls back to the next level, so a file can look as if it needs no
    // entry only because of the error: a run that reported anything deletes nothing.
    [Fact]
    public async Task Run_SettingThatIsNotValid_KeepsTheSidecarOfTheFileItHides()
    {
        _scene.Project.Properties["SqlSourceOutput"] = "sql";
        var broken = _scene.Project.AddSql("Broken.sql", "-- name: Broken\nSELECT 5;\n", ("SqlSourceOutput", "modles"));
        await File.WriteAllTextAsync(
            broken + ".json",
            "of a query that needed an entry",
            TestContext.Current.CancellationToken
        );
        var before = await File.ReadAllBytesAsync(broken + ".json", TestContext.Current.CancellationToken);

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("error SQLSRC014: ");
        File.Exists(broken + ".json").ShouldBeTrue();
        (await File.ReadAllBytesAsync(broken + ".json", TestContext.Current.CancellationToken)).ShouldBe(before);
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
        File.Exists(_users + ".json").ShouldBeTrue();
        File.Exists(_invoices + ".json").ShouldBeFalse();
    }

    [Fact]
    public async Task Run_Project_DescribesAndWritesTheFilesOfThatProjectAlone()
    {
        using var run = new CliRun();
        var describer = new FakeDescriber();
        run.Describers.Add(describer);
        run.Environment["SQLSOURCE_CONNECTION"] = DescribeScene.Connection;
        var a = Plans.Postgres(run.Folder, "A").AnsweredBy(run.Processes);
        var b = Plans.Postgres(run.Folder, "B").AnsweredBy(run.Processes);
        var inA = a.AddSql("A.sql", "-- name: InA\nSELECT 1;\n");
        var inB = b.AddSql("B.sql", "-- name: InB\nSELECT 2;\n");
        var plainOfA = a.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 3;\n");
        await File.WriteAllTextAsync(plainOfA + ".json", "kept", TestContext.Current.CancellationToken);
        _ = run.Folder.WriteFile(
            "App.slnx",
            "<Solution><Project Path=\"A/A.csproj\" /><Project Path=\"B/B.csproj\" /></Solution>"
        );

        var result = await run.RunAsync("describe", "--project", a.ProjectPath);

        result.ShouldBe(new CliResult(0, "postgres (postgres): 1 described, 0 skipped, 0 failed\n", ""));
        describer.Described.ShouldHaveSingleItem().Name.ShouldBe("InA");
        File.Exists(inA + ".json").ShouldBeTrue();
        File.Exists(inB + ".json").ShouldBeFalse();
        (await File.ReadAllTextAsync(plainOfA + ".json", TestContext.Current.CancellationToken)).ShouldBe("kept");
    }
}
