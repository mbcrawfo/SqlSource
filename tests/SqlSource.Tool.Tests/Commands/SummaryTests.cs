using System;
using System.IO;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.Testing;
using SqlSource.Tool.Testing;
using Xunit;

namespace SqlSource.Tool.Tests.Commands;

// The lines that end a run: one for each selected database, and one for each name of "--database".
public sealed class SummaryTests : IDisposable
{
    private readonly DescribeScene _scene = new();
    private readonly string _users;

    public SummaryTests()
    {
        // "Invoices.sql" comes before "Users.sql", so the plan holds "billing" first.
        _ = _scene.Project.AddSql("Invoices.sql", SqlTexts.ListInvoices);
        _users = _scene.Project.AddSql("Users.sql", SqlTexts.Users);
    }

    public void Dispose() => _scene.Dispose();

    [Fact]
    public async Task Run_TwoDatabases_HaveALineEachInThePlansOrder()
    {
        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                0,
                "billing (postgres): 2 described, 0 skipped, 0 failed\n"
                    + "postgres (postgres): 1 described, 0 skipped, 0 failed\n",
                ""
            )
        );
    }

    [Fact]
    public async Task Run_DescribedSkippedAndFailed_AreEachCounted()
    {
        _ = await _scene.DescribeAsync();
        await File.WriteAllTextAsync(
            _users,
            SqlTexts.WithSecondQueryEdited(SqlTexts.Users) + "\n-- name: Bad\nSELECT 4;\n",
            TestContext.Current.CancellationToken
        );
        _scene.Describer.Failures["Bad"] = FakeDescriber.Failure("Bad");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe(
            "billing (postgres): 1 described, 1 skipped, 0 failed\n"
                + "postgres (postgres): 0 described, 1 skipped, 1 failed\n"
        );
    }

    [Fact]
    public async Task Run_DatabaseWithoutAConnectionAndNothingToDescribe_SaysNoConnection()
    {
        _ = await _scene.DescribeAsync();
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_BILLING");

        var result = await _scene.DescribeAsync();

        result.ShouldBe(
            new CliResult(
                0,
                "billing (postgres): no connection, 2 skipped\npostgres (postgres): 0 described, 1 skipped, 0 failed\n",
                ""
            )
        );
    }

    // The "no connection" form has no place for a failure, so a database with one has the three counts.
    [Fact]
    public async Task Run_DatabaseWithoutAConnectionAndAFailedQuery_HasTheThreeCounts()
    {
        _ = await _scene.DescribeAsync();
        _ = _scene.Project.AddSql("Tokens.sql", "-- name: Find\n-- database: billing\nSELECT 1 {{where}};\n");
        _ = _scene.Run.Environment.Remove("SQLSOURCE_CONNECTION_BILLING");

        var result = await _scene.DescribeAsync();

        result.ExitCode.ShouldBe(1);
        result.Out.ShouldBe(
            "billing (postgres): 0 described, 2 skipped, 1 failed\n"
                + "postgres (postgres): 0 described, 1 skipped, 0 failed\n"
        );
    }

    [Fact]
    public async Task Run_Database_CountsOnlyTheSelectedQueriesAndSpellsTheNameAsThePlanDoes()
    {
        var result = await _scene.DescribeAsync("--database", "BILLING");

        result.Out.ShouldBe("billing (postgres): 2 described, 0 skipped, 0 failed\n");
        // "Users.sql" was held back by GetUser, which is not in the run and has no entry.
        result.ExitCode.ShouldBe(1);
        result.Error.ShouldContain("    query: GetUser: not in this run\n");
    }

    // A filter that matched nothing is visible.  The name is printed because it is near a database of the plan.
    [Fact]
    public async Task Run_DatabaseCloseToADatabase_IsNamed()
    {
        var result = await _scene.DescribeAsync("--database", "postgres", "--database", "biling");

        result.ShouldBe(
            new CliResult(
                1,
                "postgres (postgres): 1 described, 0 skipped, 0 failed\nbiling: 0 described, 0 skipped, 0 failed\n",
                result.Error
            )
        );
        result.Error.ShouldContain("    query: GetInvoice: not in this run\n");
    }

    [Fact]
    public async Task Run_DatabaseCloseToADatabaseUnderCheck_IsNamedWithTheWordsOfACheck()
    {
        var result = await _scene.DescribeAsync("--check", "--database", "biling");

        result.Out.ShouldBe("biling: 0 same, 0 differ, 0 failed\n");
    }

    // A secret typed after --database passes the rule of a database name, and is printed by its position instead.
    [Fact]
    public async Task Run_DatabaseFarFromEveryDatabase_IsNamedByItsPosition()
    {
        var result = await _scene.DescribeAsync("--database", Secrets.Marker);

        result.Out.ShouldBe("--database at position 3: 0 described, 0 skipped, 0 failed\n");
        result.Error.ShouldNotContain(Secrets.Marker);
    }

    [Fact]
    public async Task Run_DatabaseFarFromEveryDatabaseUnderCheck_IsNamedByItsPositionWithTheWordsOfACheck()
    {
        var result = await _scene.DescribeAsync("--check", "--database", Secrets.Marker);

        result.Out.ShouldBe("--database at position 4: 0 same, 0 differ, 0 failed\n");
        result.Error.ShouldNotContain(Secrets.Marker);
    }

    // The filter keeps the first spelling of a name, and the line names the position of that value.
    [Fact]
    public async Task Run_DatabaseGivenTwiceFarFromEveryDatabase_IsNamedByTheFirstPosition()
    {
        var result = await _scene.DescribeAsync("--database", "a", "--database", "A");

        result.Out.ShouldBe("--database at position 3: 0 described, 0 skipped, 0 failed\n");
    }

    // A database of the plan counts as one to be near, though no query of it is selected.
    [Fact]
    public async Task Run_DatabaseCloseToADatabaseWithNoSelectedQuery_IsNamedAsTyped()
    {
        var result = await _scene.DescribeAsync("Invoices.sql", "--database", "postgress");

        result.Out.ShouldBe("postgress: 0 described, 0 skipped, 0 failed\n");
    }

    [Fact]
    public async Task Run_DatabaseOfThePlanThatNoSelectedQueryHas_HasThreeZerosAndItsEngine()
    {
        var result = await _scene.DescribeAsync("Invoices.sql", "--database", "Postgres");

        result.ShouldBe(new CliResult(0, "postgres (postgres): 0 described, 0 skipped, 0 failed\n", ""));
        _scene.Describer.Opened.ShouldBeEmpty();
    }
}
