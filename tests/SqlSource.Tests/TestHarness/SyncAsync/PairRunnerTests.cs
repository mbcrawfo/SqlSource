using System.Linq;
using System.Threading.Tasks;
using Shouldly;
using SqlSource.DatabaseTests.TestHarness.SyncAsync;
using Xunit;

namespace SqlSource.Tests.TestHarness.SyncAsync;

public sealed class PairRunnerTests
{
    // A command of a data source refuses a connection, so the runner runs Prepare and the tables' SELECTs on commands
    // of the call's connection.
    [Fact]
    public async Task RunAsync_PrepareAndTables_RunOnTheConnectionOfEachCall()
    {
        var database = new FakeDatabase();
        var pair = MethodPairs
            .Find(typeof(PairRunnerTests).Assembly)
            .Single(static pair =>
                pair.Host == typeof(FakePairs.Queries) && pair.Method == "Get" && pair.Kind == MethodKind.Query
            );
        var scenario = new Scenario(
            "Queries.Get: prepared, with a table",
            typeof(FakePairs.Queries),
            "Get",
            MethodKind.Query,
            ScenarioTag.Ordinary,
            new Setup(Tables: ["SELECT n"], Prepare: "SET n"),
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Same);
        database.Commands.ShouldBe(["SET n", "SELECT n", "SET n", "SELECT n"]);
    }
}
