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

    // A caller who begins a transaction and does not pass it: each call is given null, and the transaction is still
    // begun before it and rolled back after it.
    [Theory]
    [InlineData(true, "given")]
    [InlineData(false, "not given")]
    public async Task RunAsync_PassTransaction_GivesTheCallTheTransactionOrNull(bool passTransaction, string seen)
    {
        var database = new FakeDatabase();
        var pair = MethodPairs
            .Find(typeof(PairRunnerTests).Assembly)
            .Single(static pair => pair.Host == typeof(FakeConnectionPairs) && pair.Method == "Run");
        var scenario = new Scenario(
            "FakeConnectionPairs.Run: in a transaction",
            typeof(FakeConnectionPairs),
            "Run",
            MethodKind.Query,
            ScenarioTag.Ordinary,
            new Setup(Transaction: CallerTransaction.Begun, PassTransaction: passTransaction),
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Same);
        database.Commands.ShouldBe(["BEGIN", seen, "ROLLBACK", "BEGIN", seen, "ROLLBACK"]);
    }

    // A caller whose transaction has completed before the call: the call is given it, and the runner's own rollback
    // after the call finds nothing left to roll back.
    [Fact]
    public async Task RunAsync_CompletedTransaction_IsRolledBackBeforeTheCall()
    {
        var database = new FakeDatabase();
        var pair = MethodPairs
            .Find(typeof(PairRunnerTests).Assembly)
            .Single(static pair => pair.Host == typeof(FakeConnectionPairs) && pair.Method == "Run");
        var scenario = new Scenario(
            "FakeConnectionPairs.Run: completed",
            typeof(FakeConnectionPairs),
            "Run",
            MethodKind.Query,
            ScenarioTag.Other,
            new Setup(Transaction: CallerTransaction.Completed),
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Same);
        database.Commands.ShouldBe(["BEGIN", "ROLLBACK", "given", "BEGIN", "ROLLBACK", "given"]);
    }

    // Each run of a scenario that writes has a copy of the database of its own, whose name a server's message may hold.
    [Fact]
    public async Task RunAsync_MessageNamingTheFreshDatabase_IsTheSame()
    {
        var database = new FakeDatabase();
        var pair = MethodPairs
            .Find(typeof(PairRunnerTests).Assembly)
            .Single(static pair => pair.Host == typeof(FakeConnectionPairs) && pair.Method == "FailIn");
        var scenario = new Scenario(
            "FakeConnectionPairs.FailIn: on a copy",
            typeof(FakeConnectionPairs),
            "FailIn",
            MethodKind.Query,
            ScenarioTag.DatabaseError,
            new Setup(Writes: true),
            new FakePairs.Args(1)
        );

        var verdict = await PairRunner.RunAsync(database, pair, scenario, []);

        verdict.Agreement.ShouldBe(Agreement.Same);
    }
}
