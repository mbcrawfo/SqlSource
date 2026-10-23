using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// "--check" compares within what "describe" with the same filters would change, and counts what it found.
public sealed class CheckFilterTests : IDisposable
{
    private readonly DescribeScene _scene = new();
    private readonly string _plain;
    private readonly string _users;

    public CheckFilterTests()
    {
        // "Invoices.sql" comes before "Users.sql", so the plan holds "billing" first.
        _ = _scene.Project.AddSql("Invoices.sql", "-- name: ListInvoices\n-- database: billing\nSELECT 3;\n");
        _plain = _scene.Project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 4;\n");
        _users = _scene.Project.AddSql("Users.sql", SqlTexts.Users);
        // Two units stand in the working directory for the test of "--project", so each run names its own.
        _ = _scene.Run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"App.csproj\" /></Solution>");
    }

    public void Dispose() => _scene.Dispose();

    private Task<CliResult> DescribeAsync(params string[] args) => _scene.DescribeAsync(["App.csproj", .. args]);

    private void AddASidecarToPlain() => File.WriteAllText(SidecarFormat.PathFor(_plain), SidecarFiles.Empty());

    [Fact]
    public async Task Check_NoFilter_CountsEachQueryAsSameDifferOrFailed()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Descriptions["GetInvoice"] = FakeDescriber.Rows;
        _scene.Describer.Failures["GetUser"] = FakeDescriber.Failure("GetUser");

        var result = await DescribeAsync("--check");

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe(
            "billing (postgres): 1 same, 1 differ, 0 failed\npostgres (postgres): 0 same, 0 differ, 1 failed\n"
        );
    }

    // Each query of a sidecar that is absent or cannot be read counts under "differ".
    [Fact]
    public async Task Check_NoSidecars_CountsEveryQueryUnderDiffer()
    {
        var result = await DescribeAsync("--check");

        result.ExitCode.ShouldBe(2);
        result.Out.ShouldBe(
            "billing (postgres): 0 same, 2 differ, 0 failed\npostgres (postgres): 0 same, 1 differ, 0 failed\n"
        );
    }

    [Fact]
    public async Task Check_DatabaseWithADifferenceInTheOthersEntry_ExitsZero()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Descriptions["GetUser"] = FakeDescriber.Rows;

        var result = await DescribeAsync("--check", "--database", "billing");

        result.ShouldBe(new CliResult(0, "billing (postgres): 2 same, 0 differ, 0 failed\n", ""));
    }

    [Fact]
    public async Task Check_FileFilterWithADifferenceInAnotherFile_ExitsZero()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Descriptions["GetUser"] = FakeDescriber.Rows;

        var result = await DescribeAsync("--check", "Invoices.sql");

        result.ShouldBe(new CliResult(0, "billing (postgres): 1 same, 0 differ, 0 failed\n", ""));
    }

    // The sidecar of Users.sql with an entry for a query that is gone, of this database or of none.
    private async Task AddAnEntryOfAQueryThatIsGoneAsync(string? database)
    {
        var sidecar = SidecarFiles.Read(_users);
        var gone = sidecar.Find("GetInvoice").ShouldNotBeNull() with { Name = "Gone", Database = database };
        await File.WriteAllTextAsync(
            SidecarFormat.PathFor(_users),
            SidecarWriter.Write(
                sidecar with
                {
                    Queries = new EquatableArray<SidecarEntry>([.. sidecar.Queries, gone]),
                }
            ),
            TestContext.Current.CancellationToken
        );
    }

    private static bool ReportsGone(CliResult result) =>
        result.Error.Contains("for 'Gone': the query is not in the file", StringComparison.Ordinal);

    // An entry of a query that is gone makes the file's text differ from what describe would write, so a describe
    // with --database rewrites the file without it, whatever the entry's database: the check reports it whichever
    // database is named, in whatever case.
    [Theory]
    [InlineData("billing")]
    [InlineData("BILLING")]
    [InlineData("postgres")]
    public async Task Check_EntryOfAQueryThatIsGoneUnderDatabase_IsReportedWhenDescribeWouldWriteTheFile(string named)
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        await AddAnEntryOfAQueryThatIsGoneAsync("billing");

        var result = await DescribeAsync("--check", "--database", named);

        result.ExitCode.ShouldBe(2);
        ReportsGone(result).ShouldBeTrue();
    }

    // An entry with no database is nobody's, and describe --database drops it all the same when it rewrites the file.
    [Fact]
    public async Task Check_EntryOfAQueryThatIsGoneWithNoDatabase_IsReportedUnderDatabase()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        await AddAnEntryOfAQueryThatIsGoneAsync(database: null);

        var result = await DescribeAsync("--check", "--database", "billing");

        result.ExitCode.ShouldBe(2);
        ReportsGone(result).ShouldBeTrue();
    }

    // What the check reports, describe with the same filter drops, and the same check then passes.
    [Fact]
    public async Task Check_EntryOfAnotherDatabaseThatIsGone_AgreesWithDescribe()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        await AddAnEntryOfAQueryThatIsGoneAsync("billing");

        var reported = await DescribeAsync("--check", "--database", "postgres");
        var described = await DescribeAsync("--database", "postgres");
        var result = await DescribeAsync("--check", "--database", "postgres");

        reported.ExitCode.ShouldBe(2);
        ReportsGone(reported).ShouldBeTrue();
        described.ExitCode.ShouldBe(0);
        SidecarFiles.Read(_users).Find("Gone").ShouldBeNull();
        result.ShouldBe(new CliResult(0, "postgres (postgres): 1 same, 0 differ, 0 failed\n", ""));
    }

    // A file that a query left out holds back is not written by describe with the same filter, so an entry that is
    // gone stays in it, of the named database or of another, and the check says nothing of it.  The query of the
    // database that is not named is edited, so that it is left out.
    [Theory]
    [InlineData("postgres", "SELECT 2;")]
    [InlineData("billing", "SELECT 1;")]
    public async Task Check_EntryThatIsGoneInAHeldBackFile_IsNotReported(string named, string editedSql)
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        await AddAnEntryOfAQueryThatIsGoneAsync("billing");
        await File.WriteAllTextAsync(
            _users,
            SqlTexts.Users.Replace(editedSql, "SELECT 22;", StringComparison.Ordinal),
            TestContext.Current.CancellationToken
        );
        var before = await File.ReadAllTextAsync(SidecarFormat.PathFor(_users), TestContext.Current.CancellationToken);

        var result = await DescribeAsync("--check", "--database", named);
        var described = await DescribeAsync("--database", named);

        result.ExitCode.ShouldBe(0);
        ReportsGone(result).ShouldBeFalse();
        described.ExitCode.ShouldBe(0);
        (await File.ReadAllTextAsync(SidecarFormat.PathFor(_users), TestContext.Current.CancellationToken)).ShouldBe(
            before
        );
    }

    // Without --database a run knows no database to leave an entry to, whatever else it narrows to.
    [Theory]
    [InlineData("App.csproj", "Users.sql")]
    [InlineData("App.slnx", "--project", "App.csproj")]
    public async Task Check_EntryOfAQueryThatIsGoneUnderAFileOrProjectFilter_IsReported(params string[] args)
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        await AddAnEntryOfAQueryThatIsGoneAsync("billing");

        var result = await _scene.DescribeAsync(["--check", .. args]);

        result.ExitCode.ShouldBe(2);
        ReportsGone(result).ShouldBeTrue();
    }

    // "describe" deletes a sidecar only in a run with no filter, so only such a check says it would.
    [Theory]
    [InlineData(2, "App.csproj")]
    [InlineData(0, "App.csproj", "--database", "postgres", "--database", "billing")]
    [InlineData(0, "App.csproj", "Users.sql", "Invoices.sql")]
    [InlineData(0, "App.slnx", "--project", "App.csproj")]
    public async Task Check_SidecarOfAFileThatNeedsNone_IsADifferenceOnlyWithoutAFilter(
        int exitCode,
        params string[] args
    )
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        AddASidecarToPlain();

        var result = await _scene.DescribeAsync(["--check", .. args]);

        result.ExitCode.ShouldBe(exitCode);
        File.Exists(SidecarFormat.PathFor(_plain)).ShouldBeTrue();
    }

    // What "describe" with a filter wrote, "--check" with the same filter finds the same.
    [Theory]
    [InlineData("App.csproj", "--database", "billing")]
    [InlineData("App.csproj", "Invoices.sql")]
    [InlineData("App.slnx", "--project", "App.csproj")]
    public async Task Check_AfterADescribeWithTheSameFilterThatSucceeded_ExitsZero(params string[] args)
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        (await _scene.DescribeAsync(["--force", .. args])).ExitCode.ShouldBe(0);

        var result = await _scene.DescribeAsync(["--check", .. args]);

        result.ExitCode.ShouldBe(0);
        result.Error.ShouldBeEmpty();
    }

    [Fact]
    public async Task Check_DatabaseNameThatNoQueryHas_HasThreeZerosInTheWordsOfACheck()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);

        var result = await DescribeAsync("--check", "--database", "biling");

        result.ShouldBe(new CliResult(0, "biling: 0 same, 0 differ, 0 failed\n", ""));
    }
}
