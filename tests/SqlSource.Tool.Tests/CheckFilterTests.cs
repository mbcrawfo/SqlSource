using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Snapshot;
using SqlSource.Tool.Describing;
using Xunit;

namespace SqlSource.Tool.Tests;

// "--check" compares within what "describe" with the same filters would change, and counts what it found.
public sealed class CheckFilterTests : IDisposable
{
    private const string Users =
        "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n";

    private static readonly QueryDescription Rows = new(
        SidecarResultKind.Rows,
        EquatableArray<DescribedParameter>.Empty,
        EquatableArray<SidecarColumn>.Empty,
        null,
        "not-needed",
        "no-origin"
    );

    private readonly DescribeScene _scene = new();
    private readonly string _plain;
    private readonly string _users;

    public CheckFilterTests()
    {
        // "Invoices.sql" comes before "Users.sql", so the plan holds "billing" first.
        _ = _scene.Project.AddSql("Invoices.sql", "-- name: ListInvoices\n-- database: billing\nSELECT 3;\n");
        _plain = _scene.Project.AddSql("Plain.sql", "-- name: Plain\n-- output: sql\nSELECT 4;\n");
        _users = _scene.Project.AddSql("Users.sql", Users);
        // Two units stand in the working directory for the test of "--project", so each run names its own.
        _ = _scene.Run.Folder.WriteFile("App.slnx", "<Solution><Project Path=\"App.csproj\" /></Solution>");
    }

    public void Dispose() => _scene.Dispose();

    private Task<CliResult> DescribeAsync(params string[] args) => _scene.DescribeAsync(["App.csproj", .. args]);

    private void AddASidecarToPlain() =>
        File.WriteAllText(
            _plain + ".json",
            SidecarWriter.Write(
                new Sidecar(SidecarFormat.Version, PackageVersion.Prefix, EquatableArray<SidecarEntry>.Empty)
            )
        );

    [Fact]
    public async Task Check_NoFilter_CountsEachQueryAsSameDifferOrFailed()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Descriptions["GetInvoice"] = Rows;
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
        _scene.Describer.Descriptions["GetUser"] = Rows;

        var result = await DescribeAsync("--check", "--database", "billing");

        result.ShouldBe(new CliResult(0, "billing (postgres): 2 same, 0 differ, 0 failed\n", ""));
    }

    [Fact]
    public async Task Check_FileFilterWithADifferenceInAnotherFile_ExitsZero()
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        _scene.Describer.Descriptions["GetUser"] = Rows;

        var result = await DescribeAsync("--check", "Invoices.sql");

        result.ShouldBe(new CliResult(0, "billing (postgres): 1 same, 0 differ, 0 failed\n", ""));
    }

    // An entry of a query that is gone is reported when its own database is named, and not when another is.
    [Theory]
    [InlineData("billing", true)]
    [InlineData("postgres", false)]
    public async Task Check_EntryOfAQueryThatIsGoneUnderDatabase_IsReportedForItsOwnDatabaseAlone(
        string named,
        bool reported
    )
    {
        (await DescribeAsync()).ExitCode.ShouldBe(0);
        var sidecar = TestSidecar.Read(_users);
        var gone = sidecar.Find("GetInvoice").ShouldNotBeNull() with { Name = "Gone" };
        await File.WriteAllTextAsync(
            _users + ".json",
            SidecarWriter.Write(
                sidecar with
                {
                    Queries = new EquatableArray<SidecarEntry>([.. sidecar.Queries, gone]),
                }
            ),
            TestContext.Current.CancellationToken
        );

        var result = await DescribeAsync("--check", "--database", named);

        result.ExitCode.ShouldBe(reported ? 2 : 0);
        result.Error.Contains("for 'Gone': the query is not in the file", StringComparison.Ordinal).ShouldBe(reported);
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
        File.Exists(_plain + ".json").ShouldBeTrue();
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

        var result = await DescribeAsync("--check", "--database", "nowhere");

        result.ShouldBe(new CliResult(0, "nowhere: 0 same, 0 differ, 0 failed\n", ""));
    }
}
