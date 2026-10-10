using System;
using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using Xunit;

namespace SqlSource.Tool.Tests;

// What "--database" and a .sql path select, from the command line to the plan.  The plan keeps every query of
// every claimed file; a filter says which of them the run was asked to describe.
public sealed class FilterTests : IDisposable
{
    private const string Secret = "s3cret";

    private const string See =
        "    see: https://github.com/mbcrawfo/SqlSource/blob/main/docs/diagnostics.md#sqlsrc212\n";

    private readonly CliRun _run = new();
    private readonly string _users;

    public FilterTests()
    {
        // The project file is in the working directory, so a run that names no unit finds it.
        var project = new TestProject(_run.Folder, directory: "").AnsweredBy(_run.Processes);
        project.Properties["SqlSourceDialect"] = "postgres";
        _ = project.AddSource(
            "Queries.cs",
            "[SqlSource.SqlSourceGenerate(Path = \"Q\")]\ninternal static partial class Queries;\n"
        );
        _ = project.AddSql("Q/Invoices.sql", "-- database: billing\n-- name: ListInvoices\nSELECT 4;\n");
        _users = project.AddSql(
            "Q/Users.sql",
            "-- name: GetUser\nSELECT 1;\n\n-- name: GetInvoice\n-- database: billing\nSELECT 2;\n\n"
                + "-- name: Plain\n-- output: sql\nSELECT 3;\n"
        );
        _ = project.AddSql("Unclaimed/Other.sql", "SELECT 5;");
    }

    public void Dispose() => _run.Dispose();

    // The names of the queries a command line selects, in the plan's order, for a run that reports nothing.
    private async Task<string[]> SelectedAsync(params string[] args)
    {
        var (plan, result) = await _run.PlanAsync(["describe", .. args]);

        result.ShouldBe(new CliResult(0, "", ""));
        var queries = plan.ShouldNotBeNull().Files.SelectMany(file => file.Queries).ToArray();
        // A filter takes nothing out of the plan.
        queries.Length.ShouldBe(4);
        return [.. queries.Where(query => query.IsSelected).Select(query => query.Query.Name)];
    }

    [Fact]
    public async Task Plan_NoFilter_SelectsEveryQuery() =>
        (await SelectedAsync()).ShouldBe(["ListInvoices", "GetUser", "GetInvoice", "Plain"]);

    [Theory]
    [InlineData("--database", "billing")]
    [InlineData("--database=BILLING")]
    [InlineData("--database", "billing", "--database", "Billing")]
    public async Task Plan_Database_SelectsItsQueriesIgnoringCase(params string[] args) =>
        (await SelectedAsync(args)).ShouldBe(["ListInvoices", "GetInvoice"]);

    [Fact]
    public async Task Plan_TwoDatabases_SelectTheQueriesOfBothAndNoneThatNeedsNoEntry() =>
        (await SelectedAsync("--database", "billing", "--database", "postgres")).ShouldBe([
            "ListInvoices",
            "GetUser",
            "GetInvoice",
        ]);

    [Fact]
    public async Task Plan_SqlPath_SelectsEveryQueryOfThatFile() =>
        (await SelectedAsync("Q/Users.sql")).ShouldBe(["GetUser", "GetInvoice", "Plain"]);

    [Fact]
    public async Task Plan_SqlPathAndDatabase_SelectWhatBothTake() =>
        (await SelectedAsync("Q/Users.sql", "--database", "billing")).ShouldBe(["GetInvoice"]);

    [Fact]
    public async Task Plan_DatabaseThatNoQueryHas_SelectsNothingAndIsNoError() =>
        (await SelectedAsync("--database", "warehouse")).ShouldBeEmpty();

    // Review focus 4.
    [Theory]
    [InlineData("q/users.SQL")]
    [InlineData("./Q/Users.sql")]
    [InlineData("Q/../Q/Users.sql")]
    [InlineData("Q\\Users.sql")]
    public async Task Plan_SqlPathSpelledAnotherWay_SelectsTheFile(string path) =>
        (await SelectedAsync(path)).ShouldBe(["GetUser", "GetInvoice", "Plain"]);

    [Fact]
    public async Task Plan_SqlPathInFull_SelectsTheFile() =>
        (await SelectedAsync(_users)).ShouldBe(["GetUser", "GetInvoice", "Plain"]);

    [Theory]
    [InlineData("App.csproj", "Q/Users.sql")]
    [InlineData("Q/Users.sql", "App.csproj")]
    [InlineData(".", "Q/Users.sql")]
    public async Task Plan_SqlPathBesideAUnit_IsAFilterAndTheOtherPathIsTheUnit(params string[] args) =>
        (await SelectedAsync(args)).ShouldBe(["GetUser", "GetInvoice", "Plain"]);

    [Fact]
    public async Task Run_TwoUnits_IsAWrongCommandLineThatDoesNotRepeatTheSecond()
    {
        var result = await _run.RunAsync("describe", "App.csproj", "Q/Users.sql", Secret);

        result.ShouldBe(new CliResult(1, "", "sqlsource: unexpected argument at position 4\n"));
    }

    [Theory]
    [InlineData("Q/Missing.sql")]
    [InlineData("Unclaimed/Other.sql")]
    public async Task Run_SqlPathThatNoTypeClaims_IsSqlsrc212(string path)
    {
        var result = await _run.RunAsync("describe", path);

        var full = _run.Folder.PathOf(path);
        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"sqlsource : error SQLSRC212: '{full}' is not a .sql file that a type of the run claims\n" + See
            )
        );
    }

    [Fact]
    public async Task Plan_SqlPathThatNoTypeClaimsBesideOneThatIsClaimed_ReportsTheOneAndSelectsTheOther()
    {
        var (plan, result) = await _run.PlanAsync("describe", "Q/Missing.sql", "Q/Users.sql");

        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("SQLSRC212");
        plan.ShouldNotBeNull()
            .Files.SelectMany(file => file.Queries)
            .Where(query => query.IsSelected)
            .Select(query => query.Query.Name)
            .ShouldBe(["GetUser", "GetInvoice", "Plain"]);
    }

    // Review focus 5.
    [Fact]
    public async Task Run_OneSqlPathGivenThreeWaysThatNoTypeClaims_IsSqlsrc212Once()
    {
        var result = await _run.RunAsync("describe", "Q/Missing.sql", "./Q/Missing.sql", "q/MISSING.SQL");

        result.ExitCode.ShouldBe(1);
        result.Error.Split("error SQLSRC212").Length.ShouldBe(2);
    }

    [Theory]
    [InlineData(3, "describe", "--database", "not a name")]
    [InlineData(2, "describe", "--database=" + Secret + "!")]
    public async Task Run_DatabaseThatIsNoName_IsAWrongCommandLineThatDoesNotRepeatIt(
        int position,
        params string[] args
    )
    {
        var result = await _run.RunAsync(args);

        result.ShouldBe(
            new CliResult(
                1,
                "",
                $"sqlsource: the value of option '--database' at position {position} is not a database name\n"
            )
        );
        _run.Processes.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Run_Help_ShowsThePathsAndTheOptionsOfDescribe()
    {
        var result = await _run.RunAsync("describe", "--help");

        result.ExitCode.ShouldBe(0);
        result.Out.ShouldContain("sqlsource describe [<path>...]");
        result.Out.ShouldContain(".sql");
        result.Out.ShouldContain("--database <name>");
        result.Out.ShouldContain("--connection <name=value>");
        result.Out.ShouldContain("--force");
    }
}
